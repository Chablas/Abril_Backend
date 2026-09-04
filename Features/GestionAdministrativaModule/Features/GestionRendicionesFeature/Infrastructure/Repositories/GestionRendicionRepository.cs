using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Infrastructure.Repositories
{
    /// <summary>
    /// Las planillas de rendición desde el lado del revisor. La visibilidad es exactamente la de
    /// Gestión de Salidas (misma regla, <see cref="SalidaVisibilidadFilter"/>): esta pantalla
    /// muestra las mismas salidas, agrupadas por planilla.
    /// </summary>
    public class GestionRendicionRepository : IGestionRendicionRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public GestionRendicionRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<List<GestionRendicionListItemDto>> GetAll(GestionRendicionFiltersDto filters)
        {
            using var ctx = _factory.CreateDbContext();

            var planillas = await PlanillaRendicionLoader.LoadAsync(ctx, SalidasVisibles(ctx, filters));
            var propios   = await MisWorkerIdsAsync(ctx, filters.CurrentUserId);
            var porDecidir = await IdsConReembolsoRevisableAsync(
                ctx, planillas.SelectMany(p => p.Salidas).Select(s => s.Id).ToList());

            var items = planillas.Select(p => Armar(p, propios, porDecidir)).ToList();
            return Filtrar(items, filters);
        }

        public async Task<GestionRendicionDetalleDto?> GetDetalle(int rendicionId, GestionRendicionFiltersDto scope)
        {
            using var ctx = _factory.CreateDbContext();

            var query = SalidasVisibles(ctx, scope).Where(s => s.RendicionId == rendicionId);
            var planillas = await PlanillaRendicionLoader.LoadAsync(ctx, query, conDetalle: true);
            if (planillas.Count == 0) return null;

            var planilla   = planillas[0];
            var propios    = await MisWorkerIdsAsync(ctx, scope.CurrentUserId);
            var porDecidir = await IdsConReembolsoRevisableAsync(ctx, planilla.Salidas.Select(s => s.Id).ToList());

            var cabecera = Armar(planilla, propios, porDecidir);
            var detalle  = new GestionRendicionDetalleDto();
            CopiarCabecera(cabecera, detalle);

            detalle.Salidas = planilla.Salidas
                .Select(s => new GestionRendicionSalidaDto
                {
                    Id                   = s.Id,
                    Codigo               = s.Codigo,
                    Trabajador           = s.Trabajador,
                    Area                 = s.Area,
                    FechaSalida          = s.FechaSalida,
                    Motivo               = s.Motivo,
                    LugarOrigen          = s.LugarOrigen,
                    LugarDestino         = s.LugarDestino,
                    TrayectosCount       = s.TrayectosCount,
                    Monto                = s.Monto,
                    EstadoReembolso      = s.EstadoReembolso,
                    ObservacionReembolso = s.ObservacionReembolso,
                    PorDecidir           = porDecidir.Contains(s.Id),
                    EsPropia             = propios.Contains(s.WorkerId),
                })
                .ToList();

            return detalle;
        }

        public async Task<GestionRendicionFilterDataDto> GetFilterData(GestionRendicionFiltersDto scope)
        {
            using var ctx = _factory.CreateDbContext();

            var seesAll  = scope.SeesAll;
            var areaIds  = scope.VisibleAreaScopeIds ?? new List<int>();
            var uid      = scope.CurrentUserId;

            // Trabajadores con al menos una salida YA RENDIDA: los que no rindieron nada todavía
            // no tienen planilla, y ofrecerlos en el filtro sería ofrecer un resultado vacío.
            var workerIds = await ctx.GaSolicitudSalida
                .Where(s => s.RendicionId != null)
                .Select(s => s.WorkerId)
                .Distinct()
                .ToListAsync();

            var trabajadoresQuery = ctx.Worker.Where(w => workerIds.Contains(w.Id));
            if (!seesAll)
            {
                trabajadoresQuery = trabajadoresQuery.Where(w =>
                    (w.PuestoCatalogo!.AreaDestinoScopeId != null
                     && areaIds.Contains(w.PuestoCatalogo.AreaDestinoScopeId!.Value))
                    || (uid != null && ctx.Person.Any(p => p.PersonId == w.PersonId && p.UserId == uid)));
            }

            var trabajadores = await (
                from w   in trabajadoresQuery
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                orderby per != null ? per.FullName : null
                select new TrabajadorOptionDto
                {
                    WorkerId       = w.Id,
                    NombreCompleto = per != null ? (per.FullName ?? "[Sin nombre]") : "[Sin nombre]",
                }
            ).ToListAsync();

            var areaTree = await (
                from s  in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.State && ai.State && at.State
                   && (seesAll || areaIds.Contains(s.AreaScopeId))
                orderby s.DisplayOrder
                select new AreaNodeDto
                {
                    AreaScopeId       = s.AreaScopeId,
                    AreaItemId        = s.AreaItemId,
                    AreaItemName      = ai.AreaItemName,
                    AreaTypeId        = ai.AreaTypeId,
                    AreaTypeName      = at.AreaTypeName,
                    AreaScopeParentId = s.AreaScopeParentId,
                    DisplayOrder      = s.DisplayOrder,
                }
            ).ToListAsync();

            // El periodo de una planilla es el mes de su salida más antigua — el mismo criterio que
            // usa la tabla, si no el filtro dejaría fuera planillas que sí muestra.
            var fechas = await SalidasVisibles(ctx, SoloVisibilidad(scope))
                .Where(s => s.RendicionId != null)
                .Select(s => new { RendicionId = s.RendicionId!.Value, s.FechaSalida })
                .ToListAsync();

            var periodos = fechas
                .GroupBy(x => x.RendicionId)
                .Select(g => g.Min(x => x.FechaSalida))
                .Select(f => (f.Year, f.Month))
                .Distinct()
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .Select(p => new PeriodoRendicionOptionDto
                {
                    Anio  = p.Year,
                    Mes   = p.Month,
                    Label = PlanillaRendicionHelper.EtiquetaMes(p.Year, p.Month),
                })
                .ToList();

            return new GestionRendicionFilterDataDto
            {
                Trabajadores = trabajadores,
                AreaTree     = areaTree,
                Periodos     = periodos,
            };
        }

        public async Task<List<int>> ResolverSolicitudIds(
            IEnumerable<int> rendicionIds, IEnumerable<int> solicitudIds, GestionRendicionFiltersDto scope)
        {
            var rIds = rendicionIds?.Distinct().ToList() ?? new List<int>();
            var sIds = solicitudIds?.Distinct().ToList() ?? new List<int>();
            if (rIds.Count == 0 && sIds.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            // El recorte por visibilidad se aplica igual a las dos vías: mandar un rendicion_id no
            // puede alcanzar salidas de áreas que el usuario no ve, aunque cuelguen de la misma
            // planilla.
            return await SalidasVisibles(ctx, SoloVisibilidad(scope))
                .Where(s => s.RendicionId != null
                         && (rIds.Contains(s.RendicionId!.Value) || sIds.Contains(s.Id)))
                .Select(s => s.Id)
                .ToListAsync();
        }

        // ══ Reembolso ═══════════════════════════════════════════════════════

        public async Task<List<int>> DecidirReembolso(
            IEnumerable<int> ids, bool aprobar, string? observacion, int reviewerUserId)
        {
            var idsList = ids?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            if (!aprobar && string.IsNullOrWhiteSpace(observacion))
                throw new AbrilException("Para rechazar un reembolso hay que escribir la observación.", 400);

            using var ctx = _factory.CreateDbContext();

            var elegibles = await IdsConReembolsoRevisableAsync(ctx, idsList);
            if (elegibles.Count == 0)
                throw new AbrilException(
                    "Ninguna de las salidas seleccionadas tiene un reembolso por decidir: deben estar " +
                    "rendidas y con el Consolidado del S10 adjunto.", 400);

            var solicitudes = await ctx.GaSolicitudSalida
                .Where(s => elegibles.Contains(s.Id))
                .ToListAsync();

            // Nadie decide el reembolso de sus propias salidas (salvo Gerente), misma regla que la
            // aprobación de la salida. El chequeo se hace con UNA consulta para todo el lote.
            var misWorkers = await (
                from w in ctx.Worker
                join per in ctx.Person on w.PersonId equals per.PersonId
                where per.UserId == reviewerUserId
                select new
                {
                    w.Id,
                    CategoriaId = w.PuestoCatalogo != null ? w.PuestoCatalogo.CategoriaId : (int?)null
                }
            ).ToListAsync();

            var misWorkerIds = misWorkers.Select(x => x.Id).ToHashSet();
            var esGerente    = misWorkers.Any(x => x.CategoriaId == CategoriaIds.Gerente);

            if (!esGerente && solicitudes.Any(x => misWorkerIds.Contains(x.WorkerId)))
                throw new AbrilException(
                    "No puedes decidir el reembolso de tus propias salidas — deselecciónalas primero.", 403);

            var now  = DateTimeOffset.UtcNow;
            var obs  = aprobar ? null : observacion!.Trim();
            var next = aprobar ? EstadosSalida.Reembolso.Aprobado : EstadosSalida.Reembolso.Rechazado;

            foreach (var s in solicitudes)
            {
                s.EstadoReembolsoId      = next;
                s.ReembolsoDecididoPorId = reviewerUserId;
                s.ReembolsoDecididoAt    = now;
                s.UpdatedAt              = now;
                // Al aprobar se limpia la observación: ya no hay nada que subsanar. Al rechazar se
                // reemplaza por la nueva.
                s.ObservacionReembolso   = obs;
            }

            await ctx.SaveChangesAsync();
            return solicitudes.Select(s => s.Id).ToList();
        }

        public async Task<List<RendicionPorFirmarDto>> GetRendicionesPorFirmar(IEnumerable<int> ids)
        {
            var idsList = ids?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            var filas = await (
                from s in ctx.GaSolicitudSalida
                join r in ctx.GaRendicion on s.RendicionId equals r.Id
                where idsList.Contains(s.Id)
                   && s.EstadoReembolsoId == EstadosSalida.Reembolso.Aprobado
                   && s.EstadoRendicionId == EstadosSalida.Rendicion.Rendido
                select new
                {
                    SolicitudId = s.Id,
                    r.Id, r.PdfUrl, r.PdfFilename, r.PdfFirmadoUrl,
                }
            ).ToListAsync();

            return filas
                .GroupBy(x => x.Id)
                .Select(g => new RendicionPorFirmarDto
                {
                    RendicionId   = g.Key,
                    PdfUrl        = g.First().PdfUrl,
                    PdfFilename   = g.First().PdfFilename,
                    PdfFirmadoUrl = g.First().PdfFirmadoUrl,
                    SolicitudIds  = g.Select(x => x.SolicitudId).ToList(),
                })
                .ToList();
        }

        public async Task MarcarFirmadas(
            int rendicionId, IEnumerable<int> solicitudIds, int userId,
            string? pdfUrl, string? pdfItemId, string? pdfFilename)
        {
            var idsList = solicitudIds?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return;

            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;

            var rendicion = await ctx.GaRendicion.FirstOrDefaultAsync(r => r.Id == rendicionId)
                ?? throw new AbrilException("La planilla de rendición no existe.", 404);

            // Solo se guarda el archivo si esta firma lo generó. Si la planilla ya venía firmada no
            // se pisa: el PDF firmado que vale es el primero, y lo que falta es mover el estado de
            // las salidas que aún no estaban firmadas.
            if (!string.IsNullOrWhiteSpace(pdfUrl))
            {
                rendicion.PdfFirmadoUrl      = pdfUrl;
                rendicion.PdfFirmadoItemId   = pdfItemId;
                rendicion.PdfFirmadoFilename = pdfFilename;
                rendicion.FirmadoPorId       = userId;
                rendicion.FirmadoAt          = now;
            }

            var solicitudes = await ctx.GaSolicitudSalida
                .Where(s => idsList.Contains(s.Id)
                         && s.EstadoReembolsoId == EstadosSalida.Reembolso.Aprobado)
                .ToListAsync();

            foreach (var s in solicitudes)
            {
                s.EstadoReembolsoId = EstadosSalida.Reembolso.Firmado;
                s.FirmadoPorId      = userId;
                s.FirmadoAt         = now;
                s.UpdatedAt         = now;
            }

            await ctx.SaveChangesAsync();
        }

        public async Task<string?> GetRendicionFolderUrl()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.GaRendicionFolder
                .Where(f => f.State && f.Active)
                .OrderBy(f => f.GaRendicionFolderId)
                .Select(f => f.LinkUrl)
                .FirstOrDefaultAsync();
        }

        public async Task<ReembolsoCorreoInfoDto?> GetReembolsoCorreoInfo(int solicitudId)
        {
            using var ctx = _factory.CreateDbContext();

            var head = await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                join u in ctx.User on (per != null ? per.UserId : null) equals (int?)u.UserId into uGroup
                from u in uGroup.DefaultIfEmpty()
                join r in ctx.GaRendicion on s.RendicionId equals (int?)r.Id into rGroup
                from r in rGroup.DefaultIfEmpty()
                where s.Id == solicitudId
                select new
                {
                    s.Id, WorkerInternalId = w.Id,
                    AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                    Trabajador = per != null ? (per.FullName ?? "Trabajador") : "Trabajador",
                    Email = u != null ? u.Email : null,
                    s.FechaSalida, s.EstadoReembolsoId, s.ObservacionReembolso, s.ReembolsoDecididoPorId,
                    s.RendicionId,
                    NumeroPlanilla = r != null ? r.NumeroPlanilla : null,
                }
            ).FirstOrDefaultAsync();

            if (head == null) return null;

            var trayectoIds = await ctx.GaSolicitudTrayecto
                .Where(t => t.SolicitudId == solicitudId)
                .Select(t => t.Id)
                .ToListAsync();

            var monto = trayectoIds.Count == 0
                ? 0m
                : await ctx.GaSolicitudCaptura
                    .Where(c => trayectoIds.Contains(c.TrayectoId))
                    .SumAsync(c => (decimal?)c.Monto) ?? 0m;

            string? decididoPor = null;
            if (head.ReembolsoDecididoPorId.HasValue)
            {
                decididoPor = await (
                    from w in ctx.Worker
                    join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                    where per.UserId == head.ReembolsoDecididoPorId.Value
                    select per.FullName
                ).FirstOrDefaultAsync();
            }

            var codigo = await ctx.GaSolicitudSalida
                .Where(s => s.Id == solicitudId)
                .Select(s => s.Codigo)
                .FirstOrDefaultAsync();

            return new ReembolsoCorreoInfoDto
            {
                SolicitudId          = head.Id,
                WorkerId             = head.WorkerInternalId,
                Codigo               = codigo ?? $"#{head.Id}",
                Trabajador           = head.Trabajador,
                SolicitanteEmail     = head.Email,
                Area                 = await ResolveAreaNombreAsync(ctx, head.AreaScopeId),
                FechaSalida          = head.FechaSalida,
                NumeroPlanilla       = PlanillaRendicionHelper.NumeroPlanilla(head.NumeroPlanilla),
                RendicionId          = head.RendicionId,
                TrayectosCount       = trayectoIds.Count,
                MontoTotal           = monto,
                EstadoReembolso      = EstadosSalida.Reembolso.Nombre(head.EstadoReembolsoId),
                ObservacionReembolso = head.ObservacionReembolso,
                DecididoPor          = decididoPor,
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Salidas del alcance del usuario con los filtros de la pantalla ya aplicados.</summary>
        private static IQueryable<GaSolicitudSalida> SalidasVisibles(
            AppDbContext ctx, GestionRendicionFiltersDto filters)
        {
            var query = ctx.GaSolicitudSalida.AsQueryable();

            if (filters.WorkerId.HasValue)
                query = query.Where(s => s.WorkerId == filters.WorkerId.Value);

            if (filters.FilterAreaScopeIds is { Count: > 0 })
            {
                var areaFilter = filters.FilterAreaScopeIds;
                query = query.Where(s =>
                    ctx.Worker.Any(w => w.Id == s.WorkerId &&
                        w.PuestoCatalogo!.AreaDestinoScopeId != null
                        && areaFilter.Contains(w.PuestoCatalogo.AreaDestinoScopeId!.Value)));
            }

            return SalidaVisibilidadFilter.Aplicar(
                query, ctx, filters.CurrentUserId, filters.SeesAll, filters.VisibleAreaScopeIds);
        }

        /// <summary>Copia solo el alcance del usuario, sin los filtros de la pantalla.</summary>
        private static GestionRendicionFiltersDto SoloVisibilidad(GestionRendicionFiltersDto scope) => new()
        {
            CurrentUserId       = scope.CurrentUserId,
            SeesAll             = scope.SeesAll,
            VisibleAreaScopeIds = scope.VisibleAreaScopeIds,
        };

        private static async Task<HashSet<int>> MisWorkerIdsAsync(AppDbContext ctx, int? userId)
        {
            if (!userId.HasValue) return new();
            var ids = await (
                from w in ctx.Worker
                join per in ctx.Person on w.PersonId equals per.PersonId
                where per.UserId == userId.Value
                select w.Id
            ).ToListAsync();
            return ids.ToHashSet();
        }

        /// <summary>
        /// De los ids indicados, cuáles tienen un reembolso listo para decidir: rendidas, con
        /// Consolidado del S10 adjunto y todavía Pendiente o Rechazado.
        /// </summary>
        private static async Task<HashSet<int>> IdsConReembolsoRevisableAsync(AppDbContext ctx, List<int> ids)
        {
            if (ids.Count == 0) return new();

            var candidatas = await ctx.GaSolicitudSalida
                .Where(s => ids.Contains(s.Id)
                         && s.EstadoRendicionId == EstadosSalida.Rendicion.Rendido
                         && (s.EstadoReembolsoId == EstadosSalida.Reembolso.Pendiente
                          || s.EstadoReembolsoId == EstadosSalida.Reembolso.Rechazado))
                .Select(s => new { s.Id, s.RendicionId })
                .ToListAsync();

            if (candidatas.Count == 0) return new();

            var consolidados = await ConsolidadoS10Loader.LoadAsync(
                ctx, candidatas.ToDictionary(x => x.Id, x => x.RendicionId));

            return candidatas.Where(x => consolidados.ContainsKey(x.Id)).Select(x => x.Id).ToHashSet();
        }

        private static async Task<string?> ResolveAreaNombreAsync(AppDbContext ctx, int? areaScopeId)
        {
            if (!areaScopeId.HasValue) return null;
            return await (
                from sc in ctx.AreaScope
                join it in ctx.AreaItem on sc.AreaItemId equals it.AreaItemId
                where sc.AreaScopeId == areaScopeId.Value
                select it.AreaItemName
            ).FirstOrDefaultAsync();
        }

        private static GestionRendicionListItemDto Armar(
            PlanillaRendicionLoader.PlanillaFila p, HashSet<int> misWorkerIds, HashSet<int> porDecidir) => new()
        {
            Id                 = p.Id,
            NumeroPlanilla     = p.NumeroPlanilla,
            RendidoAt          = p.RendidoAt,
            Periodo            = p.Periodo,
            PeriodoAnio        = p.PeriodoAnio,
            PeriodoMes         = p.PeriodoMes,
            Trabajadores       = p.Trabajadores,
            SalidasCount       = p.SalidasCount,
            MontoTotal         = p.MontoTotal,
            PdfUrl             = p.PdfUrl,
            PdfFilename        = p.PdfFilename,
            PdfFirmadoUrl      = p.PdfFirmadoUrl,
            PdfFirmadoFilename = p.PdfFirmadoFilename,
            FirmadoAt          = p.FirmadoAt,
            ConsolidadoS10     = p.ConsolidadoS10,
            EstadoReembolso    = p.EstadoReembolso,
            ReembolsoMixto     = p.ReembolsoMixto,
            ObservacionReembolso = p.ObservacionReembolso,
            RevisorNotificadoAt  = p.RevisorNotificadoAt,
            PorDecidirCount    = p.Salidas.Count(s => porDecidir.Contains(s.Id)),
            PorFirmarCount     = p.Salidas.Count(s => s.EstadoReembolsoId == EstadosSalida.Reembolso.Aprobado),
            IncluyePropias     = p.Salidas.Any(s => misWorkerIds.Contains(s.WorkerId)),
        };

        private static void CopiarCabecera(GestionRendicionListItemDto o, GestionRendicionDetalleDto d)
        {
            d.Id = o.Id; d.NumeroPlanilla = o.NumeroPlanilla; d.RendidoAt = o.RendidoAt;
            d.Periodo = o.Periodo; d.PeriodoAnio = o.PeriodoAnio; d.PeriodoMes = o.PeriodoMes;
            d.Trabajadores = o.Trabajadores; d.SalidasCount = o.SalidasCount; d.MontoTotal = o.MontoTotal;
            d.PdfUrl = o.PdfUrl; d.PdfFilename = o.PdfFilename;
            d.PdfFirmadoUrl = o.PdfFirmadoUrl; d.PdfFirmadoFilename = o.PdfFirmadoFilename;
            d.FirmadoAt = o.FirmadoAt; d.ConsolidadoS10 = o.ConsolidadoS10;
            d.EstadoReembolso = o.EstadoReembolso; d.ReembolsoMixto = o.ReembolsoMixto;
            d.ObservacionReembolso = o.ObservacionReembolso; d.RevisorNotificadoAt = o.RevisorNotificadoAt;
            d.PorDecidirCount = o.PorDecidirCount; d.PorFirmarCount = o.PorFirmarCount;
            d.IncluyePropias = o.IncluyePropias;
        }

        /// <summary>
        /// Filtros que se resuelven sobre la fila ya armada (el estado y el periodo de una planilla
        /// salen de sus salidas, así que no se pueden pedir en la consulta).
        /// </summary>
        private static List<GestionRendicionListItemDto> Filtrar(
            List<GestionRendicionListItemDto> items, GestionRendicionFiltersDto filters)
        {
            IEnumerable<GestionRendicionListItemDto> q = items;

            if (!string.IsNullOrWhiteSpace(filters.EstadoReembolso))
                q = q.Where(x => x.EstadoReembolso == filters.EstadoReembolso!.Trim());

            if (filters.ConConsolidado.HasValue)
                q = q.Where(x => (x.ConsolidadoS10 != null) == filters.ConConsolidado.Value);

            if (filters.PeriodoAnio.HasValue && filters.PeriodoMes.HasValue)
                q = q.Where(x => x.PeriodoAnio == filters.PeriodoAnio.Value
                              && x.PeriodoMes  == filters.PeriodoMes.Value);

            return q.ToList();
        }
    }
}
