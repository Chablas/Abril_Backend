using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Application.Dtos;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Infrastructure.Interfaces
{
    public interface ILessonAreaRepository
    {
        Task<List<LessonAreaConfigItemDTO>> GetAllAsync();
        Task<ToggleLessonAreaResultDTO> ToggleAsync(int areaItemId);
    }
}
