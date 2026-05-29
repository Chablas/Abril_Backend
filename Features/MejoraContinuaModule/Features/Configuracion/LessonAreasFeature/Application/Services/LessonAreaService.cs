using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Application.Interfaces;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Application.Services
{
    public class LessonAreaService : ILessonAreaService
    {
        private readonly ILessonAreaRepository _repo;
        public LessonAreaService(ILessonAreaRepository repo) => _repo = repo;

        public Task<List<LessonAreaConfigItemDTO>> GetAllAsync() => _repo.GetAllAsync();
        public Task<List<LessonAreaConfigItemDTO>> GetAllWithScopeAsync() => _repo.GetAllWithScopeAsync();
        public Task<ToggleLessonAreaResultDTO> ToggleAsync(int areaScopeId) => _repo.ToggleAsync(areaScopeId);
    }
}
