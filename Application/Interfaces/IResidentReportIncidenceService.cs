using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Application.Interfaces
{
    public interface IResidentReportIncidenceService
    {
        Task<PagedResult<ResidentReportIncidenceDTO>> GetPaged(int page);
        Task Create(ResidentReportIncidenceCreateDTO dto, int userId);
        Task CreateResponse(ResidentReportResponseCreateDTO dto, int userId);
        Task UpdateIncidenceState(UpdateIncidenceDTO incidenceId, int userId);
    }
}