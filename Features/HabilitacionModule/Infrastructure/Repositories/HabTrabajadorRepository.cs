using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores;
using Abril_Backend.Features.Habilitacion.Infrastructure.Helpers;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class HabTrabajadorRepository : IHabTrabajadorRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public HabTrabajadorRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<(List<WorkerHabilitacionListDto> Items, int Total)> GetWorkersHabilitacionAsync(
            string? search, int? empresaId, int? proyectoId,
            string? estadoHabilitacion, string? contratistaCasa,
            int page, int pageSize)
        {
            using var ctx = _factory.CreateDbContext();

            var itemsEmoIds = await ctx.SsItemTrabajador
                .Where(i => i.Nombre.Contains("EMO"))
                .Select(i => i.Id)
                .ToListAsync();

            var baseQuery = ctx.Worker
                .Select(w => new
                {
                    Worker = w,
                    LatestVinc = ctx.WorkerVinculacion
                        .Where(v => v.WorkerId == w.Id && v.FechaFin == null)
                        .OrderByDescending(v => v.CreatedAt)
                        .ThenByDescending(v => v.Id)
                        .FirstOrDefault(),
                    EstadoCalc =
                        (ctx.SsHabTrabajador.Any(h => h.WorkerId == w.Id &&
                             (h.Estado == "Falta" || h.Estado == "Rechazado" || h.Estado == "Vencido") &&
                             !(w.ContrataCasa == "Casa" && itemsEmoIds.Contains(h.ItemId)))
                         || (w.ContrataCasa == "Casa" && !ctx.WorkerEmo.Any(e => e.WorkerId == w.Id &&
                             e.Activo && (e.Estado == "Vigente" || e.Estado == "Convalidado"))))
                        ? "No Autorizado"
                        : ctx.SsHabTrabajador.Any(h => h.WorkerId == w.Id && h.Estado == "En Plazo" &&
                            !(w.ContrataCasa == "Casa" && itemsEmoIds.Contains(h.ItemId)))
                        ? "Autorizado Temporalmente"
                        : "Habilitado"
                });

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                baseQuery = baseQuery.Where(x =>
                    (x.Worker.ApellidoNombre != null && x.Worker.ApellidoNombre.ToLower().Contains(s)) ||
                    (x.Worker.Dni != null && x.Worker.Dni.Contains(s)));
            }

            if (empresaId.HasValue)
                baseQuery = baseQuery.Where(x => x.LatestVinc != null && x.LatestVinc.EmpresaId == empresaId.Value);

            if (proyectoId.HasValue)
                baseQuery = baseQuery.Where(x => x.LatestVinc != null && x.LatestVinc.ProyectoId == proyectoId.Value);

            if (!string.IsNullOrWhiteSpace(contratistaCasa))
            {
                var cc = contratistaCasa.Trim();
                baseQuery = baseQuery.Where(x => x.Worker.ContrataCasa == cc);
            }

            if (!string.IsNullOrWhiteSpace(estadoHabilitacion))
                baseQuery = baseQuery.Where(x => x.EstadoCalc == estadoHabilitacion);

            var total = await baseQuery.CountAsync();

            var pageRows = await baseQuery
                .OrderBy(x => x.Worker.ApellidoNombre)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var empresaIds = pageRows
                .Where(r => r.LatestVinc != null && r.LatestVinc.EmpresaId.HasValue)
                .Select(r => r.LatestVinc!.EmpresaId!.Value)
                .Distinct()
                .ToList();

            var proyectoIds = pageRows
                .Where(r => r.LatestVinc != null && r.LatestVinc.ProyectoId.HasValue)
                .Select(r => r.LatestVinc!.ProyectoId!.Value)
                .Distinct()
                .ToList();

            var empresas = await ctx.SsEmpresaContratista
                .Where(e => empresaIds.Contains(e.Id))
                .Select(e => new { e.Id, e.RazonSocial })
                .ToListAsync();

            var proyectos = await ctx.Set<Projects>()
                .Where(p => proyectoIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Nombre })
                .ToListAsync();

            var empresaMap = empresas.ToDictionary(e => e.Id, e => e.RazonSocial);
            var proyectoMap = proyectos.ToDictionary(p => p.Id, p => p.Nombre);

            var items = pageRows.Select(r => new WorkerHabilitacionListDto
            {
                WorkerId = r.Worker.Id,
                ApellidoNombre = r.Worker.ApellidoNombre ?? string.Empty,
                Dni = r.Worker.Dni ?? string.Empty,
                EmpresaId = r.LatestVinc?.EmpresaId,
                EmpresaNombre = r.LatestVinc?.EmpresaId is int eid && empresaMap.TryGetValue(eid, out var en) ? en : null,
                ProyectoActualId = r.LatestVinc?.ProyectoId,
                ProyectoActual = r.LatestVinc?.ProyectoId is int pid && proyectoMap.TryGetValue(pid, out var pn) ? pn : null,
                EstadoHabilitacion = r.EstadoCalc,
                Categoria = r.Worker.Categoria,
                Ocupacion = r.Worker.Ocupacion,
                EstadoWorker = r.Worker.Estado ?? "ACTIVO"
            }).ToList();

            return (items, total);
        }

        public async Task<List<WorkerEntregableDto>> GetEntregablesWorkerAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var workerType = string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase)
                ? "CASA"
                : "CONTRATISTA";

            var esContratista = string.Equals(worker.ContrataCasa?.Trim(), "Contratista", StringComparison.OrdinalIgnoreCase);

            var items = await ctx.SsItemTrabajador
                .Where(i => i.Activo && (i.AplicaA == "TODOS" || i.AplicaA == workerType))
                .OrderBy(i => i.Orden)
                .ToListAsync();

            items = items
                .Where(i => CsvContiene(i.AplicaCategoria, worker.Categoria))
                .Where(i => CsvContiene(i.AplicaObraOficina, worker.ObraOficina))
                .Where(i => !CsvExcluye(i.ExcluyeObraOficina, worker.ObraOficina))
                .Where(i => !esContratista || !CsvExcluye(i.ExcluyeCategoriaContratista, worker.Categoria))
                .ToList();

            var emoItems = items.Where(i => i.Nombre.Contains("EMO", StringComparison.OrdinalIgnoreCase)).ToList();
            var nonEmoItems = items.Except(emoItems).ToList();
            var nonEmoIds = nonEmoItems.Select(i => i.Id).ToList();

            var existentes = await ctx.SsHabTrabajador
                .Where(h => h.WorkerId == workerId && nonEmoIds.Contains(h.ItemId))
                .ToListAsync();

            var nonEmoMap = nonEmoItems.ToDictionary(i => i.Id);

            var entregables = existentes
                .Where(h => nonEmoMap.ContainsKey(h.ItemId))
                .Select(h =>
                {
                    var item = nonEmoMap[h.ItemId];
                    return new WorkerEntregableDto
                    {
                        Id = h.Id,
                        ItemId = h.ItemId,
                        NombreItem = item.Nombre,
                        Estado = h.Estado,
                        Vigencia = h.Vigencia,
                        ArchivoUrl = h.ArchivoUrl,
                        ObsAbril = h.ObsAbril,
                        ObsContratista = h.ObsContratista,
                        RequiereVigencia = item.RequiereVigencia,
                        EsSctrVidaley = item.EsSctrVidaley,
                        Responsable = item.Responsable
                    };
                })
                .ToList();

            if (emoItems.Count > 0)
            {
                var ultimoEmo = await ctx.WorkerEmo
                    .Where(e => e.WorkerId == workerId && e.Activo)
                    .OrderByDescending(e => e.FechaEmo)
                    .FirstOrDefaultAsync();

                var vigente = ultimoEmo != null
                    && (ultimoEmo.Estado == "Vigente" || ultimoEmo.Estado == "Convalidado");

                DateTime? vigenciaEmo = null;
                if (vigente)
                {
                    var fechaVenc = ultimoEmo!.FechaVencimientoCalculada ?? ultimoEmo.FechaVencimiento;
                    if (fechaVenc.HasValue)
                        vigenciaEmo = fechaVenc.Value.ToDateTime(TimeOnly.MinValue);
                }

                foreach (var item in emoItems)
                {
                    entregables.Add(new WorkerEntregableDto
                    {
                        Id = 0,
                        ItemId = item.Id,
                        NombreItem = item.Nombre,
                        Estado = vigente ? "Aprobado" : "Falta",
                        Vigencia = vigente ? vigenciaEmo : null,
                        ArchivoUrl = null,
                        ObsAbril = "Gestionado por módulo SSOMA",
                        ObsContratista = null,
                        RequiereVigencia = item.RequiereVigencia,
                        EsSctrVidaley = item.EsSctrVidaley,
                        Responsable = item.Responsable
                    });
                }
            }

            var ordenMap = items.ToDictionary(i => i.Id, i => i.Orden);
            return entregables.OrderBy(d => ordenMap[d.ItemId]).ToList();
        }

        public async Task<SsHabTrabajador> UpdateEntregableAsync(int id, WorkerEntregableUpdateDto dto, int? userId, int? empresaId = null)
        {
            using var ctx = _factory.CreateDbContext();

            var entregable = await ctx.SsHabTrabajador
                .Include(h => h.Item)
                .FirstOrDefaultAsync(h => h.Id == id)
                ?? throw new AbrilException("Entregable no encontrado.", 404);

            if (!string.IsNullOrWhiteSpace(dto.ArchivoUrl) && dto.ArchivoUrl != entregable.ArchivoUrl)
            {
                var versionActual = await ctx.SsHabDocumentoVersion
                    .CountAsync(v => v.HabTrabajadorId == id);

                ctx.SsHabDocumentoVersion.Add(new SsHabDocumentoVersion
                {
                    HabTrabajadorId = id,
                    Version = versionActual + 1,
                    ArchivoUrl = dto.ArchivoUrl,
                    SubidoPorUserId = userId,
                    SubidoPorEmpresaId = empresaId,
                    EstadoAlSubir = dto.Estado,
                    CreatedAt = DateTime.UtcNow
                });
            }

            entregable.Estado = dto.Estado;
            entregable.Vigencia = HabilitacionDateHelper.ResolverVigencia(entregable.Item?.RequiereVigencia ?? true, dto.Estado, dto.Vigencia);
            if (dto.ArchivoUrl is not null) entregable.ArchivoUrl = dto.ArchivoUrl;
            if (dto.ObsAbril is not null) entregable.ObsAbril = dto.ObsAbril;
            if (dto.ObsContratista is not null) entregable.ObsContratista = dto.ObsContratista;
            entregable.UpdatedAt = DateTime.UtcNow;

            if (string.Equals(dto.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase))
            {
                entregable.AprobadoPor = userId;
                entregable.FechaAprobacion = DateTime.UtcNow;
            }

            await ctx.SaveChangesAsync();
            return entregable;
        }

        public async Task<List<SsHabDocumentoVersion>> GetVersionesDocumentoAsync(int habTrabajadorId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsHabDocumentoVersion
                .Where(v => v.HabTrabajadorId == habTrabajadorId)
                .OrderByDescending(v => v.Version)
                .ToListAsync();
        }

        public async Task CambiarObraAsync(int workerId, WorkerCambiarObraDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var fechaCambio = DateOnly.FromDateTime(dto.FechaCambio);

            var activas = await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .ToListAsync();

            int? empresaPrevia = activas.Select(a => a.EmpresaId).FirstOrDefault();

            if (dto.NuevaEmpresaId.HasValue)
                await ValidarExclusividadEmpresaAsync(ctx, workerId, dto.NuevaEmpresaId.Value);

            foreach (var v in activas)
            {
                v.FechaFin = fechaCambio;
                v.UpdatedAt = DateTimeOffset.UtcNow;
            }

            var nueva = new WorkerVinculacion
            {
                WorkerId = workerId,
                EmpresaId = dto.NuevaEmpresaId ?? empresaPrevia,
                ProyectoId = dto.NuevoProyectoId,
                FechaInicio = fechaCambio,
                CreatedAt = DateTimeOffset.UtcNow
            };

            ctx.WorkerVinculacion.Add(nueva);
            await ctx.SaveChangesAsync();
        }

        public async Task ReingresoAsync(int workerId, int proyectoId, int empresaId)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            await ValidarExclusividadEmpresaAsync(ctx, workerId, empresaId);

            worker.Estado = "ACTIVO";
            worker.FechaRetiro = null;
            worker.UpdatedAt = DateTimeOffset.UtcNow;

            var nueva = new WorkerVinculacion
            {
                WorkerId = workerId,
                EmpresaId = empresaId,
                ProyectoId = proyectoId,
                FechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
                CreatedAt = DateTimeOffset.UtcNow
            };

            ctx.WorkerVinculacion.Add(nueva);
            await ctx.SaveChangesAsync();
        }

        public async Task<int?> GetEmpresaActivaWorkerAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .Select(v => v.EmpresaId)
                .FirstOrDefaultAsync();
        }

        public async Task InicializarEntregablesAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var workerType = string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase)
                ? "CASA"
                : "CONTRATISTA";

            var todosItems = await ctx.SsItemTrabajador
                .Where(i => i.Activo)
                .ToListAsync();

            var esContratista = string.Equals(worker.ContrataCasa?.Trim(), "Contratista", StringComparison.OrdinalIgnoreCase);

            var itemsAplicables = todosItems
                .Where(i => i.AplicaA == "TODOS" ||
                            (i.AplicaA == "CASA" && workerType == "CASA") ||
                            (i.AplicaA == "CONTRATISTA" && workerType == "CONTRATISTA"))
                .Where(i => CsvContiene(i.AplicaCategoria, worker.Categoria))
                .Where(i => CsvContiene(i.AplicaObraOficina, worker.ObraOficina))
                .Where(i => !CsvExcluye(i.ExcluyeObraOficina, worker.ObraOficina))
                .Where(i => !esContratista || !CsvExcluye(i.ExcluyeCategoriaContratista, worker.Categoria))
                .ToList();

            var itemIds = itemsAplicables.Select(i => i.Id).ToList();

            var existentesIds = (await ctx.SsHabTrabajador
                .Where(h => h.WorkerId == workerId && itemIds.Contains(h.ItemId))
                .Select(h => h.ItemId)
                .ToListAsync())
                .ToHashSet();

            var nuevos = itemsAplicables
                .Where(i => !existentesIds.Contains(i.Id))
                .Select(i => new SsHabTrabajador
                {
                    WorkerId = workerId,
                    ItemId = i.Id,
                    Estado = "Falta",
                    Vigencia = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                })
                .ToList();

            if (nuevos.Count > 0)
            {
                ctx.SsHabTrabajador.AddRange(nuevos);
                await ctx.SaveChangesAsync();
            }
        }

        private static bool CsvContiene(string? csv, string? valor)
            => csv == null || csv.Split(',', StringSplitOptions.TrimEntries)
                   .Contains(valor ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        private static bool CsvExcluye(string? csv, string? valor)
            => csv != null && csv.Split(',', StringSplitOptions.TrimEntries)
                   .Contains(valor ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        private static async Task ValidarExclusividadEmpresaAsync(
            AppDbContext ctx, int workerId, int empresaSolicitanteId)
        {
            var activa = await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .FirstOrDefaultAsync();

            if (activa == null || !activa.EmpresaId.HasValue) return;
            if (activa.EmpresaId.Value == empresaSolicitanteId) return;

            ctx.SsHabBloqueoLog.Add(new SsHabBloqueoLog
            {
                WorkerId = workerId,
                EmpresaSolicitanteId = empresaSolicitanteId,
                EmpresaPropietariaId = activa.EmpresaId.Value,
                Motivo = "Trabajador con vinculación activa en otra empresa.",
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();

            throw new AbrilException(
                "Este trabajador está activo en otra empresa y no puede ser habilitado.",
                409);
        }
    }
}
