using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class WorkerSearchService : IWorkerSearchService
    {
        private const int LimitDefault = 20;
        private const int LimitMax = 100;

        private readonly IWorkerSearchRepository _repo;

        public WorkerSearchService(IWorkerSearchRepository repo)
        {
            _repo = repo;
        }

        public Task<List<WorkerSearchResultDto>> Search(string? q, int limit)
        {
            if (limit <= 0) limit = LimitDefault;
            if (limit > LimitMax) limit = LimitMax;
            return _repo.Search(q, limit);
        }
    }
}
