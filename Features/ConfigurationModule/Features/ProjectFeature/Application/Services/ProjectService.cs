using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Sunat.Interfaces;

namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IProjectRepository _repository;
        private readonly ISunatService _sunatService;

        public ProjectService(IProjectRepository repository, ISunatService sunatService)
        {
            _repository = repository;
            _sunatService = sunatService;
        }

        public async Task<PagedResult<ProjectDto>> GetPaged(int page)
        {
            if (page < 1) page = 1;
            return await _repository.GetPaged(page);
        }

        public async Task Create(ProjectCreateDto dto, int userId)
        {
            await _repository.Create(dto, userId);
        }

        public async Task Update(ProjectEditDto dto, int userId)
        {
            await _repository.Update(dto, userId);
        }

        public async Task<bool> DeleteSoftAsync(int projectId, int userId)
        {
            return await _repository.DeleteSoftAsync(projectId, userId);
        }

        public async Task<CompanyLookupDto?> GetOrCreateCompanyByRuc(string ruc, int userId)
        {
            var trimmed = ruc.Trim();

            var existing = await _repository.FindCompanyByRuc(trimmed);
            if (existing != null)
            {
                return new CompanyLookupDto
                {
                    CompanyId = existing.CompanyId,
                    CompanyRuc = existing.CompanyRuc,
                    CompanyName = existing.CompanyName,
                    CompanyAddress = existing.CompanyAddress
                };
            }

            var sunat = await _sunatService.GetByRucAsync(trimmed);
            if (sunat == null)
                return null;

            var created = await _repository.CreateCompany(
                sunat.CompanyRuc,
                sunat.CompanyName,
                sunat.CompanyAddress,
                sunat.CompanyEconomicActivityDescription,
                userId);

            return new CompanyLookupDto
            {
                CompanyId = created.CompanyId,
                CompanyRuc = created.CompanyRuc,
                CompanyName = created.CompanyName,
                CompanyAddress = created.CompanyAddress
            };
        }
    }
}
