using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Application.Dtos;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Infrastructure.Interfaces
{
    public interface ILessonAreaRepository
    {
        Task<List<LessonAreaConfigItemDTO>> GetAllAsync();
        /// <summary>Devuelve solo lesson_areas activos que tengan al menos un scope_item configurado.</summary>
        Task<List<LessonAreaConfigItemDTO>> GetAllWithScopeAsync();
        Task<ToggleLessonAreaResultDTO> ToggleAsync(int areaScopeId);
        Task<SetLessonAreaFlagResultDTO> SetIncludeInFormAsync(int areaScopeId, bool value);
        Task<SetLessonAreaFlagResultDTO> SetIncludeDescendantsAsync(int areaScopeId, bool value);
    }
}
