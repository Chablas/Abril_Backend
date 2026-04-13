using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos;

namespace Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Interfaces
{
    public interface IContractorRegistrationRepository
    {
        Task Create(CompanyCreateDto dto, int? userId);
    }
}
