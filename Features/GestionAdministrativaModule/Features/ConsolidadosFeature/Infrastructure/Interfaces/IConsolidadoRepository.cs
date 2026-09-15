using Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Consolidados.Infrastructure.Interfaces
{
    /// <summary>
    /// Los Consolidados del S10 del alcance del usuario y la escritura de la decisión del
    /// reembolso. Es el mismo recorte por área que el resto del módulo, solo que en el ámbito
    /// CONSOLIDADOS: ver las planillas en Gestión de Rendiciones y ver los consolidados acá son dos
    /// permisos distintos.
    /// </summary>
    public interface IConsolidadoRepository
    {
        Task<List<ConsolidadoListItemDto>> GetAll(ConsolidadoFiltersDto filters);

        Task<ConsolidadoDetalleDto?> GetDetalle(int consolidadoId, ConsolidadoFiltersDto scope);

        Task<ConsolidadoFilterDataDto> GetFilterData(ConsolidadoFiltersDto scope);

        /// <summary>
        /// Salidas del alcance del usuario cubiertas por esos consolidados. Un consolidado puede
        /// cubrir planillas de varias áreas, así que el recorte se aplica igual: mandar su id no
        /// alcanza salidas que el usuario no ve.
        /// </summary>
        Task<List<int>> ResolverSolicitudIds(IEnumerable<int> consolidadoIds, ConsolidadoFiltersDto scope);

        /// <summary>
        /// Observa el reembolso de esas salidas con el comentario de la jefatura. Devuelve las que
        /// cambiaron de estado.
        /// </summary>
        Task<List<int>> ObservarReembolso(IEnumerable<int> ids, string observacion, int reviewerUserId);

        /// <summary>
        /// De esas salidas, qué planillas se pueden aprobar hoy y qué PDF hay que firmar de cada
        /// una (el de la planilla y el del consolidado). Aplica los mismos guards que la escritura.
        /// </summary>
        Task<List<PlanillaParaFirmarDto>> GetPlanillasParaAprobarReembolso(
            IEnumerable<int> ids, int reviewerUserId);

        /// <summary>
        /// Escribe la aprobación una vez que las copias firmadas ya están en SharePoint: deja las
        /// salidas en "Firmado" y referencia los PDF firmados. Devuelve las salidas que cambiaron.
        /// </summary>
        Task<List<int>> AprobarReembolsoFirmado(
            IReadOnlyCollection<PlanillaFirmadaDto> planillas, int reviewerUserId);

        /// <summary>Correos de los dueños de las salidas de esos consolidados que están por decidir.</summary>
        Task<List<string>> GetCorreosSolicitantesPorDecidir(
            IEnumerable<int> consolidadoIds, ConsolidadoFiltersDto scope);

        /// <summary>Correos corporativos de quien tiene el rol TESORERO.</summary>
        Task<List<string>> GetCorreosTesoreria();

        /// <summary>Carpeta de SharePoint donde viven las planillas y sus copias firmadas.</summary>
        Task<string?> GetRendicionFolderUrl();

        /// <summary>Lo que necesita el correo de la decisión del reembolso de UNA salida.</summary>
        Task<ReembolsoCorreoInfoDto?> GetReembolsoCorreoInfo(int solicitudId);

        /// <summary>Lo que necesita el aviso a Tesorería de UNA planilla firmada.</summary>
        Task<TesoreriaCorreoInfoDto?> GetTesoreriaCorreoInfo(int rendicionId);
    }
}
