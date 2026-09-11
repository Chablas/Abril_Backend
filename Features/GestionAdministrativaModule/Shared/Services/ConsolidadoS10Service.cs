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
    /// la contraparte de las planillas en el S10 y así no hace falta configurar una carpeta extra
    /// por entorno.
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

        public async Task<ConsolidadoS10Dto> UploadParaRendiciones(
            IReadOnlyCollection<int> rendicionIds,
            IFormFile file,
            decimal montoTotal,
            string numeroGuia,
            int userId,
            int? ownerUserId = null)
        {
            if (file == null || file.Length == 0)
                throw new AbrilException("No se recibió el archivo del consolidado.", 400);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".pdf")
                throw new AbrilException("El Consolidado del S10 debe ser un archivo PDF.", 400);

            var guia = (numeroGuia ?? string.Empty).Trim();
            if (guia.Length == 0)
                throw new AbrilException("Falta el número de guía del Consolidado del S10.", 400);
            if (guia.Length > 60)
                throw new AbrilException("El número de guía no puede pasar de 60 caracteres.", 400);

            // La columna es numeric(12,2): se redondea antes de comparar y de guardar, así el
            // monto que se contrasta es el mismo que queda en la base.
            var monto = decimal.Round(montoTotal, 2, MidpointRounding.AwayFromZero);
            if (monto <= 0m)
                throw new AbrilException("El monto total del Consolidado del S10 tiene que ser mayor que 0.", 400);

            var ids = (rendicionIds ?? Array.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            if (ids.Count == 0)
                throw new AbrilException("Selecciona al menos una rendición.", 400);

            using var ctx = _factory.CreateDbContext();

            var planillas = await ctx.GaRendicion
                .Where(r => ids.Contains(r.Id))
                .Select(r => new { r.Id, r.Codigo, r.EstadoPrimeraRevisionId })
                .ToListAsync();

            if (planillas.Count != ids.Count)
                throw new AbrilException(
                    ids.Count == 1
                        ? "La planilla de rendición no existe."
                        : "Alguna de las planillas de rendición seleccionadas ya no existe.", 404);

            var codigo = planillas.ToDictionary(
                p => p.Id, p => PlanillaRendicionHelper.CodigoRendicion(p.Codigo, p.Id));

            string Codigos(IEnumerable<int> deIds) => ConsolidadoS10Agrupacion.Enumerar(
                deIds.Select(id => codigo.TryGetValue(id, out var c) ? c : $"#{id}"));

            // RG-35: el Consolidado del S10 se habilita SOLO después de que la jefatura apruebe la
            // primera revisión. Vale igual para el consolidador que lo sube en nombre del
            // trabajador: lo que falta no es el permiso, es el registro en el S10, que recién se hace
            // con la planilla aprobada. El mensaje dice en qué estado está para no dejarlo adivinando.
            var sinAprobar = planillas
                .Where(p => p.EstadoPrimeraRevisionId != EstadosSalida.PrimeraRevision.Aprobada)
                .ToList();
            if (sinAprobar.Count > 0)
                throw new AbrilException(
                    ids.Count == 1
                        ? MensajeSinPrimeraRevision(sinAprobar[0].EstadoPrimeraRevisionId)
                        : $"{Codigos(sinAprobar.Select(p => p.Id))} todavía no "
                          + (sinAprobar.Count == 1 ? "tiene" : "tienen")
                          + " la primera revisión aprobada: el Consolidado del S10 se habilita recién "
                          + "cuando la jefatura la aprueba.",
                    409);

            // Guard de propiedad (autoservicio): cada planilla tiene que incluir alguna salida del
            // trabajador de ese usuario. Una planilla puede agrupar a varios trabajadores cuando la
            // genera el revisor desde Gestión de Salidas, así que basta con tener una adentro.
            if (ownerUserId.HasValue)
            {
                var propias = await (
                    from s in ctx.GaSolicitudSalida
                    join w in ctx.Worker on s.WorkerId equals w.Id
                    join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                    where s.RendicionId != null && ids.Contains(s.RendicionId.Value)
                       && per.UserId == ownerUserId.Value
                    select s.RendicionId!.Value
                ).Distinct().ToListAsync();

                if (ids.Any(id => !propias.Contains(id)))
                    throw new AbrilException(
                        "Solo puedes adjuntar el Consolidado del S10 de tus propias planillas.", 403);
            }

            // ── Qué planillas pueden ir juntas (ver ConsolidadoS10Agrupacion) ──
            var actuales = await ConsolidadoS10Loader.LoadPorRendicionAsync(ctx, ids);
            var agrupables = await ConsolidadoS10Agrupacion.LoadPlanillasAsync(
                ctx,
                ids.Concat(actuales.Values.SelectMany(c => c.Rendiciones).Select(r => r.Id))
                   .Distinct()
                   .ToList());

            // Con el reembolso ya decidido, el consolidado quedó firmado junto con la planilla:
            // cambiarlo dejaría a Tesorería con un respaldo distinto del que aprobó la jefatura.
            var cerradas = ids
                .Where(id => agrupables.TryGetValue(id, out var p) && !p.ReembolsoAbierto)
                .ToList();
            if (cerradas.Count > 0)
                throw new AbrilException(
                    $"{Codigos(cerradas)} ya {(cerradas.Count == 1 ? "tiene" : "tienen")} el reembolso "
                    + "decidido: su Consolidado del S10 ya no se puede cambiar.", 409);

            // El documento se reemplaza entero: las demás planillas abiertas de un consolidado
            // compartido tienen que venir en la misma subida.
            var faltantes = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (!actuales.TryGetValue(id, out var actual)) continue;

                foreach (var otra in ConsolidadoS10Agrupacion.Conjunto(id, actual, agrupables))
                    if (!ids.Contains(otra))
                        faltantes.Add(actual.Rendiciones.First(r => r.Id == otra).Codigo);
            }
            if (faltantes.Count > 0)
                throw new AbrilException(
                    $"El Consolidado del S10 actual también cubre {ConsolidadoS10Agrupacion.Enumerar(faltantes)}, "
                    + "que " + (faltantes.Count == 1 ? "sigue" : "siguen") + " con el reembolso por decidir: "
                    + "el documento se reemplaza para todas sus rendiciones a la vez.", 409);

            // Un solo registro del S10 es de una sola empresa.
            var errorRazonSocial = await ConsolidadoS10Agrupacion.ValidarRazonSocialAsync(
                ctx, ids, agrupables, codigo);
            if (errorRazonSocial != null)
                throw new AbrilException(errorRazonSocial, 400);

            // El consolidado es UN registro en el S10 y cubre las planillas completas, así que su
            // importe se contrasta contra el total de TODAS sus salidas —no contra el subconjunto
            // que ve quien lo sube—. Se valida acá, antes de tocar SharePoint: un archivo subido con
            // el monto mal solo dejaría basura en la carpeta.
            var totales = await TotalPlanillaLoader.LoadAsync(ctx, ids);
            var totalPlanillas = decimal.Round(totales.Values.Sum(), 2, MidpointRounding.AwayFromZero);

            if (monto != totalPlanillas)
                throw new AbrilException(
                    $"El monto total del Consolidado del S10 (S/ {monto:N2}) no coincide con el monto "
                    + (ids.Count == 1 ? "de la planilla" : $"de las {ids.Count} planillas")
                    + $" (S/ {totalPlanillas:N2}). Corrígelo antes de adjuntarlo.", 400);

            // Si el Coordinador ERP ANULÓ el registro del S10, la guía anterior quedó inservible y
            // hay que sacar una nueva (HU-ERP-03 / CA-19). Se valida acá, con el resto de lo que se
            // mira antes de tocar SharePoint: dejar pasar la guía vieja mandaría a la jefatura a
            // revisar un consolidado que el S10 ya no reconoce.
            var correcciones = await ctx.GaCorreccionS10
                .Where(c => c.State && ids.Contains(c.RendicionId))
                .ToListAsync();

            var anulada = correcciones.FirstOrDefault(c =>
                c.GuiaAnulada
                && !string.IsNullOrWhiteSpace(c.NumeroGuia)
                && string.Equals(c.NumeroGuia!.Trim(), guia, StringComparison.OrdinalIgnoreCase));
            if (anulada != null)
                throw new AbrilException(
                    $"La guía {anulada.NumeroGuia} se anuló en el S10 y no se puede reutilizar. " +
                    "Genera una guía nueva y vuelve a adjuntar el consolidado.", 400);

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
            var filename = ids.Count == 1
                ? $"Consolidado_S10_r{ids[0]}_{stamp}.pdf"
                : $"Consolidado_S10_r{ids[0]}_{ids.Count}planillas_{stamp}.pdf";

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
                _logger.LogError(ex, "Falló la subida del Consolidado del S10 (rendiciones={RendicionIds})",
                    string.Join(",", ids));
                throw new AbrilException("Error al subir el Consolidado del S10 a SharePoint.", 502);
            }

            // ── Persistir ────────────────────────────────────────────────────
            var now = DateTimeOffset.UtcNow;

            // Los vínculos vigentes de estas planillas pasan al consolidado nuevo.
            var vinculosAnteriores = await ctx.GaConsolidadoS10Rendicion
                .Where(v => v.State && ids.Contains(v.RendicionId))
                .ToListAsync();

            // Un consolidado anterior se da de baja cuando se queda sin planillas. Si todavía cubre
            // alguna con el reembolso ya decidido, sigue vivo para ella: es el documento que se firmó.
            var anterioresIds = vinculosAnteriores.Select(v => v.ConsolidadoS10Id).Distinct().ToList();
            var siguenCubriendo = await ctx.GaConsolidadoS10Rendicion
                .Where(v => v.State
                         && anterioresIds.Contains(v.ConsolidadoS10Id)
                         && !ids.Contains(v.RendicionId))
                .Select(v => v.ConsolidadoS10Id)
                .Distinct()
                .ToListAsync();
            var anterioresDeBaja = await ctx.GaConsolidadoS10
                .Where(c => c.State && anterioresIds.Contains(c.Id) && !siguenCubriendo.Contains(c.Id))
                .ToListAsync();

            var nuevo = new GaConsolidadoS10
            {
                SolicitudId  = null,
                PdfUrl       = pdfUrl,
                PdfItemId    = pdfItemId,
                PdfDriveId   = carpeta.DriveId,
                PdfFilename  = filename,
                MontoTotal   = monto,
                NumeroGuia   = guia,
                UploadedById = userId,
                UploadedAt   = now,
                State        = true,
            };

            // Subsanación: si la jefatura había OBSERVADO el reembolso, adjuntar otra vez el
            // consolidado es exactamente lo que se le pidió al trabajador, así que el reembolso
            // vuelve a Pendiente y le reaparece al revisor. La observación NO se borra: sigue
            // siendo lo que se observó y el jefe la necesita para contrastar.
            //
            // El archivo cubre las planillas enteras, así que reabre todas sus salidas observadas.
            var reabrir = await ctx.GaSolicitudSalida
                .Where(x => x.RendicionId != null && ids.Contains(x.RendicionId.Value)
                         && x.EstadoReembolsoId == EstadosSalida.Reembolso.Observado)
                .ToListAsync();

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await ctx.Database.BeginTransactionAsync();

                // Los índices únicos parciales se validan por sentencia: hay que dar de baja los
                // vínculos anteriores y guardar ANTES de crear los nuevos, o el INSERT choca con el
                // vínculo vigente de la misma planilla. Ese mismo guardado le da id al consolidado.
                foreach (var v in vinculosAnteriores) v.State = false;
                foreach (var c in anterioresDeBaja)   c.State = false;
                ctx.GaConsolidadoS10.Add(nuevo);
                await ctx.SaveChangesAsync();

                ctx.GaConsolidadoS10Rendicion.AddRange(ids.Select(id => new GaConsolidadoS10Rendicion
                {
                    ConsolidadoS10Id = nuevo.Id,
                    RendicionId      = id,
                    State            = true,
                }));

                foreach (var r in reabrir)
                {
                    r.EstadoReembolsoId = EstadosSalida.Reembolso.Pendiente;
                    r.UpdatedAt         = now;
                }

                // Y la gestión con el ERP se cierra: recargar el consolidado ES el final de ese
                // ciclo. Va en la MISMA transacción que reabre el reembolso porque son la misma
                // subsanación — dejar la corrección viva sobre un reembolso ya Pendiente
                // bloquearía la próxima si la jefatura vuelve a observar.
                //
                // Se cierra incluso si el ERP todavía no la había atendido: el trabajador puede
                // haber resuelto el S10 por otro lado, y en ese caso el pedido ya no tiene sentido
                // (desaparece de la bandeja del ERP en vez de quedar ahí sin dueño).
                foreach (var c in correcciones)
                {
                    c.State           = false;
                    c.UpdatedDateTime = now;
                }

                await ctx.SaveChangesAsync();
                await tx.CommitAsync();
            });

            var dto = ConsolidadoS10Loader.ToDto(nuevo);
            dto.Rendiciones = ids
                .Select(id => new ConsolidadoS10RendicionDto { Id = id, Codigo = codigo[id] })
                .OrderBy(r => r.Codigo, StringComparer.Ordinal)
                .ToList();
            return dto;
        }

        /// <summary>Por qué una planilla sin la primera revisión aprobada todavía no admite el consolidado.</summary>
        private static string MensajeSinPrimeraRevision(int estadoPrimeraRevisionId) =>
            estadoPrimeraRevisionId switch
            {
                EstadosSalida.PrimeraRevision.Borrador =>
                    "Esta rendición todavía no se envió a primera revisión: el Consolidado del S10 "
                    + "se habilita cuando la jefatura la apruebe.",
                EstadosSalida.PrimeraRevision.EnRevision =>
                    "Esta rendición está en primera revisión: el Consolidado del S10 se habilita "
                    + "cuando la jefatura la apruebe.",
                _ =>
                    "Esta rendición está observada: primero hay que corregir las capturas y los "
                    + "montos, volver a generarla y esperar la aprobación de la primera revisión.",
            };

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
    }
}
