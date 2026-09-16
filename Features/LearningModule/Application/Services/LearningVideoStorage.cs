using System.Globalization;
using System.Text;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.LearningModule.Application.Dtos;
using Abril_Backend.Features.LearningModule.Application.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;

namespace Abril_Backend.Features.LearningModule.Application.Services
{
    /// <inheritdoc cref="ILearningVideoStorage"/>
    public class LearningVideoStorage : ILearningVideoStorage
    {
        /// <summary>Peso máximo de un archivo, sea video o manual.</summary>
        public const long MaxBytes = 500L * 1024 * 1024;

        /// <summary>Tope del request: el archivo más el resto del multipart.</summary>
        public const long MaxRequestBytes = MaxBytes + 5L * 1024 * 1024;

        /// <summary>Videos: los formatos que reproduce el reproductor de Stream de SharePoint.</summary>
        private static readonly string[] ExtensionesVideo =
            [".mp4", ".m4v", ".mov", ".webm", ".avi", ".wmv", ".mkv", ".mpg", ".mpeg"];

        /// <summary>Manuales: PDF e imágenes, que SharePoint abre en el navegador.</summary>
        private static readonly string[] ExtensionesManual = [".pdf", ".jpg", ".jpeg", ".png", ".webp"];

        private readonly IGraphSharePointService _sharePoint;
        private readonly ILogger<LearningVideoStorage> _logger;

        public LearningVideoStorage(IGraphSharePointService sharePoint, ILogger<LearningVideoStorage> logger)
        {
            _sharePoint = sharePoint;
            _logger     = logger;
        }

        public async Task<LearningVideoArchivoDto> SubirAsync(IFormFile archivo, string? folderLink)
        {
            var nombreOriginal = Path.GetFileName(archivo.FileName);
            var ext = Path.GetExtension(nombreOriginal).ToLowerInvariant();
            var esVideo = ExtensionesVideo.Contains(ext);
            if (!esVideo && !ExtensionesManual.Contains(ext))
                throw new AbrilException(
                    $"Formato no permitido: {nombreOriginal}. Solo PDF, imágenes (JPG, PNG o WEBP) " +
                    "o videos (MP4, M4V, MOV, WEBM, AVI, WMV, MKV o MPG).", 400);
            if (archivo.Length > MaxBytes)
                throw new AbrilException("El archivo pesa más de 500 MB.", 400);

            if (string.IsNullOrWhiteSpace(folderLink))
                throw new AbrilException(
                    "No se ha configurado la carpeta de SharePoint donde guardar los videos y manuales. " +
                    "Pide al administrador registrarla en la tabla learning_video_folder.", 409);

            var carpeta = await _sharePoint.ResolveSharePointFolderUrlAsync(folderLink);
            if (carpeta is null || !carpeta.IsFolder)
                throw new AbrilException(
                    "No se pudo acceder a la carpeta de videos y manuales configurada en SharePoint. " +
                    "Verifica el link registrado en learning_video_folder.", 502);

            // Nombre legible (la biblioteca la navega gente) + marca de tiempo de Perú: una subida con el
            // mismo nombre nunca pisa a un archivo anterior, que sigue enlazado desde su propia fila.
            var stamp = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5))
                .ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var fileName = $"{SanitizarNombre(Path.GetFileNameWithoutExtension(nombreOriginal))}_{stamp}{ext}";

            SharePointUploadResultDto? result;
            try
            {
                // Sin autoRenameOnLock a propósito: ese camino copia el archivo entero a memoria para
                // poder reintentar, y un video pesa cientos de MB. Así sube por fragmentos leyendo del
                // stream del IFormFile.
                using var stream = archivo.OpenReadStream();
                result = await _sharePoint.UploadToOneDriveFolderAsync(
                    carpeta.DriveId, carpeta.ItemId, fileName, stream,
                    string.IsNullOrWhiteSpace(archivo.ContentType) ? "application/octet-stream" : archivo.ContentType);
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la subida del archivo {Archivo} a SharePoint", nombreOriginal);
                throw new AbrilException("Error al subir el archivo a SharePoint.", 502);
            }

            if (result?.WebUrl is null || result.ItemId is null)
                throw new AbrilException($"No se pudo subir el archivo {nombreOriginal}.", 502);

            return new LearningVideoArchivoDto
            {
                // Stream solo reproduce video: un PDF o una imagen se abre con el link del propio archivo.
                Url      = esVideo ? StreamUrl(result.WebUrl) : result.WebUrl,
                FileName = nombreOriginal.Length > 255 ? nombreOriginal[..255] : nombreOriginal,
                DriveId  = carpeta.DriveId,
                ItemId   = result.ItemId,
            };
        }

        /// <summary>
        /// Link del reproductor de Stream de SharePoint (<c>.../_layouts/15/stream.aspx?id={ruta}</c>), el
        /// mismo formato que tienen los videos que se cargaban a mano. El webUrl de Graph apunta al
        /// archivo en sí.
        /// </summary>
        private static string StreamUrl(string webUrl)
        {
            if (!Uri.TryCreate(webUrl, UriKind.Absolute, out var uri)
                || uri.AbsolutePath.Contains("/_layouts/", StringComparison.OrdinalIgnoreCase))
                return webUrl;

            var ruta      = Uri.UnescapeDataString(uri.AbsolutePath);
            var segmentos = ruta.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // Colección de sitios ("/sites/x" o "/teams/x"); si no, el sitio raíz del tenant.
            var sitio = segmentos.Length > 2
                && (segmentos[0].Equals("sites", StringComparison.OrdinalIgnoreCase)
                    || segmentos[0].Equals("teams", StringComparison.OrdinalIgnoreCase))
                ? $"/{segmentos[0]}/{segmentos[1]}"
                : string.Empty;

            return $"{uri.GetLeftPart(UriPartial.Authority)}{sitio}/_layouts/15/stream.aspx?id={Uri.EscapeDataString(ruta)}";
        }

        /// <summary>
        /// Nombre válido para SharePoint que conserva espacios y tildes. No se usa
        /// Path.GetInvalidFileNameChars: en el contenedor Linux solo trae '/' y '\0'.
        /// </summary>
        private static string SanitizarNombre(string nombre)
        {
            const string invalidos = "\"*:<>?/\\|#%";

            var sb = new StringBuilder(nombre.Length);
            foreach (var ch in nombre)
                sb.Append(char.IsControl(ch) || invalidos.Contains(ch) ? '_' : ch);

            var limpio = string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Replace("_vti_", "_vti-", StringComparison.OrdinalIgnoreCase)
                .TrimStart('~')
                .Trim('.', ' ');

            if (limpio.Length > 80)
                limpio = limpio[..(char.IsHighSurrogate(limpio[79]) ? 79 : 80)].TrimEnd('.', ' ');

            return string.IsNullOrEmpty(limpio) ? "archivo" : limpio;
        }
    }
}
