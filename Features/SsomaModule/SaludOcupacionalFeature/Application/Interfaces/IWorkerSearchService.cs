using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces
{
    public interface IWorkerSearchService
    {
        Task<List<WorkerSearchResultDto>> Search(string? q, int limit);
        Task<List<DocumentTypeDto>> GetDocumentTypes();
        Task<int> Create(WorkerCreateDto dto);
        Task Update(int id, WorkerUpdateDto dto);
        Task UpdateDatosBasicos(int id, WorkerDatosBasicosDto dto);
        Task Retirar(int id);
    }
}
