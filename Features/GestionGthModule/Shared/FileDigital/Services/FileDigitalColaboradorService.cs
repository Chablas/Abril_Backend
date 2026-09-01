using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Dtos;
using Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Services
{
    /// <summary>
    /// Subcarpetas del file del colaborador. GTH pidió que cada documento del expediente viva en su
    /// propia carpeta con nombre fijo (no una por proceso), para poder ubicarlo siempre en el mismo
    /// sitio y dar permisos sobre el file completo. Son literales: si se renombran acá, los documentos
    /// nuevos van a una carpeta nueva y los ya subidos se quedan donde están.
    /// </summary>
    public static class SubcarpetaFileDigital
    {
        public const string CartaEnviada = "Carta Oferta Enviada";
        public const string CartaFirmada = "Carta Oferta Firmada";
    }

    /// <inheritdoc cref="IFileDigitalColaboradorService"/>
    public class FileDigitalColaboradorService : IFileDigitalColaboradorService
    {
        private readonly IFileDigitalFolderRepository _repo;
        private readonly IGraphSharePointService _sharePoint;
        private readonly ILogger<FileDigitalColaboradorService> _logger;

        public FileDigitalColaboradorService(
            IFileDigitalFolderRepository repo,
            IGraphSharePointService sharePoint,
            ILogger<FileDigitalColaboradorService> logger)
        {
            _repo       = repo;
            _sharePoint = sharePoint;
            _logger     = logger;
        }

        /// <summary>
        /// Resuelve la biblioteca configurada (link en <c>gth_carta_oferta_folder</c>) y, dentro, la
        /// carpeta del colaborador —«{DNI} - {NOMBRE}»—: esa carpeta es su file digital y de su nombre
        /// dependen tanto ubicarla como los permisos que se le den encima.
        ///
        /// El link se lee de la BD en cada uso, así que cambiar esa fila redirige los documentos nuevos
        /// sin redeploy y sin tocar los anteriores. Para una carta oferta ya enviada no se vuelve a
        /// llamar: su carpeta queda persistida en la fila.
        ///
        /// Si la carpeta no se puede crear se corta con error, sin caer a la raíz de la biblioteca:
        /// dejar el documento suelto en la raíz lo saca del file del colaborador y lo pone donde el
        /// permiso que cuelga de su carpeta no aplica.
        /// </summary>
        public async Task<FileDigitalCarpetaDto> ResolverCarpetaAsync(string dni, string nombre)
        {
            // Al enviar la carta oferta esto ya se validó con un mensaje más preciso; la guarda cubre
            // a los expedientes viejos, que rearman su carpeta acá al subirles un documento nuevo.
            if (string.IsNullOrWhiteSpace(dni))
                throw new AbrilException(
                    "El colaborador no tiene documento de identidad registrado y con él se nombra su carpeta en el file de colaboradores. Complétalo en su ficha de la base maestra.", 409);

            var folder = await _repo.GetFolder();
            if (folder == null || string.IsNullOrWhiteSpace(folder.LinkUrl))
                throw new AbrilException(
                    "No está configurada la biblioteca de SharePoint donde se guarda el file de los colaboradores.", 500);

            var raiz = await _sharePoint.ResolveSharePointFolderUrlAsync(folder.LinkUrl);
            if (raiz == null || !raiz.IsFolder)
                throw new AbrilException(
                    "No se pudo resolver en SharePoint la biblioteca configurada para el file de los colaboradores. Revisa que el link apunte a una carpeta existente y accesible.", 502);

            var biblioteca = string.IsNullOrWhiteSpace(folder.FolderName) ? raiz.Name : folder.FolderName;

            // El nombre va en mayúsculas a propósito: viene tal cual lo escribió el postulante y la
            // carpeta es el identificador del file, que GTH lee a diario. EnsureChildFolder compara
            // sin distinguir mayúsculas, así que un cambio de capitalización no duplica la carpeta.
            var carpetaColaborador = $"{SanitizeFilename(dni)} - {SanitizeFilename(nombre).ToUpperInvariant()}";

            try
            {
                var itemId = await _sharePoint.EnsureChildFolderAsync(raiz.DriveId, raiz.ItemId, carpetaColaborador);
                return new FileDigitalCarpetaDto
                {
                    DriveId = raiz.DriveId,
                    ItemId  = itemId,
                    Ruta    = $"{biblioteca} / {carpetaColaborador}",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "No se pudo crear el file del colaborador {Carpeta} en SharePoint", carpetaColaborador);
                throw new AbrilException(
                    $"No se pudo crear en SharePoint la carpeta «{carpetaColaborador}» del colaborador. Reintenta en unos minutos.", 502);
            }
        }

        public async Task<FileDigitalDocumentoDto> SubirDocumentoAsync(
            FileDigitalCarpetaDto carpeta,
            string subcarpeta,
            string fileName,
            byte[] content,
            string contentType,
            string queEs)
        {
            var destino = await ResolverSubcarpetaAsync(carpeta, subcarpeta);

            try
            {
                using var stream = new MemoryStream(content);
                var result = await _sharePoint.UploadToOneDriveFolderAsync(
                    destino.DriveId, destino.ItemId, fileName,
                    stream, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                    autoRenameOnLock: true);

                if (result?.WebUrl == null)
                    throw new AbrilException($"No se pudo subir {queEs} a SharePoint.", 502);

                // La fila que lo referencia se queda con el driveId/itemId/webUrl de ESTA subida: si
                // mañana se cambia la biblioteca configurada, el documento se sigue abriendo desde acá.
                return new FileDigitalDocumentoDto
                {
                    Nombre  = result.FileName ?? fileName,
                    Url     = result.WebUrl,
                    ItemId  = result.ItemId,
                    DriveId = destino.DriveId,
                };
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la subida de {QueEs} al archivo {FileName}", queEs, fileName);
                throw new AbrilException($"Error al subir {queEs} a SharePoint.", 502);
            }
        }

        public string NombreArchivo(string prefijo, string codigo, string extension) =>
            $"{prefijo}_{SanitizeFilename(codigo)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}";

        public async Task<byte[]> DescargarComoPdfAsync(string driveId, string itemId, string queEs)
        {
            if (string.IsNullOrWhiteSpace(driveId) || string.IsNullOrWhiteSpace(itemId))
                throw new AbrilException($"No se encontró en SharePoint {queEs} que hay que convertir a PDF.", 409);

            try
            {
                // La conversión la hace Graph (?format=pdf). Va por la ruta de batch porque es la
                // única que expone el servicio compartido; con un solo item el batch es un request.
                var pdfs = await _sharePoint.DownloadMultipleAsPdfFromOneDriveAsync(
                    driveId, new[] { (ItemId: itemId, AlreadyPdf: false) });

                if (!pdfs.TryGetValue(itemId, out var bytes) || bytes.Length == 0)
                    throw new AbrilException($"SharePoint no devolvió el PDF de {queEs}. Reintenta en unos minutos.", 502);

                return bytes;
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la conversión a PDF de {QueEs} ({ItemId})", queEs, itemId);
                throw new AbrilException(
                    $"No se pudo convertir {queEs} a PDF. Revisa que el documento siga en el file del colaborador y no esté abierto en Word.",
                    502);
            }
        }

        /// <summary>
        /// Devuelve la subcarpeta <paramref name="nombre"/> dentro del file del colaborador, creándola
        /// si es la primera vez. Se resuelve al subir y no se persiste: la fila guarda el file (la
        /// carpeta padre) y cada tipo de documento sabe en qué subcarpeta va. EnsureChildFolder es
        /// idempotente, así que la segunda carta cae en la misma.
        /// </summary>
        private async Task<FileDigitalCarpetaDto> ResolverSubcarpetaAsync(FileDigitalCarpetaDto carpeta, string nombre)
        {
            try
            {
                var itemId = await _sharePoint.EnsureChildFolderAsync(carpeta.DriveId, carpeta.ItemId, nombre);
                return new FileDigitalCarpetaDto
                {
                    DriveId = carpeta.DriveId,
                    ItemId  = itemId,
                    Ruta    = string.IsNullOrWhiteSpace(carpeta.Ruta) ? nombre : $"{carpeta.Ruta} / {nombre}",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "No se pudo crear la subcarpeta «{Sub}» dentro del file {ItemId}", nombre, carpeta.ItemId);
                throw new AbrilException(
                    $"No se pudo crear en SharePoint la carpeta «{nombre}» dentro del file del colaborador. Reintenta en unos minutos.", 502);
            }
        }

        /// <summary>Deja el texto usable como nombre de archivo/carpeta en SharePoint.</summary>
        private static string SanitizeFilename(string value)
        {
            var limpio = new string((value ?? string.Empty)
                .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch == '#' || ch == '%' ? '_' : ch)
                .ToArray())
                .Trim();
            return string.IsNullOrWhiteSpace(limpio) ? "sin_nombre" : limpio;
        }
    }

    /// <inheritdoc cref="IFileDigitalFolderRepository"/>
    public class FileDigitalFolderRepository : IFileDigitalFolderRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public FileDigitalFolderRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<FileDigitalFolderDto?> GetFolder()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.GthCartaOfertaFolder
                .Where(f => f.State && f.Active)
                .OrderBy(f => f.GthCartaOfertaFolderId)
                .Select(f => new FileDigitalFolderDto { LinkUrl = f.LinkUrl, FolderName = f.FolderName })
                .FirstOrDefaultAsync();
        }
    }
}
