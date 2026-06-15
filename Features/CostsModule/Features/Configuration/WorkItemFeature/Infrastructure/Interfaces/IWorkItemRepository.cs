using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Dtos;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Infrastructure.Interfaces
{
    public interface IWorkItemRepository
    {
        Task<PagedResult<WorkItemDto>> GetPaged(WorkItemFilterDto filter);
        Task Create(WorkItemCreateDto dto, int userId);
        Task Update(WorkItemEditDto dto, int userId);
        Task<bool> Delete(int workItemId, int userId);

        // ── Soporte para edición + sincronización ──────────────────────────
        Task<List<WorkSpecialtyOptionDto>> GetActiveSpecialties();
        Task<List<AdjudicacionFolderRootDto>> GetActiveAdjudicacionFolderRoots();
        /// <summary>Partidas activas (state = true) con su especialidad actual, para dedup y relleno.</summary>
        Task<List<ExistingWorkItemDto>> GetActivePartidas();
        /// <summary>Inserta las partidas omitiendo cualquier duplicado; devuelve las que sí se crearon.</summary>
        Task<List<string>> BulkCreate(IEnumerable<(string Description, int? WorkSpecialtyId)> items, int userId);
        /// <summary>Asigna especialidad a partidas existentes (solo las indicadas); devuelve cuántas se actualizaron.</summary>
        Task<int> AssignSpecialties(IEnumerable<(int WorkItemId, int WorkSpecialtyId)> assignments, int userId);
    }
}
