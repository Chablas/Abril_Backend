using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Application.Dtos;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Infrastructure.Interfaces
{
    public interface IPsssTemplateRepository
    {
        Task<PsssTemplatePagedDTO> GetPagedAsync(int page);
        Task<List<PsssTemplateSimpleDTO>> GetAllSimpleAsync();
        Task<int> CreateAsync(PsssTemplateCreateDTO dto, int userId);
        Task<bool> DeleteSoftAsync(int templateId, int userId);
        Task<List<int>> GetPsssIdsAsync(int templateId);
        Task UpdatePsssAsync(int templateId, List<int> psssIds);
        Task<List<PsssAllFlatDTO>> GetAllPsssFlat();
    }
}
