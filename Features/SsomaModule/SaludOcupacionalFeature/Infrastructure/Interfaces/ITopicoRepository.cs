using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Topico;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces
{
    public interface ITopicoRepository
    {
        Task<PagedResult<TopicoListItemDto>> ListPaged(TopicoFilterDto filter);
        Task<TopicoDetalleDto> GetById(int id);
        Task<int> Create(TopicoCreateDto dto, int registradoPorId);
        Task Update(int id, TopicoUpdateDto dto);
        Task Delete(int id);
    }
}
