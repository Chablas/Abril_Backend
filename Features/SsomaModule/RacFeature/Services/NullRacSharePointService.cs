namespace Abril_Backend.Features.Ssoma.Rac.Services;

public class NullRacSharePointService : IRacSharePointService
{
    public Task<string> SubirPdfAsync(Stream pdfStream, string filename, int racId)
        => Task.FromResult(string.Empty);

    public Task<string> SubirFotoAsync(Stream stream, string filename, int racId)
        => Task.FromResult(string.Empty);
}
