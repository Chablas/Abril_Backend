using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.Contractors.ContractorManagement.Application.Dtos;

namespace Abril_Backend.Features.Contractors.ContractorManagement.Infrastructure.Interfaces
{
    public interface IContractorManagementRepository
    {
        Task<PagedResult<CompanyPagedDto>> GetPaged(CompanyFilterDto filter);
        Task Approve(int companyId, int userId);
        Task Reject(int companyId, int userId);
    }
}
