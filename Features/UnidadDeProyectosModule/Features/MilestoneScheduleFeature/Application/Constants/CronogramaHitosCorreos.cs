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
