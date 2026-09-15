namespace Abril_Backend.Features.Ssoma.Rac.Services;

public class NullRacSharePointService : IRacSharePointService
{
    public Task<string> SubirPdfAsync(Stream pdfStream, string filename, int racId)        => Task.FromResult(string.Empty);
    public Task<string> SubirFotoAsync(Stream stream, string filename, int racId)          => Task.FromResult(string.Empty);
    public Task<string> SubirFirmaAsync(Stream stream, string filename, int racId)         => Task.FromResult(string.Empty);
    public Task<string> SubirPenalidadPdfAsync(Stream stream, string filename, int penId)  => Task.FromResult(string.Empty);
    public Task<string> SubirPenalidadDescargoAsync(Stream stream, string filename, int penId) => Task.FromResult(string.Empty);
    public Task<string> SubirGestionPreviaAdjuntoAsync(Stream stream, string filename, int empresaId) => Task.FromResult(string.Empty);
    public Task<byte[]?> DescargarFotoAsync(string url)                                   => Task.FromResult<byte[]?>(null);
}
