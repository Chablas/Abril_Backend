using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces
{
    public interface IWorkerSearchService
    {
        Task<List<WorkerSearchResultDto>> Search(string? q, int limit);
    }
}
