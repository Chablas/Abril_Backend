using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IIvtControlPdfRepository
    {
        Task<bool> Create(int scheduleId, string fileUrl, int userId, string fileDescription);
        Task<PagedResult<IvtControlPdfGetDTO>> GetPaged(int page);
    }
}