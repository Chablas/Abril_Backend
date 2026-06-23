using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Interfaces;

public interface IAccidenteIncidenteRepository
{
    Task<FlashReportInicializarDto> GetInicializarAsync();
    Task<(List<FlashReportListItemDto> Items, int Total)> GetListAsync(
        int? proyectoId, int? tipoId, string? estado,
        DateTime? fechaDesde, DateTime? fechaHasta,
        bool? soloEnviados, int page, int pageSize);
    Task<FlashReportDetalleDto?> GetDetalleAsync(int id);
    Task<string> GenerarCodigoAsync(int proyectoId, string tipoCodigoCorto);
    Task<int> CrearAsync(CrearFlashReportRequest request, string codigo, string? urlFoto1, string? urlFoto2, int? usuarioId);
    Task ActualizarAsync(int id, ActualizarFlashReportRequest request, string? urlFoto1, string? urlFoto2);
    Task MarcarEnviadoAsync(int id, string urlPdf);
    Task EliminarAsync(int id);
}
