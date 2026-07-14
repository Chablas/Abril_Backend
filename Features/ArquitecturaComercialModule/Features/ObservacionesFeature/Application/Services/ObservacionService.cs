using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Application.Dtos;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Application.Interfaces;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Application.Services;

public class ObservacionService : IObservacionService
{
    private readonly IObservacionRepository _repository;
    private readonly IObservacionSharePointService _sharePoint;

    public ObservacionService(IObservacionRepository repository, IObservacionSharePointService sharePoint)
    {
        _repository = repository;
        _sharePoint = sharePoint;
    }

    public Task<ObservacionListResponseDTO> GetObservaciones(int? proyectoId, string? estado, string? partida, DateTime? desde, DateTime? hasta, string? search, int pagina, int porPagina)
        => _repository.GetObservaciones(proyectoId, estado, partida, desde, hasta, search, pagina, porPagina);

    public Task<ObservacionListItemDTO?> GetObservacionById(int id) => _repository.GetObservacionById(id);

    public Task<ObservacionFiltrosDTO> GetFiltros() => _repository.GetFiltros();

    public Task<ObservacionDashboardDTO> GetDashboard(DateTime? desde, DateTime? hasta, int? proyectoId)
        => _repository.GetDashboard(desde, hasta, proyectoId);

    public async Task<ObservacionListItemDTO> CreateObservacion(CreateObservacionDTO body, Stream? fotoStream, string? fotoFileName)
    {
        if (body.ProyectoId <= 0) throw new AbrilException("Debe seleccionar un proyecto.", 400);
        if (string.IsNullOrWhiteSpace(body.Descripcion)) throw new AbrilException("Debe ingresar una descripción de la observación.", 400);

        var abbreviation = await _repository.GetProyectoAbbreviation(body.ProyectoId);
        var anio = body.Fecha.Year;
        var correlativo = await _repository.GetProximoCorrelativo(abbreviation, anio);
        var codigo = $"{abbreviation}-{correlativo}-{anio}";

        var entity = await _repository.CreateObservacion(body, codigo);

        if (fotoStream != null && !string.IsNullOrWhiteSpace(fotoFileName))
        {
            var contentType = GetContentType(fotoFileName);
            var url = await _sharePoint.SubirFotoObservacionAsync(fotoStream, fotoFileName, contentType, body.ProyectoId, entity.Id);
            await _repository.AgregarFoto(entity.Id, "Observacion", url, 0);
        }

        return (await _repository.GetObservacionById(entity.Id))!;
    }

    public async Task<ObservacionListItemDTO?> LevantarObservacion(int id, Stream? fotoStream, string? fotoFileName, LevantarObservacionDTO body)
    {
        var actual = await _repository.GetObservacionById(id);
        if (actual == null) throw new AbrilException("No se encontró la observación.", 404);

        if (fotoStream != null && !string.IsNullOrWhiteSpace(fotoFileName))
        {
            var contentType = GetContentType(fotoFileName);
            var orden = actual.Fotos.Count(f => f.Tipo == "Levantamiento");
            var url = await _sharePoint.SubirFotoLevantamientoAsync(fotoStream, fotoFileName, contentType, actual.ProyectoId, id, orden);
            await _repository.AgregarFoto(id, "Levantamiento", url, orden);
        }

        return await _repository.LevantarObservacion(id);
    }

    public Task<ObservacionListItemDTO?> UpdateObservacion(int id, UpdateObservacionDTO body)
        => _repository.UpdateObservacion(id, body);

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
    }
}
