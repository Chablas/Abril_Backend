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

        public async Task<ContributorLookupDto?> GetOrCreateCompanyByRuc(string ruc, int userId)
        {
            var trimmed = ruc.Trim();

            var existing = await _repository.FindContributorByRuc(trimmed);
            if (existing != null)
            {
                return new ContributorLookupDto
                {
                    ContributorId = existing.ContributorId,
                    ContributorRuc = existing.ContributorRuc,
                    ContributorName = existing.ContributorName,
                    ContributorAddress = existing.ContributorAddress
                };
            }

            var sunat = await _sunatService.GetByRucAsync(trimmed);
            if (sunat == null)
                return null;

            var created = await _repository.CreateContributor(
                sunat.ContributorRuc,
                sunat.ContributorName,
                sunat.ContributorAddress,
                sunat.ContributorEconomicActivityDescription,
                userId);

            return new ContributorLookupDto
            {
                ContributorId = created.ContributorId,
                ContributorRuc = created.ContributorRuc,
                ContributorName = created.ContributorName,
                ContributorAddress = created.ContributorAddress
            };
        }
    }
}
