using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Implementación de <see cref="IConsolidadoS10Service"/>. El PDF se guarda en la misma carpeta
    /// de SharePoint que las planillas de rendición (<c>ga_rendicion_folder</c>): el consolidado es
    /// la contraparte de la planilla en el S10 y así no hace falta configurar una carpeta extra por
    /// entorno.
    /// </summary>
    public class ConsolidadoS10Service : IConsolidadoS10Service
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IGraphSharePointService _sharePointService;
        private readonly ILogger<ConsolidadoS10Service> _logger;

        public ConsolidadoS10Service(
            IDbContextFactory<AppDbContext> factory,
            IGraphSharePointService sharePointService,
            ILogger<ConsolidadoS10Service> logger)
        {
            _factory = factory;
            _sharePointService = sharePointService;
            _logger = logger;
        }

        public async Task<ConsolidadoS10Dto> UploadParaRendicion(
            int rendicionId,
            IFormFile file,
            int userId,
            int? ownerUserId = null)
        {
            if (file == null || file.Length == 0)
                throw new AbrilException("No se recibió el archivo del consolidado.", 400);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".pdf")
                throw new AbrilException("El Consolidado del S10 debe ser un archivo PDF.", 400);

            using var ctx = _factory.CreateDbContext();

            var existe = await ctx.GaRendicion.AnyAsync(r => r.Id == rendicionId);
            if (!existe)
                throw new AbrilException("La planilla de rendición no existe.", 404);

            // Guard de propiedad (autoservicio): la planilla tiene que incluir alguna salida del
            // trabajador de ese usuario. Una planilla puede agrupar a varios trabajadores cuando la
            // genera el revisor desde Gestión de Salidas, así que basta con tener una adentro.
            if (ownerUserId.HasValue)
            {
                var esPropia = await (
                    from s in ctx.GaSolicitudSalida
                    join w in ctx.Worker on s.WorkerId equals w.Id
                    join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                    where s.RendicionId == rendicionId && per.UserId == ownerUserId.Value
                    select s.Id
                ).AnyAsync();
                if (!esPropia)
                    throw new AbrilException(
                        "Solo puedes adjuntar el Consolidado del S10 de tus propias planillas.", 403);
            }

            // ── Carpeta destino (la misma de las planillas de rendición) ──────
            var folderUrl = await ctx.GaRendicionFolder
                .Where(f => f.State && f.Active)
                .OrderBy(f => f.GaRendicionFolderId)
                .Select(f => f.LinkUrl)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(folderUrl))
                throw new AbrilException(
                    "No se ha configurado la carpeta de SharePoint donde guardar los consolidados del S10. " +
                    "Pide al administrador registrarla en la tabla ga_rendicion_folder.", 409);

            var carpeta = await _sharePointService.ResolveSharePointFolderUrlAsync(folderUrl);
            if (carpeta == null || !carpeta.IsFolder)
                throw new AbrilException("No se pudo resolver la carpeta de consolidados del S10 en SharePoint.", 502);

            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var filename = $"Consolidado_S10_r{rendicionId}_{stamp}.pdf";

            string pdfUrl;
            string? pdfItemId;
            try
            {
                using var stream = file.OpenReadStream();
                var result = await _sharePointService.UploadToOneDriveFolderAsync(
                    carpeta.DriveId, carpeta.ItemId, filename, stream,
                    "application/pdf",
                    autoRenameOnLock: true);

                if (result?.WebUrl is null)
                    throw new AbrilException("No se pudo subir el Consolidado del S10 a SharePoint (respuesta vacía).", 502);

                pdfUrl = result.WebUrl;
                pdfItemId = result.ItemId;
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la subida del Consolidado del S10 (rendicion={RendicionId})", rendicionId);
                throw new AbrilException("Error al subir el Consolidado del S10 a SharePoint.", 502);
            }

            // ── Persistir: el anterior vigente pasa a state = false (auditoría) ──
            var now = DateTimeOffset.UtcNow;

            var vigentes = await ctx.GaConsolidadoS10
                .Where(c => c.State && c.RendicionId == rendicionId)
                .ToListAsync();

            var nuevo = new GaConsolidadoS10
            {
                RendicionId  = rendicionId,
                SolicitudId  = null,
                PdfUrl       = pdfUrl,
                PdfItemId    = pdfItemId,
                PdfDriveId   = carpeta.DriveId,
                PdfFilename  = filename,
                UploadedById = userId,
                UploadedAt   = now,
                State        = true,
            };

            // Subsanación: si el jefe había RECHAZADO el reembolso, adjuntar otra vez el
            // consolidado es exactamente lo que se le pidió al trabajador, así que el reembolso
            // vuelve a Pendiente y le reaparece al revisor. La observación NO se borra: sigue
            // siendo lo que se observó y el jefe la necesita para contrastar.
            //
            // El archivo cubre TODA la planilla, así que reabre todas sus salidas rechazadas.
            var reabrir = await ctx.GaSolicitudSalida
                .Where(x => x.RendicionId == rendicionId
                         && x.EstadoReembolsoId == EstadosSalida.Reembolso.Rechazado)
                .ToListAsync();

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await ctx.Database.BeginTransactionAsync();
                // Los índices únicos parciales se validan por sentencia: hay que dar de baja el
                // anterior y guardar ANTES de insertar el nuevo, o el INSERT choca con el vigente.
                if (vigentes.Count > 0)
                {
                    foreach (var v in vigentes) v.State = false;
                    await ctx.SaveChangesAsync();
                }
                ctx.GaConsolidadoS10.Add(nuevo);

                foreach (var r in reabrir)
                {
                    r.EstadoReembolsoId = EstadosSalida.Reembolso.Pendiente;
                    r.UpdatedAt         = now;
                }

                await ctx.SaveChangesAsync();
                await tx.CommitAsync();
            });

            return ToDto(nuevo);
        }

        public async Task<ConsolidadoS10Dto?> GetForSolicitud(int solicitudId)
        {
            var map = await GetForSolicitudes(new[] { solicitudId });
            return map.TryGetValue(solicitudId, out var dto) ? dto : null;
        }

        public async Task<Dictionary<int, ConsolidadoS10Dto>> GetForSolicitudes(IEnumerable<int> solicitudIds)
        {
            var ids = solicitudIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            // rendicion_id de cada solicitud, para resolver el consolidado de la planilla.
            var rendicionPorSolicitud = await ctx.GaSolicitudSalida
                .Where(s => ids.Contains(s.Id))
                .Select(s => new { s.Id, s.RendicionId })
                .ToListAsync();

            return await ConsolidadoS10Loader.LoadAsync(
                ctx, rendicionPorSolicitud.ToDictionary(x => x.Id, x => x.RendicionId));
        }

        public async Task<Dictionary<int, ConsolidadoS10Dto>> GetForRendiciones(IEnumerable<int> rendicionIds)
        {
            var ids = rendicionIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();
            return await ConsolidadoS10Loader.LoadPorRendicionAsync(ctx, ids);
        }

        private static ConsolidadoS10Dto ToDto(GaConsolidadoS10 c) => ConsolidadoS10Loader.ToDto(c);
    }
}
