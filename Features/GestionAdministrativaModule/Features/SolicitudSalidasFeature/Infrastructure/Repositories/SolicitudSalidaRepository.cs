using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Repositories
{
    public class SolicitudSalidaRepository : ISolicitudSalidaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public SolicitudSalidaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<SolicitudSalidaFormDataDto> GetFormData()
        {
            using var ctx = _factory.CreateDbContext();

            var motivos = await ctx.GaMotivoSalida
                .Where(m => m.Activo)
                .OrderBy(m => m.Descripcion)
                .Select(m => new MotivoSalidaDto { Id = m.Id, Descripcion = m.Descripcion })
                .ToListAsync();

            var lugares = await (
                from l in ctx.GaLugar
                join p in ctx.Project on l.ProjectId equals p.ProjectId into pGroup
                from p in pGroup.DefaultIfEmpty()
                where l.Activo
                orderby l.Orden
                select new LugarSalidaDto
                {
                    Id            = l.Id,
                    NombreDisplay = l.Tipo == "proyecto" ? (p != null ? p.ProjectDescription : "[Sin proyecto]")
                                  : l.Tipo == "libre"    ? "Otro lugar"
                                  : l.Nombre ?? string.Empty,
                    EsLibre       = l.Tipo == "libre"
                }
            ).ToListAsync();

            return new SolicitudSalidaFormDataDto
            {
                Motivos = motivos,
                Lugares = lugares
            };
        }

        public async Task<List<SolicitudSalidaListItemDto>> GetByUserId(int userId, SolicitudSalidaFiltersDto? filters = null)
        {
            using var ctx = _factory.CreateDbContext();

            var workerId = await ctx.Worker
                .Where(w => w.Person != null && w.Person.UserId == userId)
                .Select(w => (int?)w.Id)
                .FirstOrDefaultAsync();
            if (workerId == null) return new();

            var query = ctx.GaSolicitudSalida
                .Where(s => s.WorkerId == workerId.Value);

            if (filters != null)
            {
                var aprobId = EstadosSalida.Aprobacion.IdFromNombre(filters.EstadoAprobacion);
                if (aprobId.HasValue)
                    query = query.Where(s => s.EstadoAprobacionId == aprobId.Value);

                var rendId = EstadosSalida.Rendicion.IdFromNombre(filters.EstadoRendicion);
                if (rendId.HasValue)
                    query = query.Where(s => s.EstadoRendicionId == rendId.Value);

                if (filters.LugarProyectoId.HasValue)
                {
                    var lugId = filters.LugarProyectoId.Value;
                    query = query.Where(s => ctx.GaSolicitudTrayecto.Any(t =>
                        t.SolicitudId == s.Id &&
                        (t.LugarOrigenId == lugId || t.LugarDestinoId == lugId)));
                }
            }

            var solicitudes = await query
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            if (solicitudes.Count == 0) return new();

            var ids = solicitudes.Select(s => s.Id).ToList();

            // Cargar todos los trayectos en una query, ordenados.
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
                where ids.Contains(t.SolicitudId)
                orderby t.SolicitudId, t.Orden
                select new
                {
                    t.Id, t.SolicitudId, t.Orden, t.HoraSalida, t.HoraRetorno,
                    Motivo       = m != null ? m.Descripcion : (t.MotivoLibre ?? string.Empty),
                    LugarOrigen  = lo == null ? t.LugarOrigenLibre
                                 : lo.Tipo == "proyecto" ? (po != null ? po.ProjectDescription : "[Sin proyecto]")
                                 : lo.Nombre,
                    LugarDestino = ld == null ? t.LugarDestinoLibre
                                 : ld.Tipo == "proyecto" ? (pd != null ? pd.ProjectDescription : "[Sin proyecto]")
                                 : ld.Nombre,
                }
            ).ToListAsync();

            var trayectosBySolicitud = trayectos.GroupBy(t => t.SolicitudId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Orden).ToList());

            var result = new List<SolicitudSalidaListItemDto>(solicitudes.Count);
            foreach (var s in solicitudes)
            {
                trayectosBySolicitud.TryGetValue(s.Id, out var trList);
                trList ??= new();
                var first = trList.FirstOrDefault();
                var last  = trList.LastOrDefault();

                result.Add(new SolicitudSalidaListItemDto
                {
                    Id           = s.Id,
                    FechaSalida  = s.FechaSalida,
                    HoraSalida   = first?.HoraSalida ?? default,
                    HoraRetorno  = last?.HoraRetorno,
                    Motivo       = first?.Motivo ?? string.Empty,
                    LugarOrigen  = first?.LugarOrigen,
                    LugarDestino = last?.LugarDestino,
                    TrayectosCount   = trList.Count,
                    EstadoAprobacion = EstadosSalida.Aprobacion.Nombre(s.EstadoAprobacionId),
                    EstadoRendicion  = EstadosSalida.Rendicion.Nombre(s.EstadoRendicionId),
                    CreatedAt        = s.CreatedAt,
                });
            }
            return result;
        }

        public async Task<(GaSolicitudSalida Solicitud, List<GaSolicitudTrayecto> Trayectos, Worker Solicitante)> Create(SolicitudSalidaCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var solicitante = await ctx.Worker
                .Where(w => w.Person != null && w.Person.UserId == userId)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException("No se encontró un trabajador asociado a su usuario.", 404);

            // Validación BD: motivos referenciados deben existir
            var motivoIds = dto.Trayectos.Where(t => t.MotivoId.HasValue).Select(t => t.MotivoId!.Value).Distinct().ToList();
            if (motivoIds.Count > 0)
            {
                var existentes = await ctx.GaMotivoSalida.Where(m => motivoIds.Contains(m.Id)).Select(m => m.Id).ToListAsync();
                var faltantes = motivoIds.Except(existentes).ToList();
                if (faltantes.Count > 0)
                    throw new AbrilException($"Motivo(s) no encontrado(s): {string.Join(", ", faltantes)}.", 404);
            }

            var now = DateTimeOffset.UtcNow;
            var solicitud = new GaSolicitudSalida
            {
                WorkerId           = solicitante.Id,
                FechaSalida        = dto.FechaSalida,
                EstadoAprobacionId = EstadosSalida.Aprobacion.Pendiente,
                EstadoRendicionId  = EstadosSalida.Rendicion.NoRendido,
                RegistradoPorId    = userId,
                CreatedAt        = now,
                UpdatedAt        = now,
            };

            var trayectosEnts = dto.Trayectos.Select((t, idx) => new GaSolicitudTrayecto
            {
                Orden             = idx,
                HoraSalida        = t.HoraSalida,
                HoraRetorno       = t.HoraRetorno,
                MotivoId          = t.MotivoId,
                MotivoLibre       = string.IsNullOrWhiteSpace(t.MotivoLibre) ? null : t.MotivoLibre.Trim(),
                LugarOrigenId     = t.LugarOrigenId,
                LugarOrigenLibre  = string.IsNullOrWhiteSpace(t.LugarOrigenLibre) ? null : t.LugarOrigenLibre.Trim(),
                LugarDestinoId    = t.LugarDestinoId,
                LugarDestinoLibre = string.IsNullOrWhiteSpace(t.LugarDestinoLibre) ? null : t.LugarDestinoLibre.Trim(),
            }).ToList();

            // Transacción explícita (Npgsql retry strategy compatible)
            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var tx = await ctx.Database.BeginTransactionAsync();
                ctx.GaSolicitudSalida.Add(solicitud);
                await ctx.SaveChangesAsync();

                foreach (var tr in trayectosEnts)
                {
                    tr.SolicitudId = solicitud.Id;
                }
                ctx.GaSolicitudTrayecto.AddRange(trayectosEnts);
                await ctx.SaveChangesAsync();
                await tx.CommitAsync();
            });

            return (solicitud, trayectosEnts, solicitante);
        }

        public async Task SetAprobadorWorkerId(int solicitudId, int aprobadorWorkerId)
        {
            using var ctx = _factory.CreateDbContext();
            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == solicitudId);
            if (s == null) return;
            s.AprobadorWorkerId = aprobadorWorkerId;
            s.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task<GaSolicitudSalida?> Aprobar(int solicitudId)
        {
            using var ctx = _factory.CreateDbContext();
            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == solicitudId);
            if (s == null || s.EstadoAprobacionId != EstadosSalida.Aprobacion.Pendiente) return null;
            s.EstadoAprobacionId = EstadosSalida.Aprobacion.Aprobado;
            s.FechaDecision    = DateTimeOffset.UtcNow;
            s.UpdatedAt        = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
            return s;
        }

        public async Task<GaSolicitudSalida?> Rechazar(int solicitudId, string? motivoRechazo)
        {
            using var ctx = _factory.CreateDbContext();
            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == solicitudId);
            if (s == null || s.EstadoAprobacionId != EstadosSalida.Aprobacion.Pendiente) return null;
            s.EstadoAprobacionId = EstadosSalida.Aprobacion.Rechazado;
            s.MotivoRechazo    = string.IsNullOrWhiteSpace(motivoRechazo) ? null : motivoRechazo.Trim();
            s.FechaDecision    = DateTimeOffset.UtcNow;
            s.UpdatedAt        = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
            return s;
        }

        public async Task<SolicitudSalidaDetalleDto?> GetDetalleForUser(int solicitudId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Carga worker + subarea para regla TI ("Tecnología de la Información")
            var workerInfo = await ctx.Worker
                .Where(w => w.Person != null && w.Person.UserId == userId)
                .Select(w => new { w.Id, w.Subarea })
                .FirstOrDefaultAsync();
            if (workerInfo == null) return null;

            var solicitud = await ctx.GaSolicitudSalida
                .Where(s => s.Id == solicitudId && s.WorkerId == workerInfo.Id)
                .Select(s => new
                {
                    s.Id, s.FechaSalida, s.EstadoAprobacionId, s.EstadoRendicionId,
                    s.CreatedAt, s.MotivoRechazo
                })
                .FirstOrDefaultAsync();
            if (solicitud == null) return null;

            // Trayectos con su info resuelta. Cargamos LugarOrigenId/LugarDestinoId crudos
            // para poder hacer el match contra ga_trayecto después.
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
                where t.SolicitudId == solicitudId
                orderby t.Orden
                select new
                {
                    Dto = new TrayectoDetalleDto
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
                }
            ).ToListAsync();

            var trayectosListado = trayectosRaw.Select(x => x.Dto).ToList();

            // Capturas
            var trayectoIds = trayectosListado.Select(t => t.Id).ToList();
            var capsByTrayecto = new Dictionary<int, List<SolicitudSalidaCapturaDto>>();
            if (trayectoIds.Count > 0)
            {
                var capsRaw = await ctx.GaSolicitudCaptura
                    .Where(c => trayectoIds.Contains(c.TrayectoId))
                    .OrderBy(c => c.UploadedAt)
                    .Select(c => new
                    {
                        c.TrayectoId,
                        Dto = new SolicitudSalidaCapturaDto
                        {
                            Id         = c.Id,
                            ImageUrl   = c.ImageUrl,
                            Filename   = c.Filename,
                            Monto      = c.Monto,
                            UploadedAt = c.UploadedAt,
                        }
                    })
                    .ToListAsync();

                capsByTrayecto = capsRaw.GroupBy(x => x.TrayectoId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Dto).ToList());
            }

            // Catálogo de trayectos (solo si TI). Cargamos todo el catálogo activo y mapeamos por (origen, destino).
            var esTI = string.Equals(workerInfo.Subarea, SubareaTi, StringComparison.OrdinalIgnoreCase);
            var catalogoMap = esTI ? await CargarCatalogoTrayectosAsync(ctx) : new();

            foreach (var raw in trayectosRaw)
            {
                if (capsByTrayecto.TryGetValue(raw.Dto.Id, out var list))
                    raw.Dto.Capturas = list;

                var sumCapturas = raw.Dto.Capturas.Sum(c => c.Monto);

                if (esTI && raw.LugarOrigenId.HasValue && raw.LugarDestinoId.HasValue &&
                    catalogoMap.TryGetValue((raw.LugarOrigenId.Value, raw.LugarDestinoId.Value), out var montoCat))
                {
                    raw.Dto.MontoCatalogo = montoCat;
                }

                raw.Dto.MontoTotal = sumCapturas > 0
                    ? sumCapturas
                    : (raw.Dto.MontoCatalogo ?? 0m);
            }

            return new SolicitudSalidaDetalleDto
            {
                Id               = solicitud.Id,
                FechaSalida      = solicitud.FechaSalida,
                EstadoAprobacion = EstadosSalida.Aprobacion.Nombre(solicitud.EstadoAprobacionId),
                EstadoRendicion  = EstadosSalida.Rendicion.Nombre(solicitud.EstadoRendicionId),
                CreatedAt        = solicitud.CreatedAt,
                MotivoRechazo    = solicitud.MotivoRechazo,
                Trayectos        = trayectosListado,
            };
        }

        public async Task<SolicitudSalidaFilterDataDto> GetFilterData(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Solo lugares activos de tipo "proyecto" (los útiles para filtrar — los fijos suelen ser oficinas, no distinguen)
            var lugaresProyecto = await (
                from l in ctx.GaLugar
                join p in ctx.Project on l.ProjectId equals p.ProjectId
                where l.Tipo == "proyecto" && l.Activo
                orderby p.ProjectDescription
                select new LugarProyectoOptionDto
                {
                    Id            = l.Id,
                    NombreDisplay = p.ProjectDescription,
                }
            ).ToListAsync();

            return new SolicitudSalidaFilterDataDto { LugaresProyecto = lugaresProyecto };
        }

        private const string SubareaTi = "Tecnología de la Información";

        /// <summary>
        /// Carga el catálogo de trayectos activos como un mapa (lugar_origen_id, lugar_destino_id) -> monto.
        /// </summary>
        private static async Task<Dictionary<(int, int), decimal>> CargarCatalogoTrayectosAsync(AppDbContext ctx)
        {
            var rows = await ctx.GaTrayecto
                .Where(g => g.Activo)
                .Select(g => new { g.LugarOrigenId, g.LugarDestinoId, g.Monto })
                .ToListAsync();
            return rows.ToDictionary(r => (r.LugarOrigenId, r.LugarDestinoId), r => r.Monto);
        }

        public async Task<GaSolicitudTrayecto?> GetTrayectoForUploadingCapturas(int trayectoId, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            return await (
                from t in ctx.GaSolicitudTrayecto
                join s in ctx.GaSolicitudSalida on t.SolicitudId equals s.Id
                join w in ctx.Worker            on s.WorkerId    equals w.Id
                join per in ctx.Person          on w.PersonId    equals (int?)per.PersonId
                where t.Id == trayectoId
                   && per.UserId == userId
                   && s.EstadoAprobacionId == EstadosSalida.Aprobacion.Aprobado
                   && s.EstadoRendicionId  == EstadosSalida.Rendicion.NoRendido
                select t
            ).FirstOrDefaultAsync();
        }

        public async Task<List<SolicitudSalidaCapturaDto>> InsertCapturas(
            int trayectoId,
            IEnumerable<(string Url, string? ItemId, string Filename, decimal Monto)> items,
            int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;
            var entities = items.Select(it => new GaSolicitudCaptura
            {
                TrayectoId   = trayectoId,
                ImageUrl     = it.Url,
                ImageItemId  = it.ItemId,
                Filename     = it.Filename,
                Monto        = it.Monto,
                UploadedById = userId,
                UploadedAt   = now,
            }).ToList();

            ctx.GaSolicitudCaptura.AddRange(entities);
            await ctx.SaveChangesAsync();

            return entities.Select(c => new SolicitudSalidaCapturaDto
            {
                Id         = c.Id,
                ImageUrl   = c.ImageUrl,
                Filename   = c.Filename,
                Monto      = c.Monto,
                UploadedAt = c.UploadedAt,
            }).ToList();
        }
    }
}
