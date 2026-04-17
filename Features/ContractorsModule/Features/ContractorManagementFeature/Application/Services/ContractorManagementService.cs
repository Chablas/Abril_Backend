using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.Contractors.ContractorManagement.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorManagement.Application.Interfaces;
using Abril_Backend.Features.Contractors.ContractorManagement.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Contractors.ContractorManagement.Application.Services
{
    public class ContractorManagementService : IContractorManagementService
    {
        private readonly IContractorManagementRepository _repository;

        public ContractorManagementService(IContractorManagementRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<CompanyPagedDto>> GetPaged(CompanyFilterDto filter)
        {
            return await _repository.GetPaged(filter);
        }

        public async Task Approve(int contractorId, int userId)
        {
            await _repository.Approve(contractorId, userId);
        }

        public async Task Reject(int contractorId, int userId)
        {
            await _repository.Reject(contractorId, userId);
        }
    }
}
