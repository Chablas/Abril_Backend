using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Application.Interfaces;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Application.Services
{
    public class RelationsService : IRelationsService
    {
        private readonly IRelationsRepository _repo;

        public RelationsService(IRelationsRepository repo)
        {
            _repo = repo;
        }

        public Task<object> GetPagedAsync(int page, int? phaseId, int? stageId, int? layerId, int? subStageId, int? subSpecialtyId, int? partidaId)
            => _repo.GetPagedAsync(page, phaseId, stageId, layerId, subStageId, subSpecialtyId, partidaId);

        public Task<RelationFiltersDTO> GetFiltersAsync()
            => _repo.GetFiltersAsync();

        public Task<object?> CreateAsync(CreateRelationDTO dto, int userId)
            => _repo.CreateAsync(dto, userId);

        public Task<bool> DeleteAsync(int id, int userId)
            => _repo.DeleteAsync(id, userId);
    }
}
