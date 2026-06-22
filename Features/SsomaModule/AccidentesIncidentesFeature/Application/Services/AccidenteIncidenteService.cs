using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Interfaces;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;

namespace Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Services;

public class AccidenteIncidenteService : IAccidenteIncidenteService
{
    private readonly IAccidenteIncidenteRepository _repo;
    private readonly ISharePointHabService _sp;

    public AccidenteIncidenteService(IAccidenteIncidenteRepository repo, ISharePointHabService sp)
    {
        _repo = repo;
        _sp = sp;
    }

    public async Task<object> GetListAsync(int? proyectoId, string? tipo, string? estado,
        DateTime? fechaDesde, DateTime? fechaHasta, int page, int pageSize)
    {
        var items = await _repo.GetListAsync(proyectoId, tipo, estado, fechaDesde, fechaHasta, page, pageSize);
        var total = await _repo.GetListCountAsync(proyectoId, tipo, estado, fechaDesde, fechaHasta);
        return new { items, total, page, pageSize };
    }

    public async Task<AccidenteIncidenteDetalleDto> GetDetalleAsync(int id)
    {
        var result = await _repo.GetDetalleAsync(id);
        if (result == null) throw new AbrilException("Accidente/Incidente no encontrado.", 404);
        return result;
    }

    public async Task<int> CrearAsync(CrearAccidenteIncidenteRequest request, int? usuarioId)
    {
        if (request.ProyectoId <= 0)
            throw new AbrilException("El proyecto es requerido.", 400);
        if (string.IsNullOrWhiteSpace(request.Descripcion))
            throw new AbrilException("La descripción es requerida.", 400);
        if (string.IsNullOrWhiteSpace(request.Tipo))
            throw new AbrilException("El tipo es requerido.", 400);
        return await _repo.CrearAsync(request, usuarioId);
    }

    public async Task ActualizarAsync(int id, ActualizarAccidenteIncidenteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Descripcion))
            throw new AbrilException("La descripción es requerida.", 400);
        await _repo.ActualizarAsync(id, request);
    }

    public Task EliminarAsync(int id) => _repo.EliminarAsync(id);

    public async Task<int> SubirDocumentoAsync(int accidenteId, SubirDocumentoRequest request, int? usuarioId)
    {
        if (string.IsNullOrEmpty(request.ContenidoBase64))
            throw new AbrilException("El contenido del archivo es requerido.", 400);

        var data = request.ContenidoBase64.Contains(",")
            ? request.ContenidoBase64.Split(',')[1]
            : request.ContenidoBase64;
        var bytes = Convert.FromBase64String(data);
        using var stream = new MemoryStream(bytes);

        var fileName = $"accidente_{accidenteId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{request.NombreArchivo}";
        var url = await _sp.SubirArchivoAsync(stream, fileName, "ssoma-accidentes");

        return await _repo.SubirDocumentoAsync(accidenteId, request, url, usuarioId);
    }

    public async Task<DocumentoAdjuntoDto> GetDocumentoAsync(int accidenteId, int docId)
    {
        var doc = await _repo.GetDocumentoAsync(accidenteId, docId);
        if (doc == null) throw new AbrilException("Documento no encontrado.", 404);
        return doc;
    }
}
