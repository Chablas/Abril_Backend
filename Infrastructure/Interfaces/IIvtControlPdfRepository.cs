namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IIvtControlPdfRepository
    {
        Task<bool> Create(int scheduleId, string fileUrl, int userId);
    }
}