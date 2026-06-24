namespace Abril_Backend.Features.Habilitacion.Application.Interfaces
{
    public interface ISharePointHabService
    {
        Task<string?> GetDownloadUrlAsync(string archivoUrl, string? libraryContexto = null);
        Task<string> SubirArchivoAsync(Stream fileStream, string fileName, string contexto);
        Task<string> SubirArchivoEnRutaAsync(Stream fileStream, string fileName, string libraryContexto, string carpetaPath);
        Task<string> SubirArchivoYObtenerUrlAsync(Stream fileStream, string fileName, string libraryContexto, string carpetaPath);
    }
}
