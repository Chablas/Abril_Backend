using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Topico;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces
{
    public interface ITopicoService
    {
        Task<PagedResult<TopicoListItemDto>> ListPaged(TopicoFilterDto filter);
        Task<TopicoDetalleDto> GetById(int id);
        Task<int> Create(TopicoCreateDto dto, int? userId);
        Task Update(int id, TopicoUpdateDto dto);
        Task Delete(int id);
    }
}
