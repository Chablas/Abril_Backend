using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Interfaces;

public interface IAmonestacionService
{
    Task<AmonestacionInitDto> GetInitAsync();
    Task<AmonestacionCreadaDto> CrearAsync(AmonestacionCreateRequest req, int userId);
    Task<AmonestacionPagedResult<AmonestacionListItemDto>> GetListAsync(AmonestacionListQuery q);
    Task<AmonestacionDetalleDto?> GetDetalleAsync(int id);
    Task<AmonestacionDashboardDto> GetDashboardAsync();
    Task<WorkerPuntajeDto?> GetPuntajeWorkerAsync(int workerId);
    Task<(byte[]? Bytes, string? RedirectUrl)> GetPdfAsync(int id);
    Task<AmonestacionCreadaDto> ConfirmarAsync(int id);
    Task CerrarAsync(int id, AmonestacionCerrarRequest req);
}
