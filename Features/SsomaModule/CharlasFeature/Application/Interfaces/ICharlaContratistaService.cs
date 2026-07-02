using Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Interfaces;

public interface ICharlaContratistaService
{
    Task<List<CharlaContratistaPendienteDto>> GetPendientesAsync(int empresaId, DateOnly fecha);
    Task<List<CharlaContratistaDto>> GetHistorialAsync(int empresaId, int page, int pageSize);
    Task<CharlaContratistaDto> SubirAsync(int empresaId, CharlaContratistaUploadRequest req, int userId);

    /// <summary>Para SSOMA/admin: incumplimientos de una fecha (tareados que no subieron charla).</summary>
    Task<List<CharlaContratistaPendienteDto>> GetIncumplimientosAsync(DateOnly fecha, int? proyectoId);
}
