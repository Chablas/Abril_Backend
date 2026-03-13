using Abril_Backend.Application.DTOs;
namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IResidentReportIncidenceRepository
    {
        Task<PagedResult<ResidentReportIncidenceDTO>> GetPaged(int page);
        Task Create(ResidentReportIncidenceCreateDTO dto, List<string> uploadedUrls, int userId);
    }
}