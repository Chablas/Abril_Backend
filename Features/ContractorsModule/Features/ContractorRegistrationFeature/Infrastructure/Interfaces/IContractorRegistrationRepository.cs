using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos;

namespace Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Interfaces
{
    public interface IContractorRegistrationRepository
    {
        Task<List<ContractorPersonTypeDto>> GetPersonTypes();
        Task Create(ContributorCreateDto dto, int? userId, string? logoUrl, string? brochureUrl, string? fichaRucUrl, string? referencesUrl);
    }
}
