using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Repositories
{
    public class GestionSalidaRepository : IGestionSalidaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public GestionSalidaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters)
        {
            using var ctx = _factory.CreateDbContext();

            // 1. Filtrar solicitudes (cabecera)
            var solicitudQuery = ctx.GaSolicitudSalida.AsQueryable();

            if (filters.WorkerId.HasValue)
                solicitudQuery = solicitudQuery.Where(s => s.WorkerId == filters.WorkerId.Value);

            if (!string.IsNullOrWhiteSpace(filters.EstadoRendicion))
                solicitudQuery = solicitudQuery.Where(s => s.EstadoRendicion == filters.EstadoRendicion);

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
                orderby s.EstadoAprobacion == "Pendiente" ? 0 : 1,
                        s.CreatedAt descending
                select new
                {
                    s.Id, s.WorkerId, WorkerInternalId = w.Id,
                    Trabajador = per != null ? (per.FullName ?? "[Sin nombre]") : "[Sin nombre]",
                    s.FechaSalida, s.EstadoAprobacion, s.EstadoRendicion, s.CreatedAt
                }
            ).ToListAsync();

            if (solicitudes.Count == 0) return new();

            var solicitudIds = solicitudes.Select(s => s.Id).ToList();

            // 2. Trayectos para info agregada (motivo, origen, destino, horas)
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

            // 3. Para cada solicitud, determinar PuedeRendirse: todos los trayectos con ≥1 captura
            // Trayectos con al menos 1 captura
            var trayectoIds = trayectos.Select(t => t.Id).ToList();
            var trayectosConCapturas = trayectoIds.Count == 0
                ? new HashSet<int>()
                : (await ctx.GaSolicitudCaptura
                    .Where(c => trayectoIds.Contains(c.TrayectoId))
                    .Select(c => c.TrayectoId)
                    .Distinct()
                    .ToListAsync()).ToHashSet();

            // 4. Armar resultado
            var result = new List<GestionSalidaListItemDto>(solicitudes.Count);
            foreach (var s in solicitudes)
            {
                trayectosBySol.TryGetValue(s.Id, out var trList);
                trList ??= new();
                var first = trList.FirstOrDefault();
                var last  = trList.LastOrDefault();

                var puedeRendir = trList.Count > 0
                                 && trList.All(t => trayectosConCapturas.Contains(t.Id));

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
                    EstadoAprobacion = s.EstadoAprobacion,
                    EstadoRendicion  = s.EstadoRendicion,
                    CreatedAt        = s.CreatedAt,
                    PuedeRendirse    = puedeRendir,
                });
            }

            return result;
        }

        public async Task<GestionSalidaFilterDataDto> GetFilterData()
        {
            using var ctx = _factory.CreateDbContext();

            var workerIds = await ctx.GaSolicitudSalida
                .Select(s => s.WorkerId)
                .Distinct()
                .ToListAsync();

            var trabajadores = await (
                from w   in ctx.Worker
                where workerIds.Contains(w.Id)
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

            return new GestionSalidaFilterDataDto
            {
                Trabajadores    = trabajadores,
                LugaresProyecto = lugaresProyecto,
            };
        }

        public async Task Aprobar(int id, int reviewerUserId)
        {
            using var ctx = _factory.CreateDbContext();
            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new AbrilException("Solicitud no encontrada.", 404);
            if (s.EstadoAprobacion != "Pendiente")
                throw new AbrilException("Solo se pueden aprobar solicitudes en estado Pendiente.", 400);
            s.EstadoAprobacion = "Aprobado";
            s.UpdatedAt        = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task Rechazar(int id, int reviewerUserId)
        {
            using var ctx = _factory.CreateDbContext();
            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new AbrilException("Solicitud no encontrada.", 404);
            if (s.EstadoAprobacion != "Pendiente")
                throw new AbrilException("Solo se pueden rechazar solicitudes en estado Pendiente.", 400);
            s.EstadoAprobacion = "Rechazado";
            s.UpdatedAt        = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task<List<int>> CrearRendicionYMarcarBulk(
            IEnumerable<int> ids,
            int userId,
            string pdfUrl,
            string? pdfItemId,
            string pdfFilename)
        {
            using var ctx = _factory.CreateDbContext();
            var idsList = ids?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            var solicitudes = await ctx.GaSolicitudSalida
                .Where(s => idsList.Contains(s.Id)
                         && s.EstadoAprobacion == "Aprobado"
                         && s.EstadoRendicion  == "No rendido")
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
                    PdfUrl       = pdfUrl,
                    PdfItemId    = pdfItemId,
                    PdfFilename  = pdfFilename,
                    RendidoPorId = userId,
                    RendidoAt    = now,
                };
                ctx.GaRendicion.Add(rendicion);
                await ctx.SaveChangesAsync();

                foreach (var s in solicitudes)
                {
                    s.EstadoRendicion = "Rendido";
                    s.RendicionId     = rendicion.Id;
                    s.UpdatedAt       = now;
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
                         && s.EstadoAprobacion == "Aprobado"
                         && s.EstadoRendicion  == "No rendido")
                .Select(s => s.Id)
                .ToListAsync();
        }

        public async Task<List<int>> GetIdsConTrayectosSinCapturas(IEnumerable<int> ids)
        {
            using var ctx = _factory.CreateDbContext();
            var idsList = ids?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            // Una solicitud "está incompleta" si tiene AL MENOS UN trayecto sin ninguna captura.
            // Si la solicitud no tiene trayectos, también está incompleta (no debería pasar, pero defensivo).
            var solicitudesConTrayectoIncompleto = await (
                from s in ctx.GaSolicitudSalida
                where idsList.Contains(s.Id)
                let totalTr = ctx.GaSolicitudTrayecto.Count(t => t.SolicitudId == s.Id)
                let trCompletos = ctx.GaSolicitudTrayecto.Count(t =>
                    t.SolicitudId == s.Id &&
                    ctx.GaSolicitudCaptura.Any(c => c.TrayectoId == t.Id))
                where totalTr == 0 || trCompletos < totalTr
                select s.Id
            ).ToListAsync();

            return solicitudesConTrayectoIncompleto;
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
                    s.Id, WorkerInternalId = w.Id,
                    Trabajador = per != null ? (per.FullName ?? "[Sin nombre]") : "[Sin nombre]",
                    s.FechaSalida, s.EstadoAprobacion, s.EstadoRendicion, s.CreatedAt, s.MotivoRechazo,
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
                where t.SolicitudId == id
                orderby t.Orden
                select new GestionSalidaTrayectoDto
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
                }
            ).ToListAsync();

            // Capturas por trayecto
            var trayectoIds = trayectos.Select(t => t.Id).ToList();
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

            return new GestionSalidaDetalleDto
            {
                Id               = head.Id,
                WorkerId         = head.WorkerInternalId,
                Trabajador       = head.Trabajador,
                FechaSalida      = head.FechaSalida,
                EstadoAprobacion = head.EstadoAprobacion,
                EstadoRendicion  = head.EstadoRendicion,
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

            // Una fila = un trayecto. JOIN con solicitud para obtener trabajador/contributor/fecha.
            var rows = await (
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
                select new RendicionItemDto
                {
                    Id               = t.Id,
                    SolicitudId      = s.Id,
                    WorkerId         = w.Id,
                    TrabajadorNombre = per != null ? (per.FullName ?? "") : "",
                    TrabajadorDni    = per != null ? per.DocumentIdentityCode : null,
                    Area             = w.Area,
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
                }
            ).ToListAsync();

            // Importe por trayecto (suma de capturas)
            var trayectoIds = rows.Select(r => r.Id).ToList();
            var importes = await ctx.GaSolicitudCaptura
                .Where(c => trayectoIds.Contains(c.TrayectoId))
                .GroupBy(c => c.TrayectoId)
                .Select(g => new { TrayectoId = g.Key, Total = g.Sum(x => x.Monto) })
                .ToDictionaryAsync(x => x.TrayectoId, x => x.Total);

            foreach (var r in rows)
                r.Importe = importes.TryGetValue(r.Id, out var t) ? t : 0m;

            return rows;
        }
    }
}
