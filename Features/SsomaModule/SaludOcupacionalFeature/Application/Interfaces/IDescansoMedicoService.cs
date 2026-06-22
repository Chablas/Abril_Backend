using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.DescansoMedico;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces
{
    public interface IDescansoMedicoService
    {
        Task<PagedResult<DescansoMedicoListItemDto>> ListPaged(DescansoMedicoFilterDto filter);
        Task<DescansoMedicoDetalleDto> GetById(int id);
        Task<int> Create(DescansoMedicoCreateDto dto, int? userId);
        Task Update(int id, DescansoMedicoUpdateDto dto);
        Task Aprobar(int id, DescansoAprobarDto dto, int? userId);
        Task Rechazar(int id, DescansoRechazarDto dto, int? userId);
        Task Delete(int id);
    }
}
