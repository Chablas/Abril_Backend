using System.Globalization;
using System.Text;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Shared.Services.Convivir.Dtos;
using Abril_Backend.Shared.Services.Convivir.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;

namespace Abril_Backend.Shared.Services.Convivir.Services
{
    /// <inheritdoc cref="IPropietarioDocumentoStorage"/>
    public class PropietarioDocumentoStorage : IPropietarioDocumentoStorage
    {
        /// <summary>Peso máximo de un documento (una minuta escaneada anda por los 10 MB).</summary>
        public const long MaxBytes = 25L * 1024 * 1024;

        /// <summary>Documentos por guardado: tope del request junto con <see cref="MaxBytes"/>.</summary>
        public const int MaxPorGuardado = 10;

        /// <summary>Tope del request: el lote completo más el resto del multipart.</summary>
        public const long MaxRequestBytes = MaxPorGuardado * MaxBytes + 5L * 1024 * 1024;

        /// <summary>PDF e imágenes: lo que el teléfono abre sin instalar nada.</summary>
        private static readonly Dictionary<string, string> Formatos = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"]  = "application/pdf",
            [".jpg"]  = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"]  = "image/png",
        };

        private readonly IGraphSharePointService _sharePoint;
        private readonly ILogger<PropietarioDocumentoStorage> _logger;

        public PropietarioDocumentoStorage(IGraphSharePointService sharePoint, ILogger<PropietarioDocumentoStorage> logger)
        {
            _sharePoint = sharePoint;
            _logger     = logger;
        }

        public void Validar(IFormFile archivo)
        {
            var nombre = Path.GetFileName(archivo.FileName);
            if (!Formatos.ContainsKey(Path.GetExtension(nombre)))
                throw new AbrilException($"Formato no permitido: {nombre}. Solo PDF, JPG o PNG.", 400);
            if (archivo.Length == 0)
                throw new AbrilException($"El archivo {nombre} está vacío.", 400);
            if (archivo.Length > MaxBytes)
                throw new AbrilException($"El archivo {nombre} pesa más de 25 MB.", 400);
        }

        public async Task<PropietarioDocumentoCarpetaDto> ResolverCarpetaAsync(string? linkCarpeta)
        {
            if (string.IsNullOrWhiteSpace(linkCarpeta))
                throw new AbrilException(
                    "No se ha configurado la carpeta de SharePoint donde guardar los documentos de los propietarios. " +
                    "Pide al administrador registrarla en la tabla propietario_documento_folder.", 409);

            var carpeta = await _sharePoint.ResolveSharePointFolderUrlAsync(linkCarpeta);
            if (carpeta is null || !carpeta.IsFolder)
                throw new AbrilException(
                    "No se pudo acceder a la carpeta de documentos de propietarios configurada en SharePoint. " +
                    "Verifica el link registrado en propietario_documento_folder.", 502);

            return new PropietarioDocumentoCarpetaDto { DriveId = carpeta.DriveId, ItemId = carpeta.ItemId };
        }

        public async Task<PropietarioDocumentoSubidoDto> SubirAsync(
            PropietarioDocumentoCarpetaDto carpeta,
            IReadOnlyList<string> subcarpetas,
            string nombre,
            IFormFile archivo)
        {
            var nombreOriginal = Path.GetFileName(archivo.FileName);
            var ext = Path.GetExtension(nombreOriginal).ToLowerInvariant();

            // Marca de tiempo de Perú + sufijo al azar: la subida de Graph reemplaza al archivo con el
            // mismo nombre, y dos documentos del mismo lote pueden llamarse igual.
            var stamp = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5))
                .ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var fileName = $"{SanitizarNombre(nombre)}_{stamp}_{Guid.NewGuid().ToString("N")[..6]}{ext}";

            SharePointUploadResultDto? result;
            try
            {
                var destino = await CarpetaDestinoAsync(carpeta, subcarpetas);

                // Sin autoRenameOnLock: ese camino copia el archivo entero a memoria, y el nombre ya es único.
                using var stream = archivo.OpenReadStream();
                result = await _sharePoint.UploadToOneDriveFolderAsync(
                    carpeta.DriveId, destino, fileName, stream, ContentType(nombreOriginal));
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la subida del documento de propietario {Archivo} a SharePoint", nombreOriginal);
                throw new AbrilException("Error al subir el documento a SharePoint.", 502);
            }

            if (result?.WebUrl is null || result.ItemId is null)
                throw new AbrilException($"No se pudo subir el archivo {nombreOriginal}.", 502);

            return new PropietarioDocumentoSubidoDto
            {
                Url     = result.WebUrl,
                DriveId = carpeta.DriveId,
                ItemId  = result.ItemId,
            };
        }

        public async Task<PropietarioDocumentoArchivoDto> DescargarAsync(string driveId, string itemId)
        {
            try
            {
                var (bytes, contentType) = await _sharePoint.DownloadFromOneDriveByItemIdAsync(driveId, itemId);
                return new PropietarioDocumentoArchivoDto
                {
                    Contenido   = bytes,
                    ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la descarga del documento de propietario (driveId={DriveId}, itemId={ItemId})",
                    driveId, itemId);
                throw new AbrilException("No se pudo descargar el documento. Inténtalo de nuevo en unos minutos.", 502);
            }
        }

        public string ContentType(string nombreArchivo) =>
            Formatos.TryGetValue(Path.GetExtension(nombreArchivo), out var tipo) ? tipo : "application/octet-stream";

        /// <summary>Crea (o encuentra) cada subcarpeta una vez por lote.</summary>
        private async Task<string> CarpetaDestinoAsync(PropietarioDocumentoCarpetaDto carpeta, IReadOnlyList<string> subcarpetas)
        {
            var padre = carpeta.ItemId;
            var ruta  = string.Empty;

            foreach (var subcarpeta in subcarpetas.Select(SanitizarNombre))
            {
                ruta += "/" + subcarpeta;
                if (!carpeta.Subcarpetas.TryGetValue(ruta, out var id))
                {
                    id = await _sharePoint.EnsureChildFolderAsync(carpeta.DriveId, padre, subcarpeta);
                    carpeta.Subcarpetas[ruta] = id;
                }
                padre = id;
            }

            return padre;
        }

        /// <summary>
        /// Nombre válido para SharePoint que conserva espacios y tildes (el de LearningVideoStorage).
        /// No se usa Path.GetInvalidFileNameChars: en el contenedor Linux solo trae '/' y '\0'.
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
