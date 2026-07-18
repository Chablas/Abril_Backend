using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Options;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Application.Services
{
    public class ActasReunionService : IActasReunionService
    {
        private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB por archivo
        private const int MaxFilesPorSubida = 10;

        private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".txt", ".csv", ".zip", ".rar",
        };

        private readonly IActasReunionRepository _repository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;
        private readonly IGraphSharePointService _sharePointService;
        private readonly string[] _allowedHosts;

        public ActasReunionService(
            IActasReunionRepository repository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver,
            IGraphSharePointService sharePointService,
            IConfiguration configuration)
        {
            _repository = repository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
            _sharePointService = sharePointService;

            // Hosts permitidos del tenant, derivados del sitio ya configurado (mismo criterio
            // que la carpeta de facturas de Contabilidad).
            var siteHost = SharePointSiteRef.FromConfig(configuration, "CostosYPresupuestos").Hostname.ToLowerInvariant();
            var tenant = siteHost.Split('.')[0].Replace("-my", "");
            _allowedHosts = new[] { $"{tenant}.sharepoint.com", $"{tenant}-my.sharepoint.com" };
        }

        public Task<ReunionPaginaInicialDto> GetPaginaInicial(ReunionFiltroRequest filtro)
            => _repository.GetPaginaInicial(filtro);

        public Task<PagedResultDto<ReunionListItemDto>> GetReuniones(ReunionFiltroRequest filtro)
            => _repository.GetReuniones(filtro);

        public Task<ReunionDetalleDto> GetDetalle(int reunionId)
            => _repository.GetDetalle(reunionId);

        public Task<int> Create(ReunionCreateRequest request, int userId)
        {
            if (string.IsNullOrWhiteSpace(request.Tema))
                throw new AbrilException("El tema de la reunión es obligatorio.", 400);
            if (request.ProjectId <= 0)
                throw new AbrilException("Debe seleccionar un proyecto.", 400);
            ValidarHoras(request.HoraInicio, request.HoraFin);
            return _repository.Create(request, userId);
        }

        public Task Update(int reunionId, ReunionUpdateRequest request, int userId)
        {
            if (string.IsNullOrWhiteSpace(request.Tema))
                throw new AbrilException("El tema de la reunión es obligatorio.", 400);
            ValidarHoras(request.HoraInicio, request.HoraFin);
            return _repository.Update(reunionId, request, userId);
        }

        public Task Reprogramar(int reunionId, ReunionReprogramarRequest request, int userId)
        {
            ValidarHoras(request.HoraInicio, request.HoraFin);
            return _repository.Reprogramar(reunionId, request, userId);
        }

        public Task CambiarEstado(int reunionId, ReunionCambiarEstadoRequest request, int userId)
        {
            if (string.IsNullOrWhiteSpace(request.Estado))
                throw new AbrilException("Debe indicar el estado destino.", 400);
            return _repository.CambiarEstado(reunionId, request.Estado.Trim().ToUpperInvariant(), userId);
        }

        public Task Eliminar(int reunionId, int userId)
            => _repository.Eliminar(reunionId, userId);

        public Task<int> CrearAcuerdo(int reunionId, ReunionAcuerdoRequest request, int userId)
        {
            ValidarAcuerdo(request);
            return _repository.CrearAcuerdo(reunionId, request, userId);
        }

        public Task ActualizarAcuerdo(int reunionAcuerdoId, ReunionAcuerdoRequest request, int userId)
        {
            ValidarAcuerdo(request);
            return _repository.ActualizarAcuerdo(reunionAcuerdoId, request, userId);
        }

        public Task EliminarAcuerdo(int reunionAcuerdoId, int userId)
            => _repository.EliminarAcuerdo(reunionAcuerdoId, userId);

        public async Task<List<ReunionArchivoDto>> SubirArchivos(int reunionId, IFormFileCollection files, int userId)
        {
            if (files is null || files.Count == 0)
                throw new AbrilException("No se adjuntó ningún archivo.", 400);
            if (files.Count > MaxFilesPorSubida)
                throw new AbrilException($"Solo se pueden subir hasta {MaxFilesPorSubida} archivos por vez.", 400);

            foreach (var file in files)
            {
                if (file.Length == 0)
                    throw new AbrilException($"El archivo \"{file.FileName}\" está vacío.", 400);
                if (file.Length > MaxFileSizeBytes)
                    throw new AbrilException($"El archivo \"{file.FileName}\" supera el tamaño máximo permitido (25 MB).", 400);
                var extension = Path.GetExtension(file.FileName);
                if (!ExtensionesPermitidas.Contains(extension))
                    throw new AbrilException($"El tipo de archivo \"{extension}\" no está permitido.", 400);
            }

            // Si hay una carpeta de SharePoint configurada, los adjuntos van ahí (dentro de una
            // subcarpeta por reunión); si no, se usa el storage por defecto (Azure Blob).
            var destino = await _repository.GetFolderDestination();
            if (destino != null)
                return await SubirArchivosASharePoint(reunionId, files, destino.Value, userId);

            var container = _containerResolver.GetActasReunionContainerName();

            var streams = new List<Stream>();
            try
            {
                var toUpload = new List<(Stream Stream, string FileName)>();
                foreach (var file in files)
                {
                    var stream = file.OpenReadStream();
                    streams.Add(stream);
                    var extension = Path.GetExtension(file.FileName);
                    toUpload.Add((stream, $"{Guid.NewGuid()}{extension}"));
                }

                var urls = await _fileStorageService.UploadFilesAsync(toUpload, container);

                var archivos = urls
                    .Select((url, i) => (Url: url, OriginalFileName: (string?)files[i].FileName))
                    .ToList();

                return await _repository.AgregarArchivos(reunionId, archivos, userId);
            }
            finally
            {
                foreach (var stream in streams)
                    stream.Dispose();
            }
        }

        /// <summary>
        /// Sube los adjuntos a la carpeta de SharePoint configurada, dentro de una subcarpeta
        /// "{PROYECTO} - REUNIÓN N° {numero}" (se crea si no existe). Guarda el webUrl como URL.
        /// </summary>
        private async Task<List<ReunionArchivoDto>> SubirArchivosASharePoint(
            int reunionId,
            IFormFileCollection files,
            (string DriveId, string FolderId) destino,
            int userId)
        {
            var (projectDescription, numero) = await _repository.GetDatosCarpetaReunion(reunionId);
            var nombreSubcarpeta = SanitizeSharePointName($"{projectDescription} - REUNIÓN N° {numero}");

            var subcarpetaId = await _sharePointService.EnsureChildFolderAsync(
                destino.DriveId, destino.FolderId, nombreSubcarpeta);

            var archivos = new List<(string Url, string? OriginalFileName)>();
            foreach (var file in files)
            {
                using var stream = file.OpenReadStream();
                var resultado = await _sharePointService.UploadToOneDriveFolderAsync(
                    destino.DriveId,
                    subcarpetaId,
                    SanitizeSharePointName(file.FileName),
                    stream,
                    string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    autoRenameOnLock: true);

                if (resultado?.WebUrl == null)
                    throw new AbrilException($"No se pudo subir el archivo \"{file.FileName}\" a SharePoint.", 500);

                archivos.Add((resultado.WebUrl, file.FileName));
            }

            return await _repository.AgregarArchivos(reunionId, archivos, userId);
        }

        /// <summary>Reemplaza los caracteres no permitidos por SharePoint en nombres de carpeta/archivo.</summary>
        private static string SanitizeSharePointName(string name)
        {
            var invalid = new[] { '"', '*', ':', '<', '>', '?', '/', '\\', '|' };
            var sanitized = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(sanitized) ? "archivo" : sanitized;
        }

        // ── Carpeta de SharePoint para adjuntos ──────────────────────────────
        public Task<ReunionFolderDto?> GetFolder()
            => _repository.GetFolderSingleton();

        public async Task<ReunionFolderDto> SaveFolder(ReunionFolderSaveDto dto, int userId)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.LinkUrl))
                throw new AbrilException("Debe ingresar el link de la carpeta.");

            var link = dto.LinkUrl.Trim();

            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new AbrilException("El link no es una URL válida.");

            if (!_allowedHosts.Contains(uri.Host.ToLowerInvariant()))
                throw new AbrilException(
                    $"El link no pertenece a la organización. Solo se permiten enlaces de: {string.Join(", ", _allowedHosts)}.");

            var resolved = await _sharePointService.ResolveSharePointFolderUrlAsync(link)
                ?? throw new AbrilException(
                    "No se pudo acceder a la carpeta del link. Verifique que el enlace apunte a una carpeta/biblioteca y que la aplicación tenga acceso.");

            if (!resolved.IsFolder)
                throw new AbrilException("El link debe apuntar a una carpeta, no a un archivo.");

            await _repository.UpsertFolder(link, resolved.DriveId, resolved.ItemId, resolved.Name, resolved.WebUrl, userId);

            return await _repository.GetFolderSingleton()
                ?? throw new AbrilException("No se pudo guardar la carpeta.", 500);
        }

        public Task EliminarArchivo(int reunionArchivoId, int userId)
            => _repository.EliminarArchivo(reunionArchivoId, userId);

        private static void ValidarAcuerdo(ReunionAcuerdoRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Descripcion))
                throw new AbrilException("La descripción del acuerdo es obligatoria.", 400);
        }

        private static void ValidarHoras(TimeOnly? inicio, TimeOnly? fin)
        {
            if (inicio.HasValue && fin.HasValue && fin.Value <= inicio.Value)
                throw new AbrilException("La hora de término debe ser mayor a la hora de inicio.", 400);
        }
    }
}
