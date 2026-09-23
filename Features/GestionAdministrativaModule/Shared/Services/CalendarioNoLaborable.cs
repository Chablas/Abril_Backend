using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Feriados y días no laborables (Configuración → Feriados) resueltos en memoria, para calcular
    /// el plazo de rendición de las salidas.
    ///
    /// Se carga UNA vez por listado y responde por cualquier mes: un listado toca varios meses
    /// distintos (cada solicitud tiene el suyo) y preguntar mes por mes contra la base sería un
    /// N+1 sobre una tabla que entera cabe en memoria.
    ///
    /// Mismo criterio de resolución que Lecciones Aprendidas
    /// (<c>LessonRepository.GetHolidayDatesAsync</c>): solo filas vigentes (<c>state</c>) y activas,
    /// y las marcadas como <c>recurring_yearly</c> aplican todos los años por mes/día.
    /// </summary>
    public sealed class CalendarioNoLaborable
    {
        /// <summary>
        /// Días hábiles de plazo con los que se responde si <c>ga_rendicion_config</c> está vacía
        /// (base sin sembrar). Es el valor con el que nació la regla, antes de que fuera
        /// configurable: así una base a medio migrar no abre el plazo de par en par ni lo cierra.
        /// </summary>
        public const int DiasHabilesDePlazoPorDefecto = 7;

        /// <summary>
        /// Alcance de la ventana con el que se responde si no hay fila: solo el mes anterior, que
        /// es como funcionó siempre.
        /// </summary>
        private const int AlcancePlazoPorDefecto = 1;
        private const string AlcancePlazoNombrePorDefecto = "Hasta el mes anterior";

        /// <summary>Fechas concretas (no recurrentes), tal cual están registradas.</summary>
        private readonly HashSet<DateOnly> _fijos;

        /// <summary>(mes, día) de los feriados que se repiten todos los años.</summary>
        private readonly HashSet<(int Mes, int Dia)> _recurrentes;

        private CalendarioNoLaborable(
            HashSet<DateOnly> fijos, HashSet<(int, int)> recurrentes,
            int diasDePlazo, decimal limiteMovilidad,
            int alcancePlazoId, int alcancePlazoMeses, string alcancePlazoNombre,
            int? alcancePermanenteId, int? alcancePermanenteMeses, string? alcancePermanenteNombre)
        {
            _fijos                  = fijos;
            _recurrentes            = recurrentes;
            DiasHabilesDePlazo      = diasDePlazo;
            LimiteMovilidad         = limiteMovilidad;
            AlcancePlazoId          = alcancePlazoId;
            AlcancePlazoMeses       = alcancePlazoMeses;
            AlcancePlazoNombre      = alcancePlazoNombre;
            AlcancePermanenteId     = alcancePermanenteId;
            AlcancePermanenteMeses  = alcancePermanenteMeses;
            AlcancePermanenteNombre = alcancePermanenteNombre;
        }

        /// <summary>
        /// Días hábiles que dura la ventana para rendir un mes, contados sobre el mes siguiente.
        /// Sale de <c>ga_rendicion_config</c> (Solicitud de Salidas → Configuración → Días
        /// reembolsables), así que es un dato del calendario cargado y no una constante.
        /// </summary>
        public int DiasHabilesDePlazo { get; }

        /// <summary>
        /// La ventana escrita como lo dicen los mensajes y los tooltips: "5.º día hábil del mes
        /// siguiente". Está acá para que el número no se vuelva a escribir a mano en cada texto.
        /// </summary>
        public string DiasHabilesDePlazoTexto => $"{DiasHabilesDePlazo}.º día hábil del mes siguiente";

        /// <summary>
        /// Cuántos meses hacia atrás abre la ventana de los días hábiles
        /// (<c>ga_rendicion_config.alcance_plazo_id</c> → <c>ga_rendicion_alcance</c>). 1 = solo el
        /// mes anterior, que es lo de siempre. Vale únicamente DENTRO de la ventana: cerrada, no
        /// alcanza ningún mes pasado por esta vía.
        /// </summary>
        public int AlcancePlazoMeses { get; }

        /// <summary>Cómo se llama ese alcance en el catálogo ("Hasta 6 meses atrás").</summary>
        public string AlcancePlazoNombre { get; }

        /// <summary>Fila del catálogo elegida, para que la pantalla de configuración la marque.</summary>
        public int AlcancePlazoId { get; }

        /// <summary>
        /// Cuántos meses hacia atrás se puede rendir en cualquier momento del mes, con la ventana
        /// abierta o cerrada (<c>ga_rendicion_config.alcance_permanente_id</c>). null = no aplica.
        ///
        /// Cuando tiene valor MANDA sobre <see cref="AlcancePlazoMeses"/>: es el interruptor para
        /// dejar rendir lo atrasado mientras se capacita a los trabajadores, y se apaga volviéndolo
        /// a dejar vacío.
        /// </summary>
        public int? AlcancePermanenteMeses { get; }

        /// <summary>Cómo se llama ese alcance en el catálogo; null si no hay alcance permanente.</summary>
        public string? AlcancePermanenteNombre { get; }

        /// <summary>Fila del catálogo elegida, o null si no hay alcance permanente.</summary>
        public int? AlcancePermanenteId { get; }

        /// <summary>
        /// Cómo se explica en un mensaje el límite que devolvió <see cref="LimiteDeRendicion"/>.
        /// No siempre lo pone la ventana: con alcance permanente el plazo de días hábiles no
        /// interviene, y repetir ahí "N.º día hábil del mes siguiente" sería mentir.
        /// </summary>
        public string TextoDelLimite =>
            AlcancePermanenteNombre is { Length: > 0 } permanente ? Minuscula(permanente)
            : AlcancePlazoMeses <= 1                              ? DiasHabilesDePlazoTexto
            : $"{DiasHabilesDePlazoTexto}, {Minuscula(AlcancePlazoNombre)}";

        /// <summary>
        /// Tope de movilidad en soles (<c>ga_rendicion_config.limite_diario_movilidad</c>). Viaja
        /// con el calendario porque sale de la MISMA fila que el plazo, y quien imputa las fechas
        /// de la planilla necesita las dos cosas a la vez: el tope dice cuánto entra en un día y
        /// el calendario, cuáles son esos días. Ver <see cref="TopeMovilidad"/>.
        /// </summary>
        public decimal LimiteMovilidad { get; }

        public static async Task<CalendarioNoLaborable> CargarAsync(AppDbContext ctx)
        {
            var dias = await ctx.Holiday
                .Where(h => h.State && h.Active)
                .Select(h => new { h.HolidayDate, h.RecurringYearly })
                .ToListAsync();

            // Plazo, alcances y tope salen de la misma fila y en el mismo viaje: son columnas de
            // ga_rendicion_config y pedirlas por separado serían varias consultas por lo mismo. Los
            // meses de cada alcance se traen con dos subconsultas al catálogo, para que agregar una
            // opción nueva sea agregar una fila y no tocar código.
            var config = await ctx.GaRendicionConfig
                .Where(c => c.State)
                .OrderBy(c => c.Id)
                .Select(c => new
                {
                    c.DiasHabilesPlazo,
                    c.LimiteDiarioMovilidad,
                    c.AlcancePlazoId,
                    c.AlcancePermanenteId,
                    Plazo = ctx.GaRendicionAlcance
                        .Where(a => a.GaRendicionAlcanceId == c.AlcancePlazoId && a.State)
                        .Select(a => new { a.MesesAtras, a.Nombre })
                        .FirstOrDefault(),
                    Permanente = ctx.GaRendicionAlcance
                        .Where(a => a.GaRendicionAlcanceId == c.AlcancePermanenteId && a.State)
                        .Select(a => new { a.MesesAtras, a.Nombre })
                        .FirstOrDefault(),
                })
                .FirstOrDefaultAsync();

            var fijos       = new HashSet<DateOnly>();
            var recurrentes = new HashSet<(int, int)>();
            foreach (var d in dias)
            {
                if (d.RecurringYearly) recurrentes.Add((d.HolidayDate.Month, d.HolidayDate.Day));
                else                   fijos.Add(d.HolidayDate);
            }

            var permanente = config?.Permanente;

            return new CalendarioNoLaborable(
                fijos, recurrentes,
                Acotar(config?.DiasHabilesPlazo ?? DiasHabilesDePlazoPorDefecto),
                TopeMovilidad.Acotar(config?.LimiteDiarioMovilidad),
                config?.AlcancePlazoId ?? RendicionAlcanceIds.MesAnterior,
                // El catálogo arranca en 1 (CHECK), pero una fila torcida a mano no puede dejar el
                // alcance en 0: eso cerraría hasta el mes anterior sin que nadie lo haya pedido.
                Math.Max(1, config?.Plazo?.MesesAtras ?? AlcancePlazoPorDefecto),
                config?.Plazo?.Nombre ?? AlcancePlazoNombrePorDefecto,
                // El id solo cuenta si el catálogo lo resolvió: una fila apuntando a una opción
                // dada de baja no puede dejar el alcance permanente prendido a medias.
                permanente == null ? null : config?.AlcancePermanenteId,
                permanente == null ? null : Math.Max(1, permanente.MesesAtras),
                permanente?.Nombre);
        }

        /// <summary>
        /// Plazo configurado (fila única de <c>ga_rendicion_config</c>). Se expone aparte porque la
        /// pantalla de configuración lo necesita solo, sin cargar los feriados.
        /// </summary>
        public static async Task<int> LeerDiasDePlazoAsync(AppDbContext ctx)
        {
            var configurado = await ctx.GaRendicionConfig
                .Where(c => c.State)
                .OrderBy(c => c.Id)
                .Select(c => (int?)c.DiasHabilesPlazo)
                .FirstOrDefaultAsync();

            return Acotar(configurado ?? DiasHabilesDePlazoPorDefecto);
        }

        /// <summary>
        /// Deja el plazo dentro del rango que valida la base (CHECK de 1 a 28). Una fila torcida por
        /// SQL a mano no puede hacer que <see cref="LimiteDeRendicion"/> devuelva otro mes.
        /// </summary>
        public static int Acotar(int dias) =>
            Math.Clamp(dias, GaRendicionConfig.DiasMinimo, GaRendicionConfig.DiasMaximo);

        /// <summary>Sábado, domingo, feriado o día no laborable registrado.</summary>
        public bool EsNoLaborable(DateOnly fecha)
        {
            if (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return true;
            if (_fijos.Contains(fecha)) return true;
            return _recurrentes.Contains((fecha.Month, fecha.Day));
        }

        /// <summary>
        /// Días feriados o registrados como no laborables, SIN contar el fin de semana.
        /// </summary>
        private bool EsFeriado(DateOnly fecha) =>
            _fijos.Contains(fecha) || _recurrentes.Contains((fecha.Month, fecha.Day));

        /// <summary>
        /// Días en los que la planilla de movilidad puede imputar un gasto: todos menos domingos y
        /// feriados. **El sábado sí cuenta**, y por eso esto no es el complemento de
        /// <see cref="EsNoLaborable"/>: el plazo de rendición se cuenta en días hábiles de verdad
        /// (lunes a viernes) y la imputación del gasto no.
        /// </summary>
        public bool EsImputable(DateOnly fecha) =>
            fecha.DayOfWeek != DayOfWeek.Sunday && !EsFeriado(fecha);

        /// <summary>
        /// Cuántos días del mes admiten gasto de movilidad. Multiplicado por el tope diario da lo
        /// máximo que un trabajador puede llegar a rendir en ese mes: más que eso no entra en la
        /// planilla ni desplazando, porque no quedan días donde ponerlo.
        /// </summary>
        public int DiasImputablesDelMes(int anio, int mes)
        {
            var primero = new DateOnly(anio, mes, 1);
            var ultimo  = primero.AddMonths(1).AddDays(-1);

            var total = 0;
            for (var d = primero; d <= ultimo; d = d.AddDays(1))
                if (EsImputable(d)) total++;

            return total;
        }

        /// <summary>
        /// Cierre de la ventana de rendición de <paramref name="anio"/>/<paramref name="mes"/>: el
        /// <see cref="DiasHabilesDePlazo"/>.º día hábil del mes SIGUIENTE. Es la ventana a secas,
        /// sin mirar el alcance, y por eso la usan los recordatorios: son avisos mensuales de que
        /// la ventana se abre y se cierra, y tienen que caer todos los meses aunque el alcance
        /// configurado deje rendir meses más viejos.
        ///
        /// Para saber hasta cuándo se puede rendir un mes de verdad, ver
        /// <see cref="LimiteDeRendicion"/>.
        /// </summary>
        public DateOnly FinDeVentana(int anio, int mes) =>
            NEsimoDiaHabil(new DateOnly(anio, mes, 1).AddMonths(1));

        /// <summary>
        /// Último día en que se pueden rendir las salidas de <paramref name="anio"/>/<paramref name="mes"/>
        /// con la configuración de hoy: hasta esa fecha se rinde y pasada queda cerrado
        /// (<see cref="PlazoVencido"/>).
        ///
        /// Dos reglas, y la primera gana:
        ///  • Con alcance permanente de N meses, el mes se rinde CUALQUIER día hasta que termine el
        ///    mes N.º posterior — la ventana de días hábiles no pinta nada.
        ///  • Sin alcance permanente solo se rinde dentro de la ventana, y la ventana llega
        ///    <see cref="AlcancePlazoMeses"/> meses hacia atrás. Se devuelve el cierre de la
        ///    ventana que todavía tiene ese mes a tiro —la de este mes si entra, la última que lo
        ///    tuvo si ya no— para que "hoy &gt; límite" siga contestando bien.
        ///
        /// Con el alcance por defecto —1 mes— esto da exactamente lo de siempre: el
        /// <see cref="DiasHabilesDePlazo"/>.º día hábil del mes siguiente.
        /// </summary>
        public DateOnly LimiteDeRendicion(int anio, int mes)
        {
            var primero = new DateOnly(anio, mes, 1);

            if (AlcancePermanenteMeses is int permanente)
                return primero.AddMonths(permanente + 1).AddDays(-1);

            // El mes en curso (y cualquiera futuro) no lleva meses de atraso: su límite es el
            // cierre de su propia ventana, como siempre.
            var offset = Math.Clamp(MesesAtras(anio, mes), 1, AlcancePlazoMeses);
            return NEsimoDiaHabil(primero.AddMonths(offset));
        }

        /// <summary>
        /// El mes más viejo que HOY se puede rendir, con la configuración de hoy. Es el otro lado de
        /// <see cref="LimiteDeRendicion"/> —ese contesta "hasta cuándo" para un mes dado, este
        /// contesta "desde qué mes" para el día de hoy— y es lo que muestra la pantalla de
        /// configuración: el efecto de lo configurado se ve mirando hacia atrás, no hacia adelante.
        ///
        /// Sin alcance permanente y con la ventana ya cerrada no alcanza ningún mes pasado, así que
        /// devuelve el mes en curso.
        /// </summary>
        public (int Anio, int Mes) MesMasAntiguoRendible()
        {
            var hoy      = MesAnteriorPeru.HoyPeru();
            var enCurso  = new DateOnly(hoy.Year, hoy.Month, 1);
            var anterior = enCurso.AddMonths(-1);

            var meses = AlcancePermanenteMeses
                ?? (hoy <= FinDeVentana(anterior.Year, anterior.Month) ? AlcancePlazoMeses : 0);

            var desde = enCurso.AddMonths(-meses);
            return (desde.Year, desde.Month);
        }

        /// <summary>
        /// true si las salidas de ese mes ya no se pueden rendir hoy. "Hoy" se toma en hora de Perú
        /// y no la del servidor, que corre en UTC: el día del vencimiento, pasadas las 19:00 de
        /// Lima el UTC ya está en el día siguiente y el plazo se cerraría antes de tiempo.
        /// </summary>
        public bool PlazoVencido(int anio, int mes)
            => MesAnteriorPeru.HoyPeru() > LimiteDeRendicion(anio, mes);

        /// <summary>
        /// El <see cref="DiasHabilesDePlazo"/>.º día hábil del mes que empieza en
        /// <paramref name="primeroDelMes"/>. Si ese mes no llegara a tener tantos días hábiles
        /// (caso teórico), devuelve su último día: nunca una fecha de otro mes.
        /// </summary>
        private DateOnly NEsimoDiaHabil(DateOnly primeroDelMes)
        {
            var ultimo = primeroDelMes.AddMonths(1).AddDays(-1);

            var habiles = 0;
            for (var d = primeroDelMes; d <= ultimo; d = d.AddDays(1))
            {
                if (EsNoLaborable(d)) continue;
                if (++habiles == DiasHabilesDePlazo) return d;
            }
            return ultimo;
        }

        /// <summary>
        /// Cuántos meses de atraso lleva ese mes respecto del mes en curso (en hora de Perú):
        /// 0 = el mes en curso, 1 = el mes anterior. Negativo para meses futuros.
        /// </summary>
        private static int MesesAtras(int anio, int mes)
        {
            var hoy = MesAnteriorPeru.HoyPeru();
            return (hoy.Year * 12 + hoy.Month) - (anio * 12 + mes);
        }

        /// <summary>El nombre del catálogo metido en medio de una frase: "Hasta 6…" → "hasta 6…".</summary>
        private static string Minuscula(string texto) =>
            texto.Length == 0 ? texto : char.ToLowerInvariant(texto[0]) + texto[1..];
    }
}
