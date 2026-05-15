using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.ScopeFeature.Application.Dtos;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.ScopeFeature.Infrastructure.Interfaces
{
    public interface IScopeRepository
    {
        Task<AreaSubareaDTO> GetOrCreateAreaSubareaAsync(int areaId, int? subAreaId);
        Task<List<ScopeItemDTO>> GetScopeTreeAsync(int areaSubareaId);
        Task UpsertScopeAsync(ScopeItemUpsertDTO dto);
        Task<List<ScopeTemplateDTO>> GetTemplatesAsync();
        Task CreateTemplateAsync(ScopeTemplateCreateDTO dto);
        Task UpdateTemplateAsync(ScopeTemplateUpdateDTO dto);
        Task DeleteTemplateAsync(int scopeTemplateId);
        /// <summary>Devuelve el árbol de scope items para crear una lección (AreaId + SubAreaId opcional).</summary>
        Task<List<ScopeItemDTO>> GetScopeForLessonAsync(int areaId, int? subAreaId);
    }
}
