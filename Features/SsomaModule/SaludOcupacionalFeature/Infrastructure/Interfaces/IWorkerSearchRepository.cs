using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces
{
    public interface IWorkerSearchRepository
    {
        Task<List<WorkerSearchResultDto>> Search(string? q, int limit);
    }
}
