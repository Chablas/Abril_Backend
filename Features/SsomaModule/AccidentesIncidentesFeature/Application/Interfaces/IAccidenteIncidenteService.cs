using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Interfaces;

public interface IAccidenteIncidenteService
{
    Task<FlashReportInicializarDto> GetInicializarAsync();
    Task<object> GetListAsync(int? proyectoId, int? tipoId, string? estado,
        DateTime? fechaDesde, DateTime? fechaHasta, bool? soloEnviados, int page, int pageSize);
    Task<FlashReportDetalleDto> GetDetalleAsync(int id);
    Task<int> CrearAsync(CrearFlashReportRequest request, int? usuarioId);
    Task ActualizarAsync(int id, ActualizarFlashReportRequest request);
    Task EnviarFlashReportAsync(int id);
    Task EliminarAsync(int id);
}
