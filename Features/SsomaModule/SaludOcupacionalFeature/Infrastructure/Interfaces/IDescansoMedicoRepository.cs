using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.DescansoMedico;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces
{
    public interface IDescansoMedicoRepository
    {
        Task<PagedResult<DescansoMedicoListItemDto>> ListPaged(DescansoMedicoFilterDto filter);
        Task<DescansoMedicoDetalleDto> GetById(int id);
        Task<int> Create(DescansoMedicoCreateDto dto, int registradoPorId, string? urlCertificado = null);
        Task Update(int id, DescansoMedicoUpdateDto dto);
        Task Aprobar(int id, DescansoAprobarDto dto, int? userId);
        Task Rechazar(int id, DescansoRechazarDto dto, int? userId);
        Task DarAlta(int id, DarAltaDto dto, int? userId);
        Task<List<DescansoSeguimientoDto>> GetSeguimientos(int descansoId);
        Task<int> CreateSeguimiento(int descansoId, DescansoSeguimientoCreateDto dto, int registradoPorId, string? rolUsuario);
        Task Delete(int id);
    }
}
