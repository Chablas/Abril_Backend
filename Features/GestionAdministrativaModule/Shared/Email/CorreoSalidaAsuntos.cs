using Abril_Backend.Features.GestionAdministrativa.Shared.Services;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Email
{
    /// <summary>
    /// Los asuntos de los correos de Solicitud de Salidas, en un solo lugar: los arma el envío, y la
    /// Configuración los muestra bajo el nombre de cada correo para buscarlos en Enviados.
    /// </summary>
    public static class CorreoSalidaAsuntos
    {
        public static string Revisor(string codigo, string solicitante, DateOnly fechaSalida) =>
            Revisor(codigo, solicitante, Fecha(fechaSalida));

        public static string RevisorJefeArea(string codigo, string solicitante, DateOnly fechaSalida) =>
            RevisorJefeArea(codigo, solicitante, Fecha(fechaSalida));

        public static string Confirmacion(string codigo, DateOnly fechaSalida) =>
            Confirmacion(codigo, Fecha(fechaSalida));

        /// <param name="periodo">El mes que se rinde, en palabras («septiembre 2026»).</param>
        public static string RecordatorioApertura(string periodo) =>
            $"Ya puedes rendir tus movilidades de {periodo}";

        /// <param name="periodo">El mes que se rinde, en palabras («septiembre 2026»).</param>
        public static string RecordatorioCierre(string periodo) =>
            $"Hoy vence el plazo para rendir tus movilidades de {periodo}";

        /// <summary>
        /// El asunto como lo muestra la Configuración: lo que cambia en cada envío va entre llaves
        /// («{código}»). Null = ese correo todavía no tiene su asunto acá.
        /// </summary>
        public static string? Plantilla(string codigo) => codigo switch
        {
            CorreoEventoCodigos.Revisor => Revisor("{código}", "{solicitante}", "{fecha de salida}"),
            CorreoEventoCodigos.RevisorJefeArea => RevisorJefeArea("{código}", "{solicitante}", "{fecha de salida}"),
            CorreoEventoCodigos.Confirmacion => Confirmacion("{código}", "{fecha de salida}"),
            CorreoEventoCodigos.RecordatorioRendicionApertura => RecordatorioApertura("{mes}"),
            CorreoEventoCodigos.RecordatorioRendicionCierre => RecordatorioCierre("{mes}"),
            _ => null,
        };

        private static string Revisor(string codigo, string solicitante, string fecha) =>
            $"Solicitud de salida {codigo} - {solicitante} - {fecha}";

        private static string RevisorJefeArea(string codigo, string solicitante, string fecha) =>
            $"Salida registrada en tu área - {codigo} - {solicitante} - {fecha}";

        private static string Confirmacion(string codigo, string fecha) =>
            $"Tu solicitud de salida {codigo} está en revisión - {fecha}";

        // Como lo escribía el envío ($"{fecha:dd/MM/yyyy}"): con la cultura del servidor.
        private static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy");
    }
}
