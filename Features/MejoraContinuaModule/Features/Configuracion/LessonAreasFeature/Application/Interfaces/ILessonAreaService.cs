using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Application.Dtos;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Application.Interfaces
{
    public interface ILessonAreaService
    {
        Task<List<LessonAreaConfigItemDTO>> GetAllAsync();
        Task<List<LessonAreaConfigItemDTO>> GetAllWithScopeAsync();
        Task<ToggleLessonAreaResultDTO> ToggleAsync(int areaScopeId);
    }
}
