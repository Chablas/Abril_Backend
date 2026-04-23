using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Emo;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces
{
    public interface IEmoService
    {
        Task<PagedResult<EmoListItemDto>> ListPaged(EmoFilterDto filter);
        Task<EmoDetalleDto> GetById(int id);
        Task<WorkerEmoHistorialDto> GetHistorialByWorker(int workerId);
        Task<int> Create(EmoCreateDto dto, int? userId);
        Task Update(int id, EmoUpdateDto dto, int? userId);
        Task UpdateEstado(int id, string estado, int? userId);
    }
}
