using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Interfaces;
using Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Sunat.Dtos;
using Abril_Backend.Shared.Services.Sunat.Interfaces;

namespace Abril_Backend.Features.Contractors.ContractorRegistration.Application.Services
{
    public class ContractorRegistrationService : IContractorRegistrationService
    {
        private readonly IContractorRegistrationRepository _repository;
        private readonly ISunatService _sunatService;

        public ContractorRegistrationService(IContractorRegistrationRepository repository, ISunatService sunatService)
        {
            _repository = repository;
            _sunatService = sunatService;
        }

        public async Task Create(CompanyCreateDto dto, int? userId)
        {
            await _repository.Create(dto, userId);
        }

        public async Task<SunatCompanyDto?> GetByRuc(string ruc)
        {
            return await _sunatService.GetByRucAsync(ruc);
        }
    }
}
