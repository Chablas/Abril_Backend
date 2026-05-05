using Abril_Backend.Shared.Services.SharePoint.Dtos;

namespace Abril_Backend.Shared.Services.SharePoint.Interfaces
{
    public interface IGraphSharePointService
    {
        /// <summary>
        /// Sube un archivo al OneDrive personal del usuario autenticado (permiso delegado).
        /// </summary>
        /// <param name="accessToken">Token de acceso Graph del usuario (delegado).</param>
        /// <param name="folderPath">Ruta de carpetas relativa a la raíz del drive. Ej: "Contratistas/20123456789"</param>
        /// <param name="fileName">Nombre del archivo incluyendo extensión.</param>
        /// <param name="fileStream">Contenido del archivo.</param>
        /// <param name="contentType">MIME type del archivo.</param>
        /// <returns>URL web del archivo subido (webUrl), o null si falla.</returns>
        Task<string?> UploadToOneDriveAsync(
            string accessToken,
            string folderPath,
            string fileName,
            Stream fileStream,
            string contentType = "application/octet-stream");

        /// <summary>
        /// Sube un archivo a una biblioteca de documentos compartida en SharePoint
        /// usando permisos de aplicación (Sites.ReadWrite.All).
        /// Crea la biblioteca si no existe.
        /// Los archivos son accesibles desde el explorador de Windows.
        /// </summary>
        /// <param name="libraryName">Nombre de la biblioteca de documentos. Ej: "Adjudicaciones"</param>
        /// <param name="folderPath">Ruta de carpetas dentro de la biblioteca. Ej: "Proyecto X/20123456789 - Empresa/42 - Partida/Contrato"</param>
        /// <param name="fileName">Nombre del archivo incluyendo extensión.</param>
        /// <param name="fileStream">Contenido del archivo.</param>
        /// <param name="contentType">MIME type del archivo.</param>
        /// <returns>Resultado con WebUrl e ItemId del archivo subido, o null si falla.</returns>
        Task<SharePointUploadResultDto?> UploadToSharePointLibraryAsync(
            string libraryName,
            string folderPath,
            string fileName,
            Stream fileStream,
            string contentType = "application/octet-stream");

        /// <summary>
        /// Descarga el contenido de un archivo de SharePoint a partir de su webUrl,
        /// usando permisos de aplicación (Files.Read.All).
        /// </summary>
        /// <param name="webUrl">URL web del archivo tal como fue guardada al subirlo.</param>
        /// <returns>Bytes del archivo.</returns>
        Task<byte[]> DownloadFromSharePointAsync(string webUrl);

        /// <summary>
        /// Descarga un archivo de SharePoint convertido a PDF mediante la Graph API (?format=pdf).
        /// Funciona con Word (.docx) y Excel (.xlsx).
        /// Usa el endpoint basado en itemId para evitar problemas con URLs del tipo _layouts/15/Doc.aspx.
        /// </summary>
        /// <param name="libraryName">Nombre de la biblioteca de documentos (ej. "Adjudicaciones").</param>
        /// <param name="itemId">ID del item en Graph API (SharepointItemId guardado al subir el archivo).</param>
        /// <returns>Bytes del PDF resultante.</returns>
        Task<byte[]> DownloadAsPdfFromSharePointAsync(string libraryName, string itemId);
    }
}
