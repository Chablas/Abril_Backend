using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Models;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Repositories
{
    public class GestionSalidaRepository : IGestionSalidaRepository
    {
        private const int PageSize = 10;
        private const string CategoriaGerente = "Gerente";
        private readonly IDbContextFactory<AppDbContext> _factory;

        public GestionSalidaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Tabla ordenada + paginada. Reutiliza <see cref="GetAll"/> (que ya resuelve motivo/origen/
        /// destino/horas y <c>PuedeRendirse</c>) y aplica el orden por columna en memoria. El orden es
        /// estable: los empates conservan el orden original (pendientes primero, luego más recientes).
        /// </summary>
        public async Task<PagedResult<GestionSalidaListItemDto>> GetPaged(GestionSalidaFiltersDto filters)
        {
            var all = await GetAll(filters);
            var sorted = ApplySort(all, filters);

            var totalRecords = sorted.Count;
            var page = filters.Page < 1 ? 1 : filters.Page;

            return new PagedResult<GestionSalidaListItemDto>
            {
                Page = page,
                PageSize = PageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)PageSize),
                Data = sorted.Skip((page - 1) * PageSize).Take(PageSize).ToList(),
            };
        }

        /// <summary>
        /// Ordena en memoria por la columna indicada en <paramref name="filters"/>. Si no se indica
        /// columna (o es desconocida) se conserva el orden original que trae <see cref="GetAll"/>.
        /// Las columnas de texto se ordenan ignorando mayúsculas/acentos según la cultura.
        /// </summary>
        private static List<GestionSalidaListItemDto> ApplySort(List<GestionSalidaListItemDto> items, GestionSalidaFiltersDto filters)
        {
            if (string.IsNullOrWhiteSpace(filters.SortBy)) return items;

            var asc = !string.Equals(filters.SortDir, "desc", StringComparison.OrdinalIgnoreCase);

            IEnumerable<GestionSalidaListItemDto> OrderText(Func<GestionSalidaListItemDto, string?> sel) =>
                asc
                    ? items.OrderBy(x => sel(x) ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                    : items.OrderByDescending(x => sel(x) ?? string.Empty, StringComparer.CurrentCultureIgnoreCase);

            IEnumerable<GestionSalidaListItemDto> OrderKey<TKey>(Func<GestionSalidaListItemDto, TKey> sel) =>
                asc ? items.OrderBy(sel) : items.OrderByDescending(sel);

            IEnumerable<GestionSalidaListItemDto>? ordered = filters.SortBy.Trim().ToLowerInvariant() switch
            {
                "trabajador"       => OrderText(s => s.Trabajador),
                "motivo"           => OrderText(s => s.Motivo),
                "lugarorigen"      => OrderText(s => s.LugarOrigen),
                "lugardestino"     => OrderText(s => s.LugarDestino),
                "estadoaprobacion" => OrderText(s => s.EstadoAprobacion),
                "estadorendicion"  => OrderText(s => s.EstadoRendicion),
                "fechasalida"      => OrderKey(s => s.FechaSalida),
                "horasalida"       => OrderKey(s => s.HoraSalida),
                "horaretorno"      => OrderKey(s => s.HoraRetorno),
                "createdat"        => OrderKey(s => s.CreatedAt),
                _                  => null,
            };

            return ordered?.ToList() ?? items;
        }

        public async Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters)
        {
            using var ctx = _factory.CreateDbContext();

            // 1. Filtrar solicitudes (cabecera)
            var solicitudQuery = ctx.GaSolicitudSalida.AsQueryable();

            if (filters.WorkerId.HasValue)
                solicitudQuery = solicitudQuery.Where(s => s.WorkerId == filters.WorkerId.Value);

            var rendId = EstadosSalida.Rendicion.IdFromNombre(filters.EstadoRendicion);
            if (rendId.HasValue)
                solicitudQuery = solicitudQuery.Where(s => s.EstadoRendicionId == rendId.Value);

            var aprobId = EstadosSalida.Aprobacion.IdFromNombre(filters.EstadoAprobacion);
            if (aprobId.HasValue)
                solicitudQuery = solicitudQuery.Where(s => s.EstadoAprobacionId == aprobId.Value);

            // Visibilidad obligatoria (server-side): el usuario SIEMPRE ve sus propias solicitudes
            // (worker_id → su user), sin importar rol ni área — así un trabajador cualquiera puede
            // ver y rendir lo suyo. Además ve las que le fueron enviadas para revisar
            // (enviado_a_correo → su email_corporativo), las que él decidió (aprobador_worker_id →
            // su user; también cubre solicitudes antiguas donde ese campo guardaba al revisor al que
            // se envió), MÁS las de los trabajadores que pertenecen a las áreas (area_scope) que
            // tiene permitido ver. Si SeesAll = true no se aplica restricción por área. El servicio
            // ya resolvió el alcance (override o algoritmo).
            if (filters.CurrentUserId.HasValue && !filters.SeesAll)
            {
                var uid = filters.CurrentUserId.Value;
                var areaIds = filters.VisibleAreaScopeIds ?? new List<int>();
                solicitudQuery = solicitudQuery.Where(s =>
                    ctx.Worker.Any(w => w.Id == s.WorkerId &&
                        ctx.Person.Any(p => p.PersonId == w.PersonId && p.UserId == uid))
                    ||
                    (s.AprobadorWorkerId != null &&
                     ctx.Worker.Any(w => w.Id == s.AprobadorWorkerId &&
                         ctx.Person.Any(p => p.PersonId == w.PersonId && p.UserId == uid)))
                    ||
                    (s.EnviadoACorreo != null &&
                     ctx.Worker.Any(w => w.EmailCorporativo != null &&
                         w.EmailCorporativo.Trim().ToLower() == s.EnviadoACorreo.Trim().ToLower() &&
                         ctx.Person.Any(p => p.PersonId == w.PersonId && p.UserId == uid)))
                    ||
                    ctx.Worker.Any(w => w.Id == s.WorkerId &&
                        w.AreaScopeId != null && areaIds.Contains(w.AreaScopeId.Value)));
            }

            // Filtro de área elegido por el usuario (cascada): trabajadores cuyo area_scope_id
            // esté dentro del nodo seleccionado + sus descendientes (ya expandidos en el frontend).
            if (filters.FilterAreaScopeIds is { Count: > 0 })
            {
                var areaFilter = filters.FilterAreaScopeIds;
                solicitudQuery = solicitudQuery.Where(s =>
                    ctx.Worker.Any(w => w.Id == s.WorkerId &&
                        w.AreaScopeId != null && areaFilter.Contains(w.AreaScopeId.Value)));
            }

            // Filtro por lugar proyecto: necesita pasar por trayectos
            if (filters.LugarProyectoId.HasValue)
            {
                var lugId = filters.LugarProyectoId.Value;
                solicitudQuery = solicitudQuery.Where(s => ctx.GaSolicitudTrayecto.Any(t =>
                    t.SolicitudId == s.Id &&
                    (t.LugarOrigenId == lugId || t.LugarDestinoId == lugId)));
            }

            var solicitudes = await (
                from s in solicitudQuery
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                orderby s.EstadoAprobacionId == EstadosSalida.Aprobacion.Pendiente ? 0 : 1,
                        s.CreatedAt descending
                select new
                {
                    s.Id, s.WorkerId, WorkerInternalId = w.Id, w.Subarea,
                    Trabajador = per != null ? (per.FullName ?? "[Sin nombre]") : "[Sin nombre]",
                    s.FechaSalida, s.EstadoAprobacionId, s.EstadoRendicionId, s.CreatedAt,
                    s.HoraSalidaReal, s.HoraRetornoReal
                }
            ).ToListAsync();

            if (solicitudes.Count == 0) return new();

            var solicitudIds = solicitudes.Select(s => s.Id).ToList();

            // 2. Trayectos para info agregada (motivo, origen, destino, horas).
            // Conservamos los IDs crudos de origen/destino para el match contra ga_trayecto.
            var trayectos = await (
                from t  in ctx.GaSolicitudTrayecto
                join m  in ctx.GaMotivoSalida on t.MotivoId equals m.Id into mGroup
                from m  in mGroup.DefaultIfEmpty()
                join lo in ctx.GaLugar on t.LugarOrigenId equals lo.Id into loGroup
                from lo in loGroup.DefaultIfEmpty()
                join po in ctx.Project on lo.ProjectId equals (int?)po.ProjectId into poGroup
                from po in poGroup.DefaultIfEmpty()
                join ld in ctx.GaLugar on t.LugarDestinoId equals ld.Id into ldGroup
                from ld in ldGroup.DefaultIfEmpty()
                join pd in ctx.Project on ld.ProjectId equals (int?)pd.ProjectId into pdGroup
                from pd in pdGroup.DefaultIfEmpty()
                where solicitudIds.Contains(t.SolicitudId)
                orderby t.SolicitudId, t.Orden
                select new
                {
                    t.Id, t.SolicitudId, t.Orden, t.HoraSalida, t.HoraRetorno,
                    t.LugarOrigenId, t.LugarDestinoId,
                    Motivo = m != null ? m.Descripcion : (t.MotivoLibre ?? string.Empty),
                    LugarOrigen = lo == null ? t.LugarOrigenLibre
                                : lo.Tipo == "proyecto" ? (po != null ? po.ProjectDescription : "[Sin proyecto]")
                                : lo.Nombre,
                    LugarDestino = ld == null ? t.LugarDestinoLibre
                                 : ld.Tipo == "proyecto" ? (pd != null ? pd.ProjectDescription : "[Sin proyecto]")
                                 : ld.Nombre,
                }
            ).ToListAsync();

            var trayectosBySol = trayectos.GroupBy(t => t.SolicitudId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Orden).ToList());

            // 3. Trayectos con al menos 1 captura
            var trayectoIds = trayectos.Select(t => t.Id).ToList();
            var trayectosConCapturas = trayectoIds.Count == 0
                ? new HashSet<int>()
                : (await ctx.GaSolicitudCaptura
                    .Where(c => trayectoIds.Contains(c.TrayectoId))
                    .Select(c => c.TrayectoId)
                    .Distinct()
                    .ToListAsync()).ToHashSet();

            // 4. Catálogo si hay al menos un trabajador TI sin todas sus capturas — para evaluar la regla relajada.
            var hayWorkerTI = solicitudes.Any(s => string.Equals(s.Subarea, SubareaTi, StringComparison.OrdinalIgnoreCase));
            var catalogoMap = hayWorkerTI ? await CargarCatalogoTrayectosAsync(ctx) : new();

            // 4.b. Worker(s) del usuario actual + si es Gerente — para marcar por fila si puede
            //      aprobar/rechazar (nadie decide sus propias salidas, salvo los gerentes).
            var misWorkerIds = new HashSet<int>();
            var esGerente = false;
            if (filters.CurrentUserId.HasValue)
            {
                var uidDec = filters.CurrentUserId.Value;
                var misWorkers = await (
                    from w in ctx.Worker
                    join p in ctx.Person on w.PersonId equals p.PersonId
                    join c in ctx.WorkersCategory on w.WorkerCategoryId equals c.WorkersCategoryId into cj
                    from c in cj.DefaultIfEmpty()
                    where p.UserId == uidDec
                    select new { w.Id, Categoria = c != null ? c.Name : null }
                ).ToListAsync();
                misWorkerIds = misWorkers.Select(x => x.Id).ToHashSet();
                esGerente = misWorkers.Any(x => string.Equals(x.Categoria, CategoriaGerente, StringComparison.OrdinalIgnoreCase));
            }

            // 5. Armar resultado
            var result = new List<GestionSalidaListItemDto>(solicitudes.Count);
            foreach (var s in solicitudes)
            {
                trayectosBySol.TryGetValue(s.Id, out var trList);
                trList ??= new();
                var first = trList.FirstOrDefault();
                var last  = trList.LastOrDefault();

                var esTI = string.Equals(s.Subarea, SubareaTi, StringComparison.OrdinalIgnoreCase);
                bool trayectoCubierto(dynamic t)
                {
                    if (trayectosConCapturas.Contains((int)t.Id)) return true;
                    if (!esTI) return false;
                    if (t.LugarOrigenId == null || t.LugarDestinoId == null) return false;
                    return catalogoMap.ContainsKey(((int)t.LugarOrigenId, (int)t.LugarDestinoId));
                }
                var puedeRendir = trList.Count > 0 && trList.All(t => trayectoCubierto(t));

                result.Add(new GestionSalidaListItemDto
                {
                    Id               = s.Id,
                    WorkerId         = s.WorkerInternalId,
                    Trabajador       = s.Trabajador,
                    FechaSalida      = s.FechaSalida,
                    HoraSalida       = first?.HoraSalida ?? default,
                    HoraRetorno      = last?.HoraRetorno,
                    Motivo           = first?.Motivo ?? string.Empty,
                    LugarOrigen      = first?.LugarOrigen,
                    LugarDestino     = last?.LugarDestino,
                    TrayectosCount   = trList.Count,
                    EstadoAprobacion = EstadosSalida.Aprobacion.Nombre(s.EstadoAprobacionId),
                    EstadoRendicion  = EstadosSalida.Rendicion.Nombre(s.EstadoRendicionId),
                    CreatedAt        = s.CreatedAt,
                    PuedeRendirse    = puedeRendir,
                    HoraSalidaReal   = s.HoraSalidaReal,
                    HoraRetornoReal  = s.HoraRetornoReal,
                    PuedeDecidir     = esGerente || !misWorkerIds.Contains(s.WorkerId),
                });
            }

            return result;
        }

        public async Task<GestionSalidaFilterDataDto> GetFilterData(bool seesAll, List<int> visibleAreaScopeIds, int? currentUserId)
        {
            using var ctx = _factory.CreateDbContext();

            var workerIds = await ctx.GaSolicitudSalida
                .Select(s => s.WorkerId)
                .Distinct()
                .ToListAsync();

            // Base: trabajadores con al menos una solicitud.
            var trabajadoresQuery = ctx.Worker.Where(w => workerIds.Contains(w.Id));

            // Recorte por visibilidad: solo trabajadores cuya área esté en el alcance del usuario
            // (área actual hacia abajo). El propio trabajador del usuario siempre entra, porque
            // siempre ve sus propias solicitudes. Si ve todo (recepción/GTH), no se recorta.
            if (!seesAll)
            {
                trabajadoresQuery = trabajadoresQuery.Where(w =>
                    (w.AreaScopeId != null && visibleAreaScopeIds.Contains(w.AreaScopeId.Value))
                    || (currentUserId != null &&
                        ctx.Person.Any(p => p.PersonId == w.PersonId && p.UserId == currentUserId)));
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

            var lugaresProyecto = await (
                from g in ctx.GaLugar
                join p in ctx.Project on g.ProjectId equals p.ProjectId
                where g.Tipo == "proyecto" && g.Activo
                orderby p.ProjectDescription
                select new LugarProyectoOptionDto
                {
                    GaLugarId    = g.Id,
                    NombreDisplay = p.ProjectDescription,
                }
            ).ToListAsync();

            var areaTree = await (
                from s in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.State && ai.State && at.State
                   && (seesAll || visibleAreaScopeIds.Contains(s.AreaScopeId))
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

            return new GestionSalidaFilterDataDto
            {
                Trabajadores    = trabajadores,
                LugaresProyecto = lugaresProyecto,
                AreaTree        = areaTree,
            };
        }

        /// <summary>
        /// Regla de negocio: un usuario NO puede aprobar ni rechazar sus propias solicitudes de
        /// salida. Única excepción: si el usuario es Gerente (workers_category "Gerente"), sí puede
        /// decidir las suyas (los gerentes salen sin pedir permiso, pero se deja habilitado por si acaso).
        /// </summary>
        private static async Task EnsurePuedeDecidirAsync(AppDbContext ctx, GaSolicitudSalida s, int reviewerUserId)
        {
            var esPropia = await ctx.Worker.AnyAsync(w => w.Id == s.WorkerId &&
                ctx.Person.Any(p => p.PersonId == w.PersonId && p.UserId == reviewerUserId));
            if (!esPropia) return;

            // Categorías de los worker(s) del usuario — se materializan y comparan en memoria
            // (mismo criterio case-insensitive que SalidaVisibilityResolver).
            var categorias = await (
                from w in ctx.Worker
                join p in ctx.Person on w.PersonId equals p.PersonId
                join c in ctx.WorkersCategory on w.WorkerCategoryId equals c.WorkersCategoryId
                where p.UserId == reviewerUserId
                select c.Name
            ).ToListAsync();

            var esGerente = categorias.Any(n => string.Equals(n, CategoriaGerente, StringComparison.OrdinalIgnoreCase));
            if (!esGerente)
                throw new AbrilException("No puedes aprobar ni rechazar tus propias solicitudes de salida.", 403);
        }

        public async Task Aprobar(int id, int reviewerUserId)
        {
            using var ctx = _factory.CreateDbContext();
            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new AbrilException("Solicitud no encontrada.", 404);
            await EnsurePuedeDecidirAsync(ctx, s, reviewerUserId);
            if (s.EstadoAprobacionId != EstadosSalida.Aprobacion.Pendiente)
                throw new AbrilException("Solo se pueden aprobar solicitudes en estado Pendiente.", 400);
            s.EstadoAprobacionId = EstadosSalida.Aprobacion.Aprobado;
            s.FechaDecision      = DateTimeOffset.UtcNow;
            s.UpdatedAt          = DateTimeOffset.UtcNow;
            // Decisión desde la web: el aprobador real es el worker del usuario logueado.
            await SalidaAprobadorHelper.AsignarPorUsuarioAsync(ctx, s, reviewerUserId);
            await ctx.SaveChangesAsync();
        }

        public async Task Rechazar(int id, int reviewerUserId)
        {
            using var ctx = _factory.CreateDbContext();
            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new AbrilException("Solicitud no encontrada.", 404);
            await EnsurePuedeDecidirAsync(ctx, s, reviewerUserId);
            if (s.EstadoAprobacionId != EstadosSalida.Aprobacion.Pendiente)
                throw new AbrilException("Solo se pueden rechazar solicitudes en estado Pendiente.", 400);
            s.EstadoAprobacionId = EstadosSalida.Aprobacion.Rechazado;
            s.FechaDecision      = DateTimeOffset.UtcNow;
            s.UpdatedAt          = DateTimeOffset.UtcNow;
            // Decisión desde la web: quien decide (rechaza) es el worker del usuario logueado.
            await SalidaAprobadorHelper.AsignarPorUsuarioAsync(ctx, s, reviewerUserId);
            await ctx.SaveChangesAsync();
        }

        public async Task<int> GetNextNumeroPlanillaAsync()
        {
            using var ctx = _factory.CreateDbContext();
            var values = await ctx.Database
                .SqlQuery<int>($"SELECT nextval('seq_planilla_numero')::int AS \"Value\"")
                .ToListAsync();
            return values.First();
        }

        public async Task<List<int>> CrearRendicionYMarcarBulk(
            IEnumerable<int> ids,
            int userId,
            string pdfUrl,
            string? pdfItemId,
            string pdfFilename,
            int numeroPlanilla)
        {
            using var ctx = _factory.CreateDbContext();
            var idsList = ids?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            var solicitudes = await ctx.GaSolicitudSalida
                .Where(s => idsList.Contains(s.Id)
                         && s.EstadoAprobacionId == EstadosSalida.Aprobacion.Aprobado
                         && s.EstadoRendicionId  == EstadosSalida.Rendicion.NoRendido)
                .ToListAsync();

            if (solicitudes.Count == 0)
                throw new AbrilException("No hay solicitudes elegibles para rendir (deben estar aprobadas y no rendidas).", 400);

            var now = DateTimeOffset.UtcNow;

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await ctx.Database.BeginTransactionAsync();

                var rendicion = new GaRendicion
                {
                    PdfUrl         = pdfUrl,
                    PdfItemId      = pdfItemId,
                    PdfFilename    = pdfFilename,
                    RendidoPorId   = userId,
                    RendidoAt      = now,
                    NumeroPlanilla = numeroPlanilla,
                };
                ctx.GaRendicion.Add(rendicion);
                await ctx.SaveChangesAsync();

                foreach (var s in solicitudes)
                {
                    s.EstadoRendicionId = EstadosSalida.Rendicion.Rendido;
                    s.RendicionId       = rendicion.Id;
                    s.UpdatedAt         = now;
                }
                await ctx.SaveChangesAsync();
                await tx.CommitAsync();
            });

            return solicitudes.Select(s => s.Id).ToList();
        }

        public async Task<List<int>> GetEligibleIdsForRendicion(IEnumerable<int> ids)
        {
            using var ctx = _factory.CreateDbContext();
            var idsList = ids?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            return await ctx.GaSolicitudSalida
                .Where(s => idsList.Contains(s.Id)
                         && s.EstadoAprobacionId == EstadosSalida.Aprobacion.Aprobado
                         && s.EstadoRendicionId  == EstadosSalida.Rendicion.NoRendido)
                .Select(s => s.Id)
                .ToListAsync();
        }

        public async Task<List<int>> GetIdsNotOwnedByUser(IEnumerable<int> ids, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var idsList = ids?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            var owned = await (
                from s   in ctx.GaSolicitudSalida
                join w   in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                where idsList.Contains(s.Id) && per.UserId == userId
                select s.Id
            ).ToListAsync();

            return idsList.Except(owned).ToList();
        }

        public async Task<List<int>> GetIdsConTrayectosSinCapturas(IEnumerable<int> ids)
        {
            using var ctx = _factory.CreateDbContext();
            var idsList = ids?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            // Cargar info de cada solicitud + subarea del worker
            var solicitudes = await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                where idsList.Contains(s.Id)
                select new { s.Id, w.Subarea }
            ).ToListAsync();

            if (solicitudes.Count == 0) return idsList;

            // Trayectos por solicitud (Id + lugares para match con catálogo)
            var trayectos = await ctx.GaSolicitudTrayecto
                .Where(t => idsList.Contains(t.SolicitudId))
                .Select(t => new { t.Id, t.SolicitudId, t.LugarOrigenId, t.LugarDestinoId })
                .ToListAsync();
            var trayectosBySol = trayectos.GroupBy(t => t.SolicitudId).ToDictionary(g => g.Key, g => g.ToList());

            // Trayectos con al menos 1 captura
            var trayectoIds = trayectos.Select(t => t.Id).ToList();
            var conCapturas = trayectoIds.Count == 0
                ? new HashSet<int>()
                : (await ctx.GaSolicitudCaptura
                    .Where(c => trayectoIds.Contains(c.TrayectoId))
                    .Select(c => c.TrayectoId)
                    .Distinct()
                    .ToListAsync()).ToHashSet();

            // Catálogo (cargado solo si algún worker es TI)
            var hayTI = solicitudes.Any(s => string.Equals(s.Subarea, SubareaTi, StringComparison.OrdinalIgnoreCase));
            var catalogoMap = hayTI ? await CargarCatalogoTrayectosAsync(ctx) : new();

            var incompletas = new List<int>();
            foreach (var s in solicitudes)
            {
                if (!trayectosBySol.TryGetValue(s.Id, out var trList) || trList.Count == 0)
                {
                    incompletas.Add(s.Id);
                    continue;
                }

                var esTI = string.Equals(s.Subarea, SubareaTi, StringComparison.OrdinalIgnoreCase);
                bool todosCubiertos = trList.All(t =>
                {
                    if (conCapturas.Contains(t.Id)) return true;
                    if (!esTI) return false;
                    if (!t.LugarOrigenId.HasValue || !t.LugarDestinoId.HasValue) return false;
                    return catalogoMap.ContainsKey((t.LugarOrigenId.Value, t.LugarDestinoId.Value));
                });

                if (!todosCubiertos) incompletas.Add(s.Id);
            }

            return incompletas;
        }

        public async Task<GestionSalidaDetalleDto?> GetDetalle(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var head = await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                join r in ctx.GaRendicion on s.RendicionId equals (int?)r.Id into rGroup
                from r in rGroup.DefaultIfEmpty()
                where s.Id == id
                select new
                {
                    s.Id, WorkerInternalId = w.Id, w.Subarea,
                    Trabajador = per != null ? (per.FullName ?? "[Sin nombre]") : "[Sin nombre]",
                    s.FechaSalida, s.EstadoAprobacionId, s.EstadoRendicionId, s.CreatedAt, s.MotivoRechazo,
                    Rendicion = r == null ? null : new GestionSalidaRendicionDto
                    {
                        Id          = r.Id,
                        PdfUrl      = r.PdfUrl,
                        PdfFilename = r.PdfFilename,
                        RendidoAt   = r.RendidoAt,
                    },
                }
            ).FirstOrDefaultAsync();

            if (head == null) return null;

            var trayectosRaw = await (
                from t  in ctx.GaSolicitudTrayecto
                join m  in ctx.GaMotivoSalida on t.MotivoId equals m.Id into mGroup
                from m  in mGroup.DefaultIfEmpty()
                join lo in ctx.GaLugar on t.LugarOrigenId equals lo.Id into loGroup
                from lo in loGroup.DefaultIfEmpty()
                join po in ctx.Project on lo.ProjectId equals (int?)po.ProjectId into poGroup
                from po in poGroup.DefaultIfEmpty()
                join ld in ctx.GaLugar on t.LugarDestinoId equals ld.Id into ldGroup
                from ld in ldGroup.DefaultIfEmpty()
                join pd in ctx.Project on ld.ProjectId equals (int?)pd.ProjectId into pdGroup
                from pd in pdGroup.DefaultIfEmpty()
                where t.SolicitudId == id
                orderby t.Orden
                select new
                {
                    Dto = new GestionSalidaTrayectoDto
                    {
                        Id          = t.Id,
                        Orden       = t.Orden,
                        HoraSalida  = t.HoraSalida,
                        HoraRetorno = t.HoraRetorno,
                        Motivo      = m != null ? m.Descripcion : (t.MotivoLibre ?? string.Empty),
                        LugarOrigen = lo == null ? t.LugarOrigenLibre
                                    : lo.Tipo == "proyecto" ? (po != null ? po.ProjectDescription : "[Sin proyecto]")
                                    : lo.Nombre,
                        LugarDestino = ld == null ? t.LugarDestinoLibre
                                     : ld.Tipo == "proyecto" ? (pd != null ? pd.ProjectDescription : "[Sin proyecto]")
                                     : ld.Nombre,
                    },
                    t.LugarOrigenId,
                    t.LugarDestinoId,
                    // Adjunto legacy embebido (modelo anterior 1:1). Se combina con la tabla nueva.
                    t.AdjuntoUrl,
                    t.AdjuntoFilename,
                }
            ).ToListAsync();

            var trayectos = trayectosRaw.Select(x => x.Dto).ToList();

            // Capturas por trayecto
            var trayectoIds = trayectos.Select(t => t.Id).ToList();

            // Adjuntos (tabla nueva ga_solicitud_trayecto_adjunto, N por trayecto) + legacy embebido.
            var adjuntosByTrayecto = new Dictionary<int, List<GestionSalidaAdjuntoDto>>();
            if (trayectoIds.Count > 0)
            {
                var adjRaw = await ctx.GaSolicitudTrayectoAdjunto
                    .Where(a => trayectoIds.Contains(a.TrayectoId))
                    .OrderBy(a => a.UploadedAt).ThenBy(a => a.Id)
                    .Select(a => new { a.TrayectoId, a.AdjuntoUrl, a.AdjuntoFilename })
                    .ToListAsync();

                adjuntosByTrayecto = adjRaw.GroupBy(a => a.TrayectoId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(a => new GestionSalidaAdjuntoDto { Url = a.AdjuntoUrl, Filename = a.AdjuntoFilename }).ToList());
            }

            foreach (var raw in trayectosRaw)
            {
                var lista = new List<GestionSalidaAdjuntoDto>();
                if (!string.IsNullOrWhiteSpace(raw.AdjuntoUrl))
                    lista.Add(new GestionSalidaAdjuntoDto { Url = raw.AdjuntoUrl, Filename = raw.AdjuntoFilename ?? "Ver documento" });
                if (adjuntosByTrayecto.TryGetValue(raw.Dto.Id, out var nuevos))
                    lista.AddRange(nuevos);
                raw.Dto.Adjuntos = lista;
            }

            if (trayectoIds.Count > 0)
            {
                var capsRaw = await ctx.GaSolicitudCaptura
                    .Where(c => trayectoIds.Contains(c.TrayectoId))
                    .OrderBy(c => c.UploadedAt)
                    .Select(c => new
                    {
                        c.TrayectoId,
                        Dto = new GestionSalidaCapturaDto
                        {
                            Id         = c.Id,
                            ImageUrl   = c.ImageUrl,
                            Filename   = c.Filename,
                            Monto      = c.Monto,
                            UploadedAt = c.UploadedAt,
                        }
                    })
                    .ToListAsync();

                var capsByTr = capsRaw.GroupBy(x => x.TrayectoId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Dto).ToList());

                foreach (var tr in trayectos)
                {
                    if (capsByTr.TryGetValue(tr.Id, out var list))
                        tr.Capturas = list;
                }
            }

            // Catálogo (solo si el worker es TI)
            var esTI = string.Equals(head.Subarea, SubareaTi, StringComparison.OrdinalIgnoreCase);
            var catalogoMap = esTI ? await CargarCatalogoTrayectosAsync(ctx) : new();

            foreach (var raw in trayectosRaw)
            {
                var sumCapturas = raw.Dto.Capturas.Sum(c => c.Monto);
                if (esTI && raw.LugarOrigenId.HasValue && raw.LugarDestinoId.HasValue &&
                    catalogoMap.TryGetValue((raw.LugarOrigenId.Value, raw.LugarDestinoId.Value), out var montoCat))
                {
                    raw.Dto.MontoCatalogo = montoCat;
                }
                raw.Dto.MontoTotal = sumCapturas > 0 ? sumCapturas : (raw.Dto.MontoCatalogo ?? 0m);
            }

            return new GestionSalidaDetalleDto
            {
                Id               = head.Id,
                WorkerId         = head.WorkerInternalId,
                Trabajador       = head.Trabajador,
                FechaSalida      = head.FechaSalida,
                EstadoAprobacion = EstadosSalida.Aprobacion.Nombre(head.EstadoAprobacionId),
                EstadoRendicion  = EstadosSalida.Rendicion.Nombre(head.EstadoRendicionId),
                CreatedAt        = head.CreatedAt,
                MotivoRechazo    = head.MotivoRechazo,
                Rendicion        = head.Rendicion,
                Trayectos        = trayectos,
            };
        }

        public async Task<List<RendicionItemDto>> GetRendicionData(List<int> solicitudIds)
        {
            using var ctx = _factory.CreateDbContext();
            if (solicitudIds.Count == 0) return new();

            // Una fila = un trayecto. Cargamos los IDs crudos de lugares + subarea del worker
            // para poder hacer match contra ga_trayecto cuando el trabajador es TI.
            var rowsRaw = await (
                from t   in ctx.GaSolicitudTrayecto
                join s   in ctx.GaSolicitudSalida on t.SolicitudId equals s.Id
                join w   in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                join cont in ctx.Contributor on w.ContributorId equals (int?)cont.ContributorId into contGroup
                from cont in contGroup.DefaultIfEmpty()
                join m   in ctx.GaMotivoSalida on t.MotivoId equals m.Id into mGroup
                from m   in mGroup.DefaultIfEmpty()
                join lo  in ctx.GaLugar on t.LugarOrigenId equals lo.Id into loGroup
                from lo  in loGroup.DefaultIfEmpty()
                join po  in ctx.Project on lo.ProjectId equals (int?)po.ProjectId into poGroup
                from po  in poGroup.DefaultIfEmpty()
                join ld  in ctx.GaLugar on t.LugarDestinoId equals ld.Id into ldGroup
                from ld  in ldGroup.DefaultIfEmpty()
                join pd  in ctx.Project on ld.ProjectId equals (int?)pd.ProjectId into pdGroup
                from pd  in pdGroup.DefaultIfEmpty()
                where solicitudIds.Contains(s.Id)
                orderby w.Id, s.FechaSalida, t.Orden
                select new
                {
                    Item = new RendicionItemDto
                    {
                        Id               = t.Id,
                        SolicitudId      = s.Id,
                        WorkerId         = w.Id,
                        TrabajadorNombre = per != null ? (per.FullName ?? "") : "",
                        TrabajadorDni    = per != null ? per.DocumentIdentityCode : null,
                        TrabajadorDocumentTypeId = per != null ? per.DocumentIdentityTypeId : null,
                        Area             = w.Area,     // fallback; se sobrescribe abajo si hay area_scope_id
                        FechaSalida      = s.FechaSalida,
                        Motivo           = m != null ? m.Descripcion : (t.MotivoLibre ?? ""),
                        LugarOrigen      = lo == null ? t.LugarOrigenLibre
                                         : lo.Tipo == "proyecto" ? (po != null ? po.ProjectDescription : null)
                                         : lo.Nombre,
                        LugarDestino     = ld == null ? t.LugarDestinoLibre
                                         : ld.Tipo == "proyecto" ? (pd != null ? pd.ProjectDescription : null)
                                         : ld.Nombre,
                        RazonSocial      = cont != null ? cont.ContributorName : null,
                        Ruc              = cont != null ? cont.ContributorRuc  : null,
                    },
                    Subarea = w.Subarea,
                    WorkerAreaScopeId = w.AreaScopeId,
                    t.LugarOrigenId,
                    t.LugarDestinoId,
                }
            ).ToListAsync();

            // Resolver nombre del área navegando hacia arriba en area_scope hasta encontrar
            // el primer nodo cuyo tipo sea "Área Estándar" (saltando "Área de Gerencia", etc).
            // Una solicitud puede tener N trayectos → hay varias filas por worker; nos quedamos
            // con una sola entrada (worker → area_scope_id) para no duplicar la clave del diccionario.
            var workerScope = rowsRaw
                .Where(r => r.WorkerAreaScopeId.HasValue)
                .GroupBy(r => r.Item.WorkerId)
                .ToDictionary(g => g.Key, g => g.First().WorkerAreaScopeId!.Value);

            var areaResueltaPorWorker = await ResolverAreaPorWorkerAsync(
                ctx,
                workerScope.Keys.ToList(),
                workerScope);

            foreach (var r in rowsRaw)
            {
                if (areaResueltaPorWorker.TryGetValue(r.Item.WorkerId, out var nombreArea) && !string.IsNullOrWhiteSpace(nombreArea))
                    r.Item.Area = nombreArea;
            }

            // Importe por trayecto (suma de capturas)
            var trayectoIds = rowsRaw.Select(r => r.Item.Id).ToList();
            var importesCapturas = await ctx.GaSolicitudCaptura
                .Where(c => trayectoIds.Contains(c.TrayectoId))
                .GroupBy(c => c.TrayectoId)
                .Select(g => new { TrayectoId = g.Key, Total = g.Sum(x => x.Monto) })
                .ToDictionaryAsync(x => x.TrayectoId, x => x.Total);

            // Catálogo: necesario para los trayectos sin capturas cuyo worker es TI
            var necesitaCatalogo = rowsRaw.Any(r =>
                string.Equals(r.Subarea, SubareaTi, StringComparison.OrdinalIgnoreCase) &&
                !importesCapturas.ContainsKey(r.Item.Id));
            var catalogoMap = necesitaCatalogo ? await CargarCatalogoTrayectosAsync(ctx) : new();

            foreach (var r in rowsRaw)
            {
                if (importesCapturas.TryGetValue(r.Item.Id, out var sumCap) && sumCap > 0m)
                {
                    r.Item.Importe    = sumCap;
                    r.Item.EsCatalogo = false;
                }
                else if (string.Equals(r.Subarea, SubareaTi, StringComparison.OrdinalIgnoreCase) &&
                         r.LugarOrigenId.HasValue && r.LugarDestinoId.HasValue &&
                         catalogoMap.TryGetValue((r.LugarOrigenId.Value, r.LugarDestinoId.Value), out var montoCat))
                {
                    r.Item.Importe    = montoCat;
                    r.Item.EsCatalogo = true;
                }
                else
                {
                    r.Item.Importe    = 0m;
                    r.Item.EsCatalogo = false;
                }
            }

            return rowsRaw.Select(r => r.Item).ToList();
        }

        public async Task SetHoraSalidaReal(int solicitudId, TimeOnly? hora, int registradaPorUserId)
        {
            using var ctx = _factory.CreateDbContext();
            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == solicitudId)
                ?? throw new AbrilException("Solicitud no encontrada.", 404);

            s.HoraSalidaReal                = hora;
            s.HoraSalidaRealRegistradaPorId = hora.HasValue ? registradaPorUserId : (int?)null;
            s.HoraSalidaRealRegistradaAt    = hora.HasValue ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
            await ctx.SaveChangesAsync();
        }

        public async Task SetHoraRetornoReal(int solicitudId, TimeOnly? hora, int registradaPorUserId)
        {
            using var ctx = _factory.CreateDbContext();
            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == solicitudId)
                ?? throw new AbrilException("Solicitud no encontrada.", 404);

            s.HoraRetornoReal                = hora;
            s.HoraRetornoRealRegistradaPorId = hora.HasValue ? registradaPorUserId : (int?)null;
            s.HoraRetornoRealRegistradaAt    = hora.HasValue ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
            await ctx.SaveChangesAsync();
        }

        private const string SubareaTi              = "Tecnología de la Información";
        private const string TipoAreaEstandar       = "Área Estándar";

        /// <summary>
        /// Para cada workerId dado (con area_scope_id), camina hacia arriba en el árbol
        /// area_scope y devuelve el nombre del primer nodo cuyo tipo (area_type.area_type_name)
        /// sea "Área Estándar". Si no encuentra uno, devuelve null para ese worker.
        /// </summary>
        private static async Task<Dictionary<int, string?>> ResolverAreaPorWorkerAsync(
            AppDbContext ctx,
            List<int> workerIds,
            Dictionary<int, int> workerToScope)
        {
            var resultado = new Dictionary<int, string?>();
            if (workerIds.Count == 0) return resultado;

            // Topología: scopeId → (nombre, tipo, padre)
            var nodos = await (
                from sc in ctx.AreaScope
                join it in ctx.AreaItem on sc.AreaItemId equals it.AreaItemId
                join tp in ctx.AreaType on it.AreaTypeId equals tp.AreaTypeId
                select new
                {
                    sc.AreaScopeId,
                    sc.AreaScopeParentId,
                    Nombre = it.AreaItemName,
                    Tipo   = tp.AreaTypeName,
                }
            ).ToDictionaryAsync(
                x => x.AreaScopeId,
                x => (Padre: x.AreaScopeParentId, Nombre: x.Nombre, Tipo: x.Tipo));

            foreach (var workerId in workerIds)
            {
                if (!workerToScope.TryGetValue(workerId, out var startScope)) continue;

                string? nombre = null;
                var seen = new HashSet<int>();
                int? curr = startScope;
                while (curr.HasValue && seen.Add(curr.Value) && nodos.TryGetValue(curr.Value, out var nodo))
                {
                    if (string.Equals(nodo.Tipo, TipoAreaEstandar, StringComparison.OrdinalIgnoreCase))
                    {
                        nombre = nodo.Nombre;
                        break;
                    }
                    curr = nodo.Padre;
                }
                resultado[workerId] = nombre;
            }
            return resultado;
        }

        /// <summary>
        /// Carga el catálogo de trayectos activos en memoria. Llave: (lugar_origen_id, lugar_destino_id).
        /// </summary>
        private static async Task<Dictionary<(int, int), decimal>> CargarCatalogoTrayectosAsync(AppDbContext ctx)
        {
            var rows = await ctx.GaTrayecto
                .Where(g => g.Activo)
                .Select(g => new { g.LugarOrigenId, g.LugarDestinoId, g.Monto })
                .ToListAsync();
            return rows.ToDictionary(r => (r.LugarOrigenId, r.LugarDestinoId), r => r.Monto);
        }
    }
}
