namespace Abril_Backend.Shared.Services.SharePoint.Interfaces
{
    public interface IGraphSharePointService
    {
        /// <summary>
        /// Sube un archivo a SharePoint usando la cuenta delegada del usuario autenticado.
        /// </summary>
        /// <param name="accessToken">Token de acceso Graph del usuario autenticado.</param>
        /// <param name="folderPath">Ruta de carpetas relativa a la raíz del drive (ej: "Contratistas/20123456789").</param>
        /// <param name="fileName">Nombre del archivo a guardar (ej: "brochure.pdf").</param>
        /// <param name="fileStream">Contenido del archivo.</param>
        /// <param name="contentType">MIME type del archivo.</param>
        /// <returns>URL web del archivo subido (webUrl), o null si falla.</returns>
        Task<string?> UploadFileAsync(
            string accessToken,
            string folderPath,
            string fileName,
            Stream fileStream,
            string contentType = "application/octet-stream");
    }
}
