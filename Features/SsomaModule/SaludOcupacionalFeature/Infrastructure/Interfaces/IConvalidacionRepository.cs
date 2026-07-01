using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Convalidacion;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces
{
    public interface IConvalidacionRepository
    {
        Task<PagedResponseDto<ConvalidacionListDto>> List(ConvalidacionFilterDto filter);
        Task<int> Create(ConvalidacionCreateDto dto, int? userId);
        Task Update(int id, ConvalidacionUpdateDto dto, int? userId);
    }
}
