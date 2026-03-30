using Abril_Backend.Application.DTOs;
namespace Abril_Backend.Infrastructure.Interfaces
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
        Task<List<CompanySimpleDTO>> GetCompanyFactory();
    }
}