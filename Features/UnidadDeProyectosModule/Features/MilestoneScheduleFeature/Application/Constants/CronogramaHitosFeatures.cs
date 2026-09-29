namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Constants
{
    /// <summary>
    /// featureKeys del Cronograma de Hitos (tabla <c>feature</c>). Los permisos se deciden por
    /// feature y no por ID de rol: los roles que administran (COORDINADOR DE PROYECTOS, JEFE DE
    /// PROYECTOS, GERENTE INMOBILIARIO) no tienen el mismo role_id en dev y en prod, así que
    /// ninguna constante de <c>Roles</c> los nombra.
    /// </summary>
    public static class CronogramaHitosFeatures
    {
        /// <summary>Entrar y ver: listado, historial de versiones y Gantt. Sin ninguna otra
        /// feature, la pantalla es de solo lectura.</summary>
        public const string Ver = "mejora-continua.milestone-schedule";

        /// <summary>Residente: subir versiones nuevas y modificar el cronograma, solo en el
        /// proyecto donde es el residente de Emails SSOMA y teniendo el rol RESIDENTE.</summary>
        public const string Editar = "mejora-continua.milestone-schedule.editar";

        /// <summary>Supervisión, en cualquier proyecto: eliminar versiones, editar o agregar hitos
        /// a una versión guardada, culminar, marcar críticos, foto y característica. No sube
        /// versiones nuevas: eso es solo del residente del proyecto.</summary>
        public const string Administrar = "mejora-continua.milestone-schedule.administrar";
    }
}
