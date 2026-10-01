namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Constants
{
    /// <summary>
    /// Códigos de <c>milestone_schedule_correo</c>: los correos que manda el Cronograma de Hitos. Sus
    /// destinatarios se administran en Cronograma de Hitos → Configuración.
    /// </summary>
    public static class CronogramaHitosCorreos
    {
        /// <summary>Un residente subió una versión que cambia hitos o fechas (sección Correos).</summary>
        public const string VersionConCambios = "VERSION_CON_CAMBIOS";
        /// <summary>Últimos días hábiles del mes: al residente que no subió la versión del mes (Recordatorios).</summary>
        public const string CronogramaPendiente = "CRONOGRAMA_PENDIENTE";
        /// <summary>Día 1: las versiones que se subieron el mes anterior (Recordatorios).</summary>
        public const string ResumenMensual = "RESUMEN_MENSUAL";

        /// <summary>Los que se pueden enviar a mano desde la Configuración, simulando un día.</summary>
        public static readonly string[] Recordatorios = { CronogramaPendiente, ResumenMensual };
    }

    /// <summary>
    /// Los asuntos de los correos del cronograma, en un solo lugar: los usa el envío, y la
    /// Configuración los muestra bajo el nombre de cada correo para buscarlos en Enviados.
    /// </summary>
    public static class CronogramaHitosAsuntos
    {
        public const string VersionConCambios = "Cambios en el cronograma";

        public const string CronogramaPendiente =
            "🔔 Abril App Recordatorio: envío mensual de cronograma de hitos pendiente";

        /// <param name="periodo">El mes que cerró, en palabras («septiembre 2026»).</param>
        public static string ResumenMensual(string periodo) =>
            $"📊 Reporte mensual: cambios en cronogramas — {periodo}";

        /// <summary>
        /// El asunto como lo muestra la Configuración: lo que cambia en cada envío va entre llaves
        /// («{mes}»). Null = el correo no tiene asunto acá.
        /// </summary>
        public static string? Plantilla(string codigo) => codigo switch
        {
            CronogramaHitosCorreos.VersionConCambios => VersionConCambios,
            CronogramaHitosCorreos.CronogramaPendiente => CronogramaPendiente,
            CronogramaHitosCorreos.ResumenMensual => ResumenMensual("{mes}"),
            _ => null,
        };
    }

    /// <summary>Códigos de <c>milestone_schedule_correo_destinatario_tipo</c>.</summary>
    public static class CronogramaCorreoDestinatarioTipos
    {
        /// <summary>Un trabajador: su correo corporativo.</summary>
        public const string Trabajador = "TRABAJADOR";
        /// <summary>Un rol: los correos corporativos de quienes lo tengan el día del envío.</summary>
        public const string Rol = "ROL";
        /// <summary>Una dirección escrita a mano (p. ej. un buzón de grupo).</summary>
        public const string Correo = "CORREO";

        public static readonly string[] Todos = { Trabajador, Rol, Correo };
    }

    /// <summary>Códigos de <c>milestone_schedule_correo_recepcion</c>: cómo recibe el correo.</summary>
    public static class CronogramaCorreoRecepciones
    {
        public const string Para = "PARA";
        public const string Cc = "CC";
        /// <summary>Copia oculta.</summary>
        public const string Cco = "CCO";

        public static readonly string[] Todas = { Para, Cc, Cco };
    }
}
