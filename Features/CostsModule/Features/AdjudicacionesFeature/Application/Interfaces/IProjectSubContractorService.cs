using Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos;
using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Interfaces
{
    public interface IProjectSubContractorService
    {
        Task Create(ProjectSubContractorCreateDTO dto, int page);
        Task<ProjectSubContractorFormDataDTO> GetFormData();
        Task<PagedResult<ProjectSubContractorDTO>> GetPaged(ProjectSubContractorFilterDTO filter);
        Task SendNotification(SendAdjudicacionNotificationDto dto, int userId);
        Task SaveDates(int projectSubContractorId, UpdateDatesDTO dto, int userId);
        Task<DocumentUploadResponseDto> UploadDocumentAsync(int projectSubContractorId, AdjudicacionDocumentType documentType, IFormFile file, int userId);
        Task<DocumentUploadResponseDto> GenerateDocumentAsync(int projectSubContractorId, AdjudicacionDocumentType documentType, int userId);
        Task UpdateDocumentStatusAsync(int projectSubContractorId, AdjudicacionDocumentType documentType, int? statusId, string? observation, int userId);
    }
}