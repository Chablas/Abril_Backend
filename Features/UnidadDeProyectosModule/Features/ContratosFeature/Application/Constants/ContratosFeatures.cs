namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Constants
{
    /// <summary>featureKeys de Contratos (tabla <c>feature</c>), atados al módulo "Proyectos"
    /// (module_id=6) en el sidebar. Los role_id que tienen cada uno se deciden por feature y no
    /// se nombran acá — pueden diferir entre entornos (ver mismo criterio en CronogramaHitosFeatures).</summary>
    public static class ContratosFeatures
    {
        /// <summary>Entrar y ver: listado, detalle, hitos de pago, descargar el contrato generado.
        /// Sin Editar, la pantalla es de solo lectura.</summary>
        public const string Ver = "unidad-de-proyectos.contratos";

        /// <summary>Crear/editar contratos e hitos, generar el documento, avanzar los pasos 4-9,
        /// y configurar la carpeta de SharePoint del proyecto.</summary>
        public const string Editar = "unidad-de-proyectos.contratos.editar";
    }
}
