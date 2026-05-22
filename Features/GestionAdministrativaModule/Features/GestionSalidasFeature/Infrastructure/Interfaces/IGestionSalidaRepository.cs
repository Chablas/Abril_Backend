using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Interfaces
{
    public interface IGestionSalidaRepository
    {
        Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters);
        Task<GestionSalidaFilterDataDto> GetFilterData();
        Task Aprobar(int id, int reviewerUserId);
        Task Rechazar(int id, int reviewerUserId);

        /// <summary>Marca como rendidas todas las solicitudes elegibles (Aprobadas + No rendidas). Devuelve los IDs actualizados.</summary>
        Task<List<int>> MarcarRendidasBulk(IEnumerable<int> ids, int userId);

        /// <summary>Carga la info necesaria para armar la planilla de rendición de los ids dados.</summary>
        Task<List<RendicionItemDto>> GetRendicionData(List<int> ids);
    }
}
