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
        /// <returns>URL web del archivo subido (webUrl), o null si falla.</returns>
        Task<string?> UploadToSharePointLibraryAsync(
            string libraryName,
            string folderPath,
            string fileName,
            Stream fileStream,
            string contentType = "application/octet-stream");
    }
}
