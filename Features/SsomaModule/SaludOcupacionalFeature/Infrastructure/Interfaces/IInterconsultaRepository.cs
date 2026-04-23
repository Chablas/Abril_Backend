using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Interconsulta;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces
{
    public interface IInterconsultaRepository
    {
        Task<List<InterconsultaListDto>> List(InterconsultaFilterDto filter);
        Task<int> Create(InterconsultaCreateDto dto, int? userId);
        Task Update(int id, InterconsultaUpdateDto dto, int? userId);
        Task UpdateResultado(int id, InterconsultaResultadoPatchDto dto, int? userId);
    }
}
