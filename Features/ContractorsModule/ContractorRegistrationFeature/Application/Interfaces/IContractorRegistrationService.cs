using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos;
using Abril_Backend.Shared.Services.Sunat.Dtos;

namespace Abril_Backend.Features.Contractors.ContractorRegistration.Application.Interfaces
{
    public interface IContractorRegistrationService
    {
        Task Create(CompanyCreateDto dto, int? userId);
        Task<SunatCompanyDto?> GetByRuc(string ruc);
    }
}
