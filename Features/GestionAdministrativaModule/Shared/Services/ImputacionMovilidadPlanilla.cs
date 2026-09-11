namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Decide con qué FECHA sale impreso cada trayecto en la Planilla de Gasto por Movilidad.
    ///
    /// Es la regla RG-42 del requerimiento funcional ("Exceso del límite diario"): cuando lo
    /// acumulado de un día supera el tope, el exceso se traslada al día siguiente "manteniendo
    /// trazabilidad del registro original". Por eso esto NO toca la base: <c>fecha_salida</c> sigue
    /// siendo la real en todas las pantallas y en toda la data. Lo único que cambia es el valor que
    /// se imprime en la columna FECHA del PDF — no hay columna nueva ni en la planilla ni en la BD.
    ///
    /// Reglas, tal como quedaron definidas con GTH:
    ///
    /// <list type="bullet">
    ///   <item>Un día impreso nunca puede pasar del tope (<c>ga_rendicion_config</c>, S/ 45).</item>
    ///   <item>Ninguna fila puede caer en <b>domingo ni en feriado</b> (Configuración → Feriados).
    ///   El <b>sábado sí</b> es válido, tanto como fecha propia como destino de un traslado — por
    ///   eso se usa <see cref="CalendarioNoLaborable.EsImputable"/> y no <c>EsNoLaborable</c>.</item>
    ///   <item>Si la salida real cayó domingo o feriado, su trayecto se corre al primer día
    ///   imputable: la fecha original no es imprimible aunque nada la haya desplazado.</item>
    ///   <item>El traslado va <b>hacia adelante</b> y no sale del mes de la salida: la planilla es
    ///   de un periodo y una fila de otro mes no pertenece a ese documento.</item>
    ///   <item>Si hacia adelante ya no queda mes, se permite <b>retroceder</b>, y solo a días cuyo
    ///   periodo todavía no fue rendido: meter un gasto en una quincena o semana que ya se rindió
    ///   contradiría una planilla que la jefatura ya recibió firmada.</item>
    /// </list>
    ///
    /// De ahí sale, sin declararlo aparte, el techo del mes: <c>tope × días imputables del mes</c>.
    /// Pasado eso el gasto no entra en la planilla porque no quedan días donde ponerlo, y quien
    /// llama corta con un error (ver <see cref="Resultado.SinUbicar"/>).
    /// </summary>
    public static class ImputacionMovilidadPlanilla
    {
        /// <summary>Un trayecto a imputar. <paramref name="Importe"/> es el que imprime la planilla.</summary>
        public sealed record Trayecto(
            int TrayectoId,
            int WorkerId,
            DateOnly FechaSalida,
            int SolicitudId,
            int Orden,
            decimal Importe);

        /// <summary>
        /// Tramo del mes que ya quedó cubierto por una rendición anterior del trabajador. Se toma
        /// del alcance real de esa planilla (su primera y su última <c>fecha_salida</c>), que es lo
        /// que define en los hechos la semana o la quincena que el trabajador rindió.
        /// </summary>
        public readonly record struct PeriodoRendido(DateOnly Desde, DateOnly Hasta);

        public sealed class Resultado
        {
            /// <summary>trayectoId → fecha con la que sale impreso.</summary>
            public Dictionary<int, DateOnly> FechaPorTrayecto { get; } = new();

            /// <summary>
            /// Trayectos que no entraron en ningún día del mes. Vacío en la práctica: hace falta
            /// que el trabajador supere el techo del mes entero. Quien llama lo convierte en error.
            /// </summary>
            public List<Trayecto> SinUbicar { get; } = new();
        }

        /// <param name="periodosRendidos">
        /// workerId → tramos ya rendidos en otras planillas. <b>Null = no se permite retroceder</b>,
        /// que es como se llama la primera vez: retroceder es el caso raro y no vale la pena pagar
        /// esa consulta si todo entra hacia adelante. Si vuelve algo en
        /// <see cref="Resultado.SinUbicar"/>, se carga y se vuelve a resolver.
        /// </param>
        public static Resultado Resolver(
            IReadOnlyCollection<Trayecto> trayectos,
            CalendarioNoLaborable calendario,
            IReadOnlyDictionary<int, List<PeriodoRendido>>? periodosRendidos = null)
        {
            var resultado = new Resultado();
            if (trayectos.Count == 0) return resultado;

            var limite = calendario.LimiteMovilidad;

            // Que el diccionario venga o no es lo que habilita el retroceso, y NO que traiga algo
            // para ese trabajador: quien nunca rindió antes no tiene ningún día vedado y puede
            // retroceder libremente.
            var permiteRetroceso = periodosRendidos != null;

            // Cada trabajador tiene su propio tope diario, y el mes acota el traslado. Una planilla
            // es de un solo mes, pero se agrupa igual por mes para que una planilla vieja con
            // meses mezclados no arrastre un trayecto de enero a la capacidad de febrero.
            var grupos = trayectos.GroupBy(t => (t.WorkerId, t.FechaSalida.Year, t.FechaSalida.Month));

            foreach (var grupo in grupos)
            {
                var (workerId, anio, mes) = grupo.Key;

                var dias = DiasImputables(calendario, anio, mes);
                if (dias.Count == 0)
                {
                    // Un mes entero de domingos y feriados no existe, pero si la config lo dijera
                    // no hay dónde imputar: se reporta en vez de reventar.
                    resultado.SinUbicar.AddRange(grupo);
                    continue;
                }

                var cerrados  = periodosRendidos?.GetValueOrDefault(workerId) ?? new List<PeriodoRendido>();
                var acumulado = new decimal[dias.Count];

                // El orden decide quién se queda con el día y quién se corre: primero la fecha
                // real, después la solicitud y el trayecto dentro de ella. Así el trayecto 1 se
                // queda en su fecha y el que desborda es el siguiente, no uno al azar.
                var ordenados = grupo
                    .OrderBy(t => t.FechaSalida)
                    .ThenBy(t => t.SolicitudId)
                    .ThenBy(t => t.Orden)
                    .ThenBy(t => t.TrayectoId);

                foreach (var t in ordenados)
                {
                    // Punto de partida: su fecha real, o el primer día imputable a partir de ella
                    // si cayó domingo o feriado. Si su fecha quedó después del último día
                    // imputable del mes, arranca fuera del arreglo y solo le queda retroceder.
                    var inicio = PrimerIndiceDesde(dias, t.FechaSalida);

                    var destino = BuscarAdelante(dias, acumulado, inicio, t.Importe, limite);

                    if (destino < 0 && permiteRetroceso)
                        destino = BuscarAtras(dias, acumulado, inicio - 1, t.Importe, limite, cerrados);

                    if (destino < 0)
                    {
                        resultado.SinUbicar.Add(t);
                        continue;
                    }

                    acumulado[destino] += t.Importe;
                    resultado.FechaPorTrayecto[t.TrayectoId] = dias[destino];
                }
            }

            return resultado;
        }

        /// <summary>Días del mes que admiten gasto, en orden. Domingos y feriados quedan fuera.</summary>
        private static List<DateOnly> DiasImputables(CalendarioNoLaborable calendario, int anio, int mes)
        {
            var primero = new DateOnly(anio, mes, 1);
            var ultimo  = primero.AddMonths(1).AddDays(-1);

            var dias = new List<DateOnly>(31);
            for (var d = primero; d <= ultimo; d = d.AddDays(1))
                if (calendario.EsImputable(d)) dias.Add(d);

            return dias;
        }

        /// <summary>
        /// Índice del primer día imputable que no es anterior a <paramref name="fecha"/>. Devuelve
        /// <c>dias.Count</c> si ya no queda ninguno en el mes.
        /// </summary>
        private static int PrimerIndiceDesde(List<DateOnly> dias, DateOnly fecha)
        {
            for (var i = 0; i < dias.Count; i++)
                if (dias[i] >= fecha) return i;

            return dias.Count;
        }

        /// <summary>
        /// Cabe si el día está vacío o si sumando el importe no pasa del tope. Lo de "vacío" no es
        /// un permiso para excederse: es para que un trayecto que por sí solo pasa del tope —data
        /// anterior a la regla— tenga dónde caer en vez de trabar la planilla entera. Como el día
        /// queda por encima del tope, ningún otro trayecto se le suma después.
        /// </summary>
        private static bool Cabe(decimal ocupado, decimal importe, decimal limite) =>
            ocupado == 0m || ocupado + importe <= limite;

        private static int BuscarAdelante(
            List<DateOnly> dias, decimal[] acumulado, int desde, decimal importe, decimal limite)
        {
            for (var i = Math.Max(0, desde); i < dias.Count; i++)
                if (Cabe(acumulado[i], importe, limite)) return i;

            return -1;
        }

        /// <summary>
        /// Último recurso cuando el mes se acabó hacia adelante. Solo se aceptan días cuyo periodo
        /// no haya sido rendido todavía: sobre lo ya rendido hay una planilla firmada que diría
        /// otro total.
        /// </summary>
        private static int BuscarAtras(
            List<DateOnly> dias, decimal[] acumulado, int desde, decimal importe, decimal limite,
            List<PeriodoRendido> cerrados)
        {
            for (var i = Math.Min(desde, dias.Count - 1); i >= 0; i--)
            {
                if (YaRendido(dias[i], cerrados)) continue;
                if (Cabe(acumulado[i], importe, limite)) return i;
            }

            return -1;
        }

        private static bool YaRendido(DateOnly fecha, List<PeriodoRendido> cerrados)
        {
            foreach (var p in cerrados)
                if (fecha >= p.Desde && fecha <= p.Hasta) return true;

            return false;
        }
    }
}
