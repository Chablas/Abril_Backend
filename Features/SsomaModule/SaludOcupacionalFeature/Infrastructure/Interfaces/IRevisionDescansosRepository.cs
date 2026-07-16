using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.RevisionDescansos;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces
{
    public interface IRevisionDescansosRepository
    {
        Task<RevisionDescansosInitDto> GetInit(RevisionDescansosFiltroDto filtro);
        Task<PagedResult<RevisionDescansoListItemDto>> ListPaged(RevisionDescansosFiltroDto filtro);
        Task<RevisionDescansoDetalleDto> GetDetalle(int id);
        Task<int> Aprobar(List<int> ids, int? userId);
        Task<int> Rechazar(List<int> ids, string motivoRechazo, int? userId);
    }
}
