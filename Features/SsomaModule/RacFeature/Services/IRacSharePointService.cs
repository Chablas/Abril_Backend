namespace Abril_Backend.Features.Ssoma.Rac.Services;

public interface IRacSharePointService
{
    Task<string> SubirPdfAsync(Stream pdfStream, string filename, int racId);
    Task<string> SubirFotoAsync(Stream stream, string filename, int racId);
}
