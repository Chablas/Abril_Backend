using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.RevisionDescansos;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces
{
    public interface IRevisionDescansosService
    {
        Task<RevisionDescansosInitDto> GetInit(RevisionDescansosFiltroDto filtro);
        Task<PagedResult<RevisionDescansoListItemDto>> ListPaged(RevisionDescansosFiltroDto filtro);
        Task<RevisionDescansoDetalleDto> GetDetalle(int id);
        Task<int> Aprobar(RevisionDescansosAprobarDto dto, int? userId);
        Task<int> Rechazar(RevisionDescansosRechazarDto dto, int? userId);
    }
}
