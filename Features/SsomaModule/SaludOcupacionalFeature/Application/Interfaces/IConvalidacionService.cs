using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Convalidacion;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces
{
    public interface IConvalidacionService
    {
        Task<List<ConvalidacionListDto>> List(int? workerId);
        Task<int> Create(ConvalidacionCreateDto dto, int? userId);
        Task Update(int id, ConvalidacionUpdateDto dto, int? userId);
    }
}
