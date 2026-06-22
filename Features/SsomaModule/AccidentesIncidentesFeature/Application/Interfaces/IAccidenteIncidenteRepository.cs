using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Interfaces;

public interface IAccidenteIncidenteRepository
{
    Task<List<AccidenteIncidenteListItemDto>> GetListAsync(int? proyectoId, string? tipo, string? estado, DateTime? fechaDesde, DateTime? fechaHasta, int page, int pageSize);
    Task<int> GetListCountAsync(int? proyectoId, string? tipo, string? estado, DateTime? fechaDesde, DateTime? fechaHasta);
    Task<AccidenteIncidenteDetalleDto?> GetDetalleAsync(int id);
    Task<int> CrearAsync(CrearAccidenteIncidenteRequest request, int? usuarioId);
    Task ActualizarAsync(int id, ActualizarAccidenteIncidenteRequest request);
    Task EliminarAsync(int id);
    Task<int> SubirDocumentoAsync(int accidenteId, SubirDocumentoRequest request, string url, int? usuarioId);
    Task<DocumentoAdjuntoDto?> GetDocumentoAsync(int accidenteId, int docId);
}
