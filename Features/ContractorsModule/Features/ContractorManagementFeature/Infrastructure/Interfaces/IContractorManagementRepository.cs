using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.Contractors.ContractorManagement.Application.Dtos;

namespace Abril_Backend.Features.Contractors.ContractorManagement.Infrastructure.Interfaces
{
    public interface IContractorManagementRepository
    {
        Task<PagedResult<ContributorPagedDto>> GetPaged(ContributorFilterDto filter);
        Task Approve(int contractorId, int userId);
        Task Reject(int contractorId, int userId);
        Task<ContractorWithEmailsDto?> GetWithEmails(int contractorId);
        Task SetActivationToken(int contractorId, string token, DateTime expiry);
    }
}
