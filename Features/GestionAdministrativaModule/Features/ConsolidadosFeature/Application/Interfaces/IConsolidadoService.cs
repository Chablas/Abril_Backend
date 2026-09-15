using Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Interfaces
{
    /// <summary>
    /// "Consolidados": los Consolidados del S10 del alcance del usuario y la decisión del reembolso
    /// sobre cada uno (aprobar —que ES firmar— u observar).
    ///
    /// Se separó de Gestión de Rendiciones porque lo que se decide acá es el DOCUMENTO del S10, que
    /// puede cubrir varias planillas a la vez: decidirlo planilla por planilla dejaba al trabajador
    /// con un reembolso partido y al jefe firmando el mismo PDF varias veces.
    /// </summary>
    public interface IConsolidadoService
    {
        Task<ConsolidadoListResultDto> GetAll(ConsolidadoFiltersDto filters);

        Task<ConsolidadoFilterDataDto> GetFilterData(ConsolidadoFiltersDto scope);

        Task<ConsolidadoDetalleDto> GetDetalle(int consolidadoId, ConsolidadoFiltersDto scope);

        /// <summary>
        /// Qué correos saldrían al decidir el reembolso de esa selección y a quién. Lo resuelve con
        /// las mismas llamadas que hace el envío, así que la confirmación no puede prometer un
        /// correo que Configuración → Correos dejó fuera.
        /// </summary>
        Task<List<CorreoAvisoPreviewDto>> GetCorreoPreview(
            ConsolidadoCorreoPreviewRequestDto request, ConsolidadoFiltersDto scope);

        /// <summary>
        /// Decide el reembolso de los consolidados seleccionados. Aprobar ES firmar: estampa la
        /// firma del revisor en la planilla y en el consolidado, deja las salidas en "Firmado" y
        /// avisa a Tesorería.
        /// </summary>
        Task<ReembolsoBulkResultDto> DecidirReembolso(
            ConsolidadoAccionDto accion, bool aprobar, ConsolidadoFiltersDto scope, int reviewerUserId);
    }
}
