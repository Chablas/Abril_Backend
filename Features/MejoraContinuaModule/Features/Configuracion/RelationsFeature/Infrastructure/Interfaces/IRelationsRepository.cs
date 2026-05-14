using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Application.Dtos;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Infrastructure.Interfaces
{
    public interface IRelationsRepository
    {
        Task<object> GetPagedAsync(int page, int? phaseId, int? stageId, int? layerId, int? subStageId, int? subSpecialtyId, int? partidaId);
        Task<RelationFiltersDTO> GetFiltersAsync();
        Task<object?> CreateAsync(CreateRelationDTO dto, int userId);
        Task<bool> DeleteAsync(int id, int userId);
    }
}
