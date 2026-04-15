using Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos;
using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Interfaces
{
    public interface IProjectSubContractorRepository
    {
        Task Create(ProjectSubContractorCreateDTO dto, List<string> quotationFileUrls, List<string> comparativeFileUrls, int userId);
        Task<List<ContractSimpleDTO>> GetContractsFactory();
        Task<List<ContractTypeSimpleDTO>> GetContractTypeFactory();
        Task<List<ContractOriginSimpleDTO>> GetContractOriginFactory();
        Task<List<PaymentMethodSimpleDTO>> GetPaymentMethodFactory();
        Task<List<CurrencySimpleDTO>> GetCurrencyFactory();
        Task<List<WorkItemSimpleDTO>> GetWorkItemFactory();
        Task<List<CompanyFactoryDTO>> GetCompanyFactory();
        Task<PagedResult<ProjectSubContractorDTO>> GetPaged(ProjectSubContractorFilterDTO filter);
        Task<AdjudicacionNotificationDataDto> GetNotificationData(int projectSubContractorId);
        Task UpdateStatusToSent(int projectSubContractorId, int userId);
    }
}