using System.Globalization;
using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Email
{
    /// <summary>Una salida sin rendir tal como se imprime en la tabla del recordatorio.</summary>
    /// <param name="Codigo">SOL-AAAA-NNNN, o "#N" en las anteriores al código.</param>
    /// <param name="Origen">Origen del primer trayecto. Vacío si el motivo no pide lugares.</param>
    /// <param name="Destino">Destino del último trayecto: donde termina el recorrido.</param>
    /// <param name="TrayectosCount">Cuántos trayectos tiene la salida (1 en el caso normal).</param>
    public sealed record RecordatorioCorreoSalida(
        string Codigo, DateOnly FechaSalida, string Motivo,
        string Origen, string Destino, int TrayectosCount);

    /// <summary>
    /// Datos de los dos recordatorios del plazo. Los arma el servicio con lo que ya tiene en
    /// memoria: el correo no vuelve a la base a buscar nada.
    /// </summary>
    public sealed class RecordatorioCorreoDatos
    {
        public string Trabajador { get; set; } = string.Empty;

        /// <summary>Mes que hay que rendir, en palabras ("agosto 2026").</summary>
        public string Periodo { get; set; } = string.Empty;

        /// <summary>Último día apto para rendir ese periodo.</summary>
        public DateOnly LimiteRendicion { get; set; }

        /// <summary>Días hábiles que quedan hasta el límite, contando hoy.</summary>
        public int DiasHabilesRestantes { get; set; }

        public IReadOnlyList<RecordatorioCorreoSalida> Salidas { get; set; } =
            new List<RecordatorioCorreoSalida>();

        /// <summary>El periodo con la primera letra en mayúscula, para arrancar una frase.</summary>
        public string PeriodoTitulo =>
            string.IsNullOrEmpty(Periodo)
                ? Periodo
                : CultureInfo.GetCultureInfo("es-PE").TextInfo.ToTitleCase(Periodo);
    }

    /// <summary>
    /// Los dos recordatorios del plazo de rendición (RG-33 y RG-34), con el mismo chrome de la
    /// intranet (<see cref="SalidaEmailLayout"/>) que el resto de los correos de salidas:
    ///
    /// <list type="bullet">
    ///   <item>Apertura — el primer día hábil del mes: se abrió el plazo del mes anterior.</item>
    ///   <item>Cierre — el último día apto para rendir: hoy vence.</item>
    /// </list>
    ///
    /// Son los únicos correos del módulo que no los dispara nadie: salen porque llegó el día. Por
    /// eso los dos llevan la lista completa de lo que le falta al trabajador — no hay una acción
    /// previa suya que le recuerde de qué se trata.
    ///
    /// Criterio editorial heredado de <see cref="AbrilEmailLayout"/>: el correo lleva datos y un
    /// acceso, no explicaciones. La bajada es UNA línea y las franjas son de estado.
    /// </summary>
    public static class RecordatorioRendicionEmailTemplates
    {
        // Íconos del catálogo de public/images/emails/icons (los genera
        // Abril-Frontend/scripts/generate-email-icons.js). Se reutilizan los que ya existen, igual
        // que en el resto de los correos de salidas.
        private const string IconoApertura = "req-recordatorio";
        private const string IconoCierre   = "req-plazo";

        private const string IconoFranjaAviso = "req-aviso";
        private const string IconoFranjaPlazo = "req-plazo";

        private const string FilaTrabajador = "req-solicitante";
        private const string FilaPeriodo    = "req-fecha";
        private const string FilaPendientes = "req-solicitud";
        private const string FilaLimite     = "req-plazo";

        private const string SeccionSalidas = "req-lugar";

        private const string TextoBoton = "Rendir mis salidas";

        /// <summary>
        /// Primer día hábil del mes: se abrió el plazo para rendir el mes anterior (RG-33). La
        /// franja es informativa — recién empieza el plazo, todavía no apura a nadie.
        /// </summary>
        public static string Apertura(SalidaEmailLayout l, RecordatorioCorreoDatos d, string urlRendir) =>
            l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoApertura,
                    "Ya puedes rendir tus movilidades",
                    $"Se abrió el plazo para rendir las salidas de <b>{AbrilEmailLayout.Esc(d.Periodo)}</b>."),
                l.Franja(IconoFranjaAviso, AbrilEmailLayout.Tono.Info, PlazoTexto(d)),
                Detalle(l, d),
                l.Boton(TextoBoton, urlRendir),
                l.EnlaceDirecto(urlRendir));

        /// <summary>
        /// Último día apto para rendir: hoy vence el plazo (RG-34). La franja va en ámbar y no en
        /// rojo: el trabajador todavía puede resolverlo hoy mismo, que es justamente para lo que se
        /// le escribe.
        /// </summary>
        public static string Cierre(SalidaEmailLayout l, RecordatorioCorreoDatos d, string urlRendir) =>
            l.Documento(
                new AbrilEmailLayout.Cabecera(
                    IconoCierre,
                    "Hoy vence el plazo para rendir",
                    $"Es el último día para rendir las salidas de <b>{AbrilEmailLayout.Esc(d.Periodo)}</b>."),
                l.Franja(
                    IconoFranjaPlazo, AbrilEmailLayout.Tono.Ambar,
                    $"Después de hoy, {AbrilEmailLayout.Esc(d.Periodo)} queda cerrado y estas salidas "
                    + "ya no se podrán rendir."),
                Detalle(l, d),
                l.Boton(TextoBoton, urlRendir),
                l.EnlaceDirecto(urlRendir));

        // ── Bloques compartidos ───────────────────────────────────────────────

        /// <summary>
        /// El plazo dicho como lo lee el trabajador: la fecha límite y cuántos días hábiles le
        /// quedan. El día del vencimiento el conteo sobra —lo dice la cabecera—, así que ese caso
        /// lo resuelve la franja del cierre y este texto no se usa ahí.
        /// </summary>
        private static string PlazoTexto(RecordatorioCorreoDatos d)
        {
            var dias = d.DiasHabilesRestantes;
            var cuenta = dias <= 1
                ? "hoy es el último día hábil"
                : $"te quedan <b>{dias} días hábiles</b>";

            return $"Tienes hasta el <b>{d.LimiteRendicion:dd/MM/yyyy}</b> — {cuenta}.";
        }

        /// <summary>
        /// La tarjeta con el resumen y la tabla de las salidas que faltan. La tabla va siempre, aun
        /// con una sola fila: el trabajador tiene que saber CUÁLES son, y la tarjeta sola solo diría
        /// cuántas.
        /// </summary>
        private static string Detalle(SalidaEmailLayout l, RecordatorioCorreoDatos d)
        {
            var total = d.Salidas.Count;

            var filas = new List<AbrilEmailLayout.Fila>
            {
                new(FilaTrabajador, "Colaborador", AbrilEmailLayout.Esc(d.Trabajador)),
                new(FilaPeriodo, "Periodo", AbrilEmailLayout.Esc(d.PeriodoTitulo)),
                new(FilaPendientes, "Salidas sin rendir",
                    $"{total} salida{(total == 1 ? "" : "s")}"),
                new(FilaLimite, "Último día para rendir", $"{d.LimiteRendicion:dd/MM/yyyy}"),
            };

            // Los anchos suman el ancho interno de la tarjeta (580) para que las columnas no se
            // aprieten — ver la nota de Columna en AbrilEmailLayout.
            var columnas = new List<AbrilEmailLayout.Columna>
            {
                new("Código", 100),
                new("Fecha", 72, AbrilEmailLayout.Alineacion.Centro),
                new("Motivo", 150),
                new("Origen", 129),
                new("Destino", 129),
            };

            var cuerpo = d.Salidas
                .Select(s => (IReadOnlyList<AbrilEmailLayout.Celda>)new List<AbrilEmailLayout.Celda>
                {
                    new(AbrilEmailLayout.Esc(s.Codigo), Negrita: true, NoWrap: true),
                    new($"{s.FechaSalida:dd/MM/yyyy}", NoWrap: true),
                    new(Motivo(s)),
                    new(Guion(s.Origen)),
                    new(Guion(s.Destino)),
                })
                .ToList();

            return l.Tarjeta(filas)
                 + l.Seccion(SeccionSalidas, "Salidas sin rendir")
                 + l.Tabla(columnas, cuerpo);
        }

        /// <summary>
        /// El motivo del primer trayecto y, si hay más, cuántos siguen (§8.3: "Motivo principal
        /// (+N trayecto[s])"). El detalle completo está en la intranet, a un botón de distancia.
        /// </summary>
        private static string Motivo(RecordatorioCorreoSalida s)
        {
            var motivo = Guion(s.Motivo);
            if (s.TrayectosCount <= 1) return motivo;

            var extra = s.TrayectosCount - 1;
            return $"{motivo} <span style=\"color:#64748b\">(+{extra} trayecto{(extra == 1 ? "" : "s")})</span>";
        }

        /// <summary>En la tabla sí va un guión: una celda vacía se lee como un error de armado.</summary>
        private static string Guion(string? valor) =>
            string.IsNullOrWhiteSpace(valor) ? "—" : AbrilEmailLayout.Esc(valor);
    }
}
