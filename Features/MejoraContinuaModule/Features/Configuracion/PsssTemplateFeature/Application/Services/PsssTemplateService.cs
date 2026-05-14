using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Application.Interfaces;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Application.Services
{
    public class PsssTemplateService : IPsssTemplateService
    {
        private readonly IPsssTemplateRepository _repo;

        public PsssTemplateService(IPsssTemplateRepository repo)
        {
            _repo = repo;
        }

        public Task<PsssTemplatePagedDTO> GetPagedAsync(int page) => _repo.GetPagedAsync(page);
        public Task<List<PsssTemplateSimpleDTO>> GetAllSimpleAsync() => _repo.GetAllSimpleAsync();
        public Task<int> CreateAsync(PsssTemplateCreateDTO dto, int userId) => _repo.CreateAsync(dto, userId);
        public Task<bool> DeleteSoftAsync(int templateId, int userId) => _repo.DeleteSoftAsync(templateId, userId);
        public Task<List<int>> GetPsssIdsAsync(int templateId) => _repo.GetPsssIdsAsync(templateId);
        public Task UpdatePsssAsync(int templateId, List<int> psssIds) => _repo.UpdatePsssAsync(templateId, psssIds);
        public Task<List<PsssAllFlatDTO>> GetAllPsssFlat() => _repo.GetAllPsssFlat();
    }
}
