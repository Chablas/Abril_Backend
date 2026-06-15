using Abril_Backend.Shared.Services.SharePoint.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Options;

namespace Abril_Backend.Shared.Services.SharePoint.Interfaces
{
    public interface IGraphSharePointService
    {
        /// <summary>
        /// Lista los hijos directos de una carpeta en un drive de OneDrive/SharePoint,
        /// excluyendo subcarpetas cuyos nombres estén en <paramref name="excludedFolderNames"/>.
        /// </summary>
        Task<List<OneDriveFolderItemDto>> GetFolderChildrenAsync(
            string driveId,
            string folderPath,
            IEnumerable<string>? excludedFolderNames = null);

        /// <summary>
        /// Descarga el contenido de un archivo en un drive de OneDrive/SharePoint por su itemId.
        /// </summary>
        Task<(byte[] Content, string? ContentType)> DownloadFromOneDriveByItemIdAsync(string driveId, string itemId);

        /// <summary>
        /// Sube un archivo al OneDrive personal del usuario autenticado (permiso delegado).
        /// </summary>
        Task<string?> UploadToOneDriveAsync(
            string accessToken,
            string folderPath,
            string fileName,
            Stream fileStream,
            string contentType = "application/octet-stream");

        /// <summary>
        /// Sube un archivo a una biblioteca de documentos compartida en el sitio indicado por
        /// <paramref name="site"/>, usando permisos de aplicación (Sites.ReadWrite.All).
        /// Si <paramref name="autoRenameOnLock"/> es true y el archivo destino está bloqueado/en uso
        /// (HTTP 423), reintenta con un nombre alterno ("nombre (2).ext", "(3)", …). El nombre
        /// finalmente usado se devuelve en <see cref="SharePointUploadResultDto.FileName"/>.
        /// </summary>
        Task<SharePointUploadResultDto?> UploadToSharePointLibraryAsync(
            SharePointSiteRef site,
            string libraryName,
            string folderPath,
            string fileName,
            Stream fileStream,
            string contentType = "application/octet-stream",
            bool autoRenameOnLock = false);

        /// <summary>
        /// Resuelve un link de SharePoint/OneDrive (cualquier formato) a su ubicación estable
        /// (driveId + itemId) usando la Graph Shares API con permisos de aplicación. Devuelve
        /// null si no se puede resolver (no existe, sin acceso o link inválido).
        /// </summary>
        Task<ShareLinkResolveDto?> ResolveShareLinkAsync(string link);

        /// <summary>Lista las subcarpetas directas (id + nombre) de un item por su driveId+itemId.</summary>
        Task<List<ShareLinkResolveDto>> GetChildFoldersByItemIdAsync(string driveId, string itemId);

        /// <summary>Obtiene un driveItem por driveId+itemId (para validar/leer la carpeta elegida). Null si no existe.</summary>
        Task<ShareLinkResolveDto?> GetDriveItemAsync(string driveId, string itemId);

        /// <summary>
        /// Descarga el contenido de un archivo del sitio indicado por <paramref name="site"/>
        /// a partir de su webUrl, usando permisos de aplicación (Files.Read.All). La webUrl
        /// debe pertenecer al sitio o se lanza una excepción.
        /// </summary>
        Task<byte[]> DownloadFromSharePointAsync(SharePointSiteRef site, string webUrl);

        /// <summary>
        /// Descarga un archivo del sitio indicado convertido a PDF mediante Graph API (?format=pdf).
        /// </summary>
        Task<byte[]> DownloadAsPdfFromSharePointAsync(SharePointSiteRef site, string libraryName, string itemId);

        /// <summary>
        /// Descarga varios archivos en paralelo usando el endpoint /$batch de Microsoft Graph.
        /// Cada entrada indica si el archivo ya es PDF (se descarga tal cual) o si debe convertirse (?format=pdf).
        /// Devuelve un diccionario itemId → bytes del PDF.
        /// Si un sub-request falla, se lanza InvalidOperationException con el detalle del error.
        /// </summary>
        Task<Dictionary<string, byte[]>> DownloadMultipleAsPdfFromSharePointAsync(
            SharePointSiteRef site,
            string libraryName,
            IReadOnlyList<(string ItemId, bool AlreadyPdf)> items);

        /// <summary>
        /// Busca en la raíz de la biblioteca del sitio indicado la primera carpeta cuyo nombre
        /// comience con <paramref name="ruc"/> seguido de " - ".
        /// </summary>
        Task<(string Id, string Name)?> FindContractorFolderAsync(SharePointSiteRef site, string libraryName, string ruc);

        /// <summary>
        /// Renombra una carpeta en una biblioteca del sitio indicado usando su itemId.
        /// </summary>
        Task RenameFolderInLibraryAsync(SharePointSiteRef site, string libraryName, string folderId, string newName);

        /// <summary>
        /// Elimina un archivo (o carpeta) de una biblioteca del sitio indicado a partir de su itemId.
        /// Lanza si el item no existe o la operación falla.
        /// </summary>
        Task DeleteFromSharePointLibraryAsync(SharePointSiteRef site, string libraryName, string itemId);
    }
}
