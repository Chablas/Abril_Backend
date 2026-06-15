using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Dtos;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Interfaces
{
    public interface IWorkItemService
    {
        Task<PagedResult<WorkItemDto>> GetPaged(WorkItemFilterDto filter);
        Task<WorkItemFormDataDto> GetFormData();
        Task Create(WorkItemCreateDto dto, int userId);
        Task Update(WorkItemEditDto dto, int userId);
        Task<bool> Delete(int workItemId, int userId);
        /// <summary>
        /// Recorre las carpetas de adjudicaciones de cada proyecto configurado, ubica la carpeta
        /// "Contratos", lee sus carpetas de especialidad y, dentro de cada una, las carpetas de
        /// partida; inserta en la BD las partidas no registradas, asignándoles la especialidad
        /// correspondiente cuando la carpeta de especialidad coincide con una registrada.
        /// </summary>
        Task<WorkItemSyncResultDto> SyncPartidasAsync(int userId);
    }
}
