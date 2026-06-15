using Abril_Backend.Features.Habilitacion.Application.Interfaces;

namespace Abril_Backend.Features.Ssoma.Rac.Services;

public class RacSharePointService : IRacSharePointService
{
    private readonly ISharePointHabService _sp;

    public RacSharePointService(ISharePointHabService sp) => _sp = sp;

    public Task<string> SubirPdfAsync(Stream stream, string fileName, int racId)
        => _sp.SubirArchivoEnRutaAsync(stream, fileName, "rac-pdf", $"RAC/{racId}");

    public Task<string> SubirFotoAsync(Stream stream, string fileName, int racId)
        => _sp.SubirArchivoEnRutaAsync(stream, fileName, "rac-fotos", $"RAC/{racId}/fotos");

    public Task<string> SubirFirmaAsync(Stream stream, string fileName, int racId)
        => _sp.SubirArchivoEnRutaAsync(stream, fileName, "rac-firmas", $"RAC/{racId}/firmas");

    public Task<string> SubirPenalidadPdfAsync(Stream stream, string fileName, int penalidadId)
        => _sp.SubirArchivoEnRutaAsync(stream, fileName, "penalidad-pdf", $"Penalidades/{penalidadId}");
}
