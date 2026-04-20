using Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos;
using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Interfaces
{
    public interface IProjectSubContractorRepository
    {
        Task Create(ProjectSubContractorCreateDTO dto, List<(string Url, string OriginalFileName)> quotationFiles, List<(string Url, string OriginalFileName)> comparativeFiles, int userId);
        Task<List<ContractSimpleDTO>> GetContractsFactory();
        Task<List<ContractTypeSimpleDTO>> GetContractTypeFactory();
        Task<List<ContractOriginSimpleDTO>> GetContractOriginFactory();
        Task<List<PaymentMethodSimpleDTO>> GetPaymentMethodFactory();
        Task<List<CurrencySimpleDTO>> GetCurrencyFactory();
        Task<List<WorkItemSimpleDTO>> GetWorkItemFactory();
        Task<List<WorkItemCategorySimpleDTO>> GetWorkItemCategoryFactory();
        Task<List<CompanyFactoryDTO>> GetCompanyFactory();
        Task<PagedResult<ProjectSubContractorDTO>> GetPaged(ProjectSubContractorFilterDTO filter);
        Task<AdjudicacionNotificationDataDto> GetNotificationData(int projectSubContractorId);
        Task UpdateStatusToSent(int projectSubContractorId, int userId);
        Task SaveDates(int projectSubContractorId, UpdateDatesDTO dto, int userId);
        Task<AdjudicacionPathDataDto> GetPathDataAsync(int projectSubContractorId);
        Task SaveDocumentAsync(int projectSubContractorId, AdjudicacionDocumentType documentType, string fileUrl, string originalFileName, int userId);
        Task<AdjudicacionSummarySheetDataDto> GetSummarySheetDataAsync(int projectSubContractorId);
    }
}