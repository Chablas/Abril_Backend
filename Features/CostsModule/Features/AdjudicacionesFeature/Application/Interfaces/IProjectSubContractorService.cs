using Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos;
using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Interfaces
{
    public interface IProjectSubContractorService
    {
        Task Create(ProjectSubContractorCreateDTO dto, int page);
        Task<ProjectSubContractorFormDataDTO> GetFormData();
        Task<PagedResult<ProjectSubContractorDTO>> GetPaged(ProjectSubContractorFilterDTO filter);
        Task<ProjectSubContractorPagedWithFiltersDTO> GetPagedWithFilters(ProjectSubContractorFilterDTO filter);
        Task SendNotification(SendAdjudicacionNotificationDto dto, int userId);
        Task SaveDates(int projectSubContractorId, UpdateDatesDTO dto, int userId);
        Task<DocumentUploadResponseDto> UploadDocumentAsync(int projectSubContractorId, AdjudicacionDocumentType documentType, IFormFile file, int userId);
        Task<DocumentUploadResponseDto> GenerateDocumentAsync(int projectSubContractorId, AdjudicacionDocumentType documentType, int userId);
        Task UpdateDocumentStatusAsync(int projectSubContractorId, AdjudicacionDocumentType documentType, int? statusId, string? observation, int userId);
        Task UpdateStatusAsync(int projectSubContractorId, int statusId, int userId);
        Task AdvanceToStep4Async(int projectSubContractorId, int userId);
        Task SendScNotificationAsync(int projectSubContractorId, string graphAccessToken, IFormFile? file, int userId);
        Task SetArrivalOptionAsync(int projectSubContractorId, bool arrivedWithObservations, int userId);
        Task ConfirmStep5Async(int projectSubContractorId, bool arrivedWithObservations, string graphAccessToken, int userId);
        Task SendStep6NotificationAsync(int projectSubContractorId, int userId);
        Task SendStep8NotificationAsync(int projectSubContractorId, string graphAccessToken, int userId);
        Task<(byte[] Bytes, string FileUrl, string OriginalFileName)> GenerateContractPackageAsync(int projectSubContractorId, int userId);
        Task SendObservationEmailAsync(int projectSubContractorId, AdjudicacionDocumentType documentType, SendObservationEmailDto dto, int userId);
    }
}