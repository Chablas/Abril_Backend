using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.SctrVidaley;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Helpers;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class SctrVidaLeyRepository : ISctrVidaLeyRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogger<SctrVidaLeyRepository> _logger;
        private readonly ISharePointHabService _sharePoint;

        public SctrVidaLeyRepository(
            IDbContextFactory<AppDbContext> factory,
            ILogger<SctrVidaLeyRepository> logger,
            ISharePointHabService sharePoint)
        {
            _factory = factory;
            _logger = logger;
            _sharePoint = sharePoint;
        }

        public async Task<(List<SctrVidaLeyDto> Items, int Total)> GetPagedAsync(
            int? empresaId, int? proyectoId, string? tipo,
            int? mes, int? anio, string? estado, int page, int pageSize)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.SsSctrVidaley.AsQueryable();

            if (empresaId.HasValue) query = query.Where(s => s.EmpresaId == empresaId.Value);
            if (proyectoId.HasValue) query = query.Where(s => s.ProyectoId == proyectoId.Value);
            if (!string.IsNullOrWhiteSpace(tipo)) query = query.Where(s => s.Tipo == tipo);
            if (mes.HasValue) query = query.Where(s => s.Mes == mes.Value);
            if (anio.HasValue) query = query.Where(s => s.Anio == anio.Value);
            if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(s => s.Estado == estado);

            var total = await query.CountAsync();

            var pageRows = await query
                .OrderByDescending(s => s.Anio)
                .ThenByDescending(s => s.Mes)
                .ThenByDescending(s => s.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = await BuildDtosAsync(ctx, pageRows);
            return (items, total);
        }

        public async Task<SctrVidaLeyDto?> GetByIdAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsSctrVidaley.FirstOrDefaultAsync(s => s.Id == id);
            if (entity is null) return null;
            var list = await BuildDtosAsync(ctx, new List<SsSctrVidaley> { entity });
            return list.FirstOrDefault();
        }

        public async Task<SctrVidaLeyDto> CreateAsync(SctrVidaLeyCreateDto dto, int empresaId)
        {
            using var ctx = _factory.CreateDbContext();

            var fechaRef = dto.FechaInicio ?? DateTime.UtcNow;
            var mes = dto.Mes != 0 ? dto.Mes : fechaRef.Month;
            var anio = dto.Anio != 0 ? dto.Anio : fechaRef.Year;

            var entity = new SsSctrVidaley
            {
                EmpresaId = empresaId,
                ProyectoId = dto.ProyectoId == 0 ? null : dto.ProyectoId,
                Tipo = dto.Tipo,
                TipoPoliza = string.IsNullOrWhiteSpace(dto.TipoPoliza) ? "Renovacion" : dto.TipoPoliza,
                FechaInicio = dto.FechaInicio.HasValue ? DateTime.SpecifyKind(dto.FechaInicio.Value, DateTimeKind.Utc) : null,
                Mes = mes,
                Anio = anio,
                ArchivoUrl = dto.ArchivoUrl,
                ArchivoUrl2 = dto.ArchivoUrl2,
                Vigencia = dto.Vigencia.HasValue ? DateTime.SpecifyKind(dto.Vigencia.Value, DateTimeKind.Utc) : null,
                Estado = "Enviado",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            ctx.SsSctrVidaley.Add(entity);
            await ctx.SaveChangesAsync();

            var workersDistinct = dto.Workers.GroupBy(w => w.WorkerId).Select(g => g.First()).ToList();

            foreach (var workerInput in workersDistinct)
            {
                ctx.SsSctrVidaLeyWorker.Add(new SsSctrVidaLeyWorker
                {
                    SctrVidaLeyId = entity.Id,
                    WorkerId = workerInput.WorkerId,
                    FechaInicioCobertura = workerInput.FechaInicioCobertura.HasValue
                        ? DateTime.SpecifyKind(workerInput.FechaInicioCobertura.Value, DateTimeKind.Utc)
                        : null
                });
            }
            await ctx.SaveChangesAsync();

            var esAbril = await ctx.Contributor
                .Where(c => c.ContributorId == empresaId)
                .Select(c => c.EsAbril)
                .FirstOrDefaultAsync();

            var estadoHab = esAbril ? "Aprobado" : "Enviado";
            var vigenciaHab = dto.Vigencia.HasValue
                ? DateTime.SpecifyKind(dto.Vigencia.Value, DateTimeKind.Utc)
                : (DateTime?)null;

            var itemNombreBuscar = dto.Tipo == "VIDA_LEY" ? "Vida" : "SCTR";
            var sctrItems = await ctx.SsItemTrabajador
                .Where(i => i.EsSctrVidaley && i.Activo)
                .ToListAsync();
            var item = sctrItems.FirstOrDefault(i =>
                i.Nombre.Contains(itemNombreBuscar, StringComparison.OrdinalIgnoreCase));

            if (item is not null)
            {
                foreach (var workerInput in workersDistinct)
                {
                    var hab = await ctx.SsHabTrabajador
                        .FirstOrDefaultAsync(h => h.WorkerId == workerInput.WorkerId && h.ItemId == item.Id);

                    if (hab is null)
                    {
                        hab = new SsHabTrabajador
                        {
                            WorkerId = workerInput.WorkerId,
                            ItemId = item.Id,
                            Estado = estadoHab,
                            Vigencia = vigenciaHab,
                            ArchivoUrl = dto.ArchivoUrl,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        ctx.SsHabTrabajador.Add(hab);
                    }
                    else
                    {
                        hab.Estado = estadoHab;
                        hab.Vigencia = vigenciaHab;
                        hab.ArchivoUrl = dto.ArchivoUrl;
                        hab.UpdatedAt = DateTime.UtcNow;
                    }
                }
                await ctx.SaveChangesAsync();
            }

            if (esAbril && entity.ProyectoId.HasValue)
            {
                var itemEmpresaId = dto.Tipo == "VIDA_LEY" ? 16 : 15;
                var habEmpresa = await ctx.SsHabEmpresa
                    .FirstOrDefaultAsync(h => h.EmpresaId == entity.EmpresaId
                                           && h.ProyectoId == entity.ProyectoId.Value
                                           && h.ItemId == itemEmpresaId);
                if (habEmpresa is null)
                {
                    ctx.SsHabEmpresa.Add(new SsHabEmpresa
                    {
                        EmpresaId = entity.EmpresaId,
                        ProyectoId = entity.ProyectoId.Value,
                        ItemId = itemEmpresaId,
                        Mes = entity.Mes,
                        Anio = entity.Anio,
                        Estado = "Aprobado",
                        Vigencia = vigenciaHab,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    habEmpresa.Estado = "Aprobado";
                    habEmpresa.Vigencia = vigenciaHab;
                    habEmpresa.UpdatedAt = DateTime.UtcNow;
                }

                entity.Estado = "Aprobado";
                await ctx.SaveChangesAsync();
            }

            var dtos = await BuildDtosAsync(ctx, new List<SsSctrVidaley> { entity });
            return dtos.First();
        }

        public async Task<SctrVidaLeyDto> UpdateAsync(int id, SctrVidaLeyCreateDto dto, int empresaId)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = await ctx.SsSctrVidaley.FirstOrDefaultAsync(s => s.Id == id)
                ?? throw new AbrilException("SCTR/VidaLey no encontrado.", 404);

            if (entity.EmpresaId != empresaId)
                throw new AbrilException("No tiene permiso para editar esta póliza.", 403);

            var archivoReemplazado = !string.IsNullOrWhiteSpace(dto.ArchivoUrl)
                && dto.ArchivoUrl != entity.ArchivoUrl;

            entity.Tipo = dto.Tipo;
            entity.TipoPoliza = string.IsNullOrWhiteSpace(dto.TipoPoliza) ? entity.TipoPoliza : dto.TipoPoliza;
            entity.FechaInicio = dto.FechaInicio.HasValue
                ? DateTime.SpecifyKind(dto.FechaInicio.Value, DateTimeKind.Utc)
                : entity.FechaInicio;
            entity.Mes = dto.Mes;
            entity.Anio = dto.Anio;
            entity.ArchivoUrl = dto.ArchivoUrl;
            entity.ArchivoUrl2 = dto.ArchivoUrl2;
            entity.UpdatedAt = DateTime.UtcNow;

            if (archivoReemplazado && entity.Estado == "Aprobado")
                entity.Estado = "Enviado";

            // Workers: calcular delta
            var workersDistinct = dto.Workers.GroupBy(w => w.WorkerId).Select(g => g.First()).ToList();
            var workerIdsNuevos = workersDistinct.Select(w => w.WorkerId).ToHashSet();

            var workersActuales = await ctx.SsSctrVidaLeyWorker
                .Where(w => w.SctrVidaLeyId == id)
                .ToListAsync();

            var workerIdsActuales = workersActuales.Select(w => w.WorkerId).ToHashSet();
            var workerIdsAgregar = workerIdsNuevos.Except(workerIdsActuales).ToList();
            var workerIdsQuitar = workerIdsActuales.Except(workerIdsNuevos).ToList();

            // Quitar workers
            if (workerIdsQuitar.Count > 0)
            {
                var aEliminar = workersActuales.Where(w => workerIdsQuitar.Contains(w.WorkerId)).ToList();
                ctx.SsSctrVidaLeyWorker.RemoveRange(aEliminar);
            }

            // Agregar workers nuevos
            foreach (var workerInput in workersDistinct.Where(w => workerIdsAgregar.Contains(w.WorkerId)))
            {
                ctx.SsSctrVidaLeyWorker.Add(new SsSctrVidaLeyWorker
                {
                    SctrVidaLeyId = id,
                    WorkerId = workerInput.WorkerId,
                    FechaInicioCobertura = workerInput.FechaInicioCobertura.HasValue
                        ? DateTime.SpecifyKind(workerInput.FechaInicioCobertura.Value, DateTimeKind.Utc)
                        : null
                });
            }

            // Actualizar FechaInicioCobertura de workers existentes si cambió
            foreach (var workerActual in workersActuales.Where(w => workerIdsNuevos.Contains(w.WorkerId)))
            {
                var input = workersDistinct.First(w => w.WorkerId == workerActual.WorkerId);
                workerActual.FechaInicioCobertura = input.FechaInicioCobertura.HasValue
                    ? DateTime.SpecifyKind(input.FechaInicioCobertura.Value, DateTimeKind.Utc)
                    : workerActual.FechaInicioCobertura;
            }

            await ctx.SaveChangesAsync();

            var itemNombreBuscar = dto.Tipo == "VIDA_LEY" ? "Vida" : "SCTR";
            var sctrItems = await ctx.SsItemTrabajador
                .Where(i => i.EsSctrVidaley && i.Activo)
                .ToListAsync();
            var item = sctrItems.FirstOrDefault(i =>
                i.Nombre.Contains(itemNombreBuscar, StringComparison.OrdinalIgnoreCase));

            if (item is not null)
            {
                // Propagar estado a ss_hab_trabajador para nuevos workers
                foreach (var workerInput in workersDistinct.Where(w => workerIdsAgregar.Contains(w.WorkerId)))
                {
                    var hab = await ctx.SsHabTrabajador
                        .FirstOrDefaultAsync(h => h.WorkerId == workerInput.WorkerId && h.ItemId == item.Id);

                    if (hab is null)
                    {
                        ctx.SsHabTrabajador.Add(new SsHabTrabajador
                        {
                            WorkerId = workerInput.WorkerId,
                            ItemId = item.Id,
                            Estado = "Enviado",
                            ArchivoUrl = dto.ArchivoUrl,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        hab.Estado = "Enviado";
                        hab.ArchivoUrl = dto.ArchivoUrl;
                        hab.UpdatedAt = DateTime.UtcNow;
                    }
                }

                // Quitar workers → Estado=Falta + limpiar ArchivoUrl
                if (workerIdsQuitar.Count > 0)
                {
                    var habsQuitar = await ctx.SsHabTrabajador
                        .Where(h => h.ItemId == item.Id && workerIdsQuitar.Contains(h.WorkerId))
                        .ToListAsync();

                    foreach (var hab in habsQuitar)
                    {
                        hab.Estado = "Falta";
                        hab.ArchivoUrl = null;
                        hab.UpdatedAt = DateTime.UtcNow;
                    }
                }

                // Si se reemplazó archivo y la póliza volvió a Enviado, actualizar estado en ss_hab_trabajador
                if (archivoReemplazado)
                {
                    var habsExistentes = await ctx.SsHabTrabajador
                        .Where(h => h.ItemId == item.Id && workerIdsNuevos.Contains(h.WorkerId))
                        .ToListAsync();

                    foreach (var hab in habsExistentes)
                    {
                        if (hab.Estado == "Aprobado")
                        {
                            hab.Estado = "Enviado";
                            hab.ArchivoUrl = dto.ArchivoUrl;
                            hab.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }

                await ctx.SaveChangesAsync();
            }

            var dtos = await BuildDtosAsync(ctx, new List<SsSctrVidaley> { entity });
            return dtos.First();
        }

        public async Task<SctrVidaLeyDto> AprobarAsync(int id, SctrVidaLeyAprobarDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = await ctx.SsSctrVidaley.FirstOrDefaultAsync(s => s.Id == id)
                ?? throw new AbrilException("SCTR/VidaLey no encontrado.", 404);

            var itemNombreBuscar = entity.Tipo == "VIDA_LEY" ? "Vida" : "SCTR";
            var sctrItems = await ctx.SsItemTrabajador
                .Where(i => i.EsSctrVidaley && i.Activo)
                .ToListAsync();
            var item = sctrItems.FirstOrDefault(i =>
                i.Nombre.Contains(itemNombreBuscar, StringComparison.OrdinalIgnoreCase));

            var aprobados = dto.WorkerIdsAprobados.Distinct().ToList();
            var rechazados = dto.WorkerIdsRechazados.Distinct().ToList();
            var afectados = aprobados.Concat(rechazados).Distinct().ToList();

            if (item is not null && afectados.Count > 0)
            {
                var habs = await ctx.SsHabTrabajador
                    .Where(h => h.ItemId == item.Id && afectados.Contains(h.WorkerId))
                    .ToListAsync();

                foreach (var workerId in aprobados)
                {
                    var hab = habs.FirstOrDefault(h => h.WorkerId == workerId);
                    if (hab is null)
                    {
                        hab = new SsHabTrabajador
                        {
                            WorkerId = workerId,
                            ItemId = item.Id,
                            CreatedAt = DateTime.UtcNow
                        };
                        ctx.SsHabTrabajador.Add(hab);
                        habs.Add(hab);
                    }
                    hab.Estado = "Aprobado";
                    hab.Vigencia = HabilitacionDateHelper.AsUtc(dto.Vigencia);
                    hab.ObsAbril = dto.ObsAbril;
                    hab.AprobadoPor = userId;
                    hab.FechaAprobacion = DateTime.UtcNow;
                    hab.UpdatedAt = DateTime.UtcNow;
                }

                foreach (var workerId in rechazados)
                {
                    var hab = habs.FirstOrDefault(h => h.WorkerId == workerId);
                    if (hab is null)
                    {
                        hab = new SsHabTrabajador
                        {
                            WorkerId = workerId,
                            ItemId = item.Id,
                            CreatedAt = DateTime.UtcNow
                        };
                        ctx.SsHabTrabajador.Add(hab);
                        habs.Add(hab);
                    }
                    hab.Estado = "Rechazado";
                    hab.ObsAbril = dto.ObsAbril;
                    hab.UpdatedAt = DateTime.UtcNow;
                }
            }

            string nuevoEstado;
            if (rechazados.Count == 0 && aprobados.Count > 0) nuevoEstado = "Aprobado";
            else if (aprobados.Count == 0 && rechazados.Count > 0) nuevoEstado = "Rechazado";
            else nuevoEstado = "Parcial";

            entity.Estado = nuevoEstado;
            entity.Vigencia = HabilitacionDateHelper.AsUtc(dto.Vigencia);
            entity.ObsAbril = dto.ObsAbril;
            entity.UpdatedAt = DateTime.UtcNow;

            await ctx.SaveChangesAsync();

            var dtos = await BuildDtosAsync(ctx, new List<SsSctrVidaley> { entity });
            return dtos.First();
        }

        public async Task<List<SctrVidaLeyDto>> GetPorTrabajadorAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();

            var sctrIds = await ctx.SsSctrVidaLeyWorker
                .Where(w => w.WorkerId == workerId)
                .Select(w => w.SctrVidaLeyId)
                .Distinct()
                .ToListAsync();

            var entities = await ctx.SsSctrVidaley
                .Where(s => sctrIds.Contains(s.Id))
                .OrderByDescending(s => s.Anio)
                .ThenByDescending(s => s.Mes)
                .ToListAsync();

            return await BuildDtosAsync(ctx, entities);
        }

        public async Task<List<SctrVidaLeyDto>> GetProximosVencerAsync(int dias)
        {
            using var ctx = _factory.CreateDbContext();

            var hoy = DateTime.UtcNow.Date;
            var limite = hoy.AddDays(dias);

            var entities = await ctx.SsSctrVidaley
                .Where(s => s.Estado == "Aprobado"
                            && s.Vigencia.HasValue
                            && s.Vigencia.Value >= hoy
                            && s.Vigencia.Value <= limite)
                .OrderBy(s => s.Vigencia)
                .ToListAsync();

            return await BuildDtosAsync(ctx, entities);
        }

        public async Task<List<SctrTrabajadorEstadoDto>> GetTrabajadoresPorEmpresaAsync(
            int? empresaId, int? proyectoId, string? tipo, string? tipoPoliza,
            string? estadoSctr, string? estadoVidaLey)
        {
            using var ctx = _factory.CreateDbContext();

            List<int> workerIds;

            if (!empresaId.HasValue)
            {
                // Sin filtro de empresa: todos los workers activos
                workerIds = await ctx.Worker
                    .Select(w => w.Id)
                    .ToListAsync();
            }
            else
            {
                int contributorId = empresaId.Value;

                // WorkerVinculacion como fuente de verdad para empresa/proyecto activo
                workerIds = await ctx.WorkerVinculacion
                    .Where(v => v.EmpresaId == contributorId
                        && (!proyectoId.HasValue || v.ProyectoId == proyectoId.Value)
                        && v.FechaFin == null)
                    .Select(v => v.WorkerId)
                    .Distinct()
                    .ToListAsync();

                // Suplemento: WorkerProyecto (multi-proyecto Casa) solo cuando hay filtro de proyecto
                if (proyectoId.HasValue)
                {
                    var idsProyecto = await ctx.WorkerProyecto
                        .Where(wp => wp.EmpresaId == contributorId
                            && wp.ProyectoId == proyectoId.Value
                            && wp.FechaFin == null)
                        .Select(wp => wp.WorkerId)
                        .Distinct()
                        .ToListAsync();

                    workerIds = workerIds.Union(idsProyecto).ToList();
                }

                if (workerIds.Count == 0)
                {
                    _logger.LogWarning("[GetTrabajadoresPorEmpresa] Sin workers para empresaId={EmpresaId} proyectoId={ProyectoId}. Retornando lista vacía.", empresaId, proyectoId);
                    return [];
                }
            }

            var workers = await ctx.Worker
                .Where(w => workerIds.Contains(w.Id))
                .Select(w => new
                {
                    w.Id,
                    ApellidoNombre = w.Person != null ? w.Person.FullName : null,
                    Dni = w.Person != null ? w.Person.DocumentIdentityCode : null,
                    w.ObraOficina
                })
                .ToListAsync();

            // Items SCTR y VidaLey del catálogo
            var sctrItems = await ctx.SsItemTrabajador
                .Where(i => i.EsSctrVidaley && i.Activo)
                .ToListAsync();

            var itemSctr = sctrItems.FirstOrDefault(i => i.Nombre.Contains("SCTR", StringComparison.OrdinalIgnoreCase));
            var itemVidaLey = sctrItems.FirstOrDefault(i => i.Nombre.Contains("Vida", StringComparison.OrdinalIgnoreCase));

            var itemIdsRelevantes = sctrItems.Select(i => i.Id).ToList();

            var habs = await ctx.SsHabTrabajador
                .Where(h => workerIds.Contains(h.WorkerId) && itemIdsRelevantes.Contains(h.ItemId))
                .ToListAsync();

            _logger.LogInformation("itemSctr: {item}, itemVidaLey: {item2}, habs count: {count}", itemSctr?.Nombre, itemVidaLey?.Nombre, habs.Count);

            // sctrId de la póliza activa (estado Enviado o Parcial) más reciente por worker
            var sctrIdPorWorker = await (
                from svw in ctx.SsSctrVidaLeyWorker
                join s in ctx.SsSctrVidaley on svw.SctrVidaLeyId equals s.Id
                where workerIds.Contains(svw.WorkerId)
                    && (s.Estado == "Enviado" || s.Estado == "Parcial")
                    && (tipo == null || s.Tipo == tipo)
                group svw by svw.WorkerId into g
                select new { WorkerId = g.Key, SctrId = g.Max(x => x.SctrVidaLeyId) }
            ).ToDictionaryAsync(x => x.WorkerId, x => x.SctrId);

            var result = new List<SctrTrabajadorEstadoDto>();
            foreach (var w in workers)
            {
                var estadoSctrVal = "Falta";
                var estadoVidaLeyVal = "Falta";
                int? sctrHabId = null;
                DateTime? fechaVencimiento = null;

                if (itemSctr is not null)
                {
                    var hab = habs.FirstOrDefault(h => h.WorkerId == w.Id && h.ItemId == itemSctr.Id);
                    if (hab is not null)
                    {
                        estadoSctrVal = hab.Estado ?? "Falta";
                        sctrHabId = hab.Id;
                        fechaVencimiento ??= hab.Vigencia;
                    }
                }

                if (itemVidaLey is not null)
                {
                    var hab = habs.FirstOrDefault(h => h.WorkerId == w.Id && h.ItemId == itemVidaLey.Id);
                    if (hab is not null)
                    {
                        estadoVidaLeyVal = hab.Estado ?? "Falta";
                        fechaVencimiento ??= hab.Vigencia;
                    }
                }

                var vinculacion = await ctx.WorkerVinculacion
                    .Where(wv => wv.WorkerId == w.Id && wv.FechaFin == null)
                    .OrderByDescending(wv => wv.Id)
                    .FirstOrDefaultAsync();

                string? empNombre = null;
                string? proyNombre = null;

                if (vinculacion?.EmpresaId is not null)
                    empNombre = await ctx.Contributor
                        .Where(c => c.ContributorId == vinculacion.EmpresaId)
                        .Select(c => c.ContributorName)
                        .FirstOrDefaultAsync();

                if (vinculacion?.ProyectoId is not null)
                    proyNombre = await ctx.Project
                        .Where(p => p.ProjectId == vinculacion.ProyectoId.Value)
                        .Select(p => p.ProjectDescription)
                        .FirstOrDefaultAsync();

                result.Add(new SctrTrabajadorEstadoDto
                {
                    WorkerId = w.Id,
                    ApellidoNombre = w.ApellidoNombre ?? string.Empty,
                    Dni = w.Dni ?? string.Empty,
                    ObraOficina = w.ObraOficina,
                    SctrId = sctrIdPorWorker.TryGetValue(w.Id, out var sid) ? sid : null,
                    SctrHabId = sctrHabId,
                    EstadoSctr = estadoSctrVal,
                    EstadoVidaLey = estadoVidaLeyVal,
                    EmpresaNombre = empNombre,
                    ProyectoNombre = proyNombre,
                    FechaVencimiento = fechaVencimiento
                });
            }

            // Filtros por estado (acepta múltiples valores separados por coma)
            if (!string.IsNullOrWhiteSpace(estadoSctr))
            {
                var valoresSctr = estadoSctr.Replace("%2C", ",").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                result = result.Where(r => valoresSctr.Contains(r.EstadoSctr, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(estadoVidaLey))
            {
                var valoresVidaLey = estadoVidaLey.Replace("%2C", ",").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                result = result.Where(r => valoresVidaLey.Contains(r.EstadoVidaLey, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            return result;
        }

        private async Task<List<SctrVidaLeyDto>> BuildDtosAsync(
            AppDbContext ctx, List<SsSctrVidaley> entities)
        {
            if (entities.Count == 0) return new List<SctrVidaLeyDto>();

            var ids = entities.Select(e => e.Id).ToList();
            var empresaIds = entities.Select(e => e.EmpresaId).Distinct().ToList();
            var proyectoIds = entities.Select(e => e.ProyectoId).Distinct().ToList();

            var empresaMap = await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId))
                .ToDictionaryAsync(c => c.ContributorId, c => c.ContributorName);

            var proyectoMap = await ctx.Project
                .Where(p => proyectoIds.Contains(p.ProjectId))
                .ToDictionaryAsync(p => p.ProjectId, p => p.ProjectDescription);

            var workersData = await (from svw in ctx.SsSctrVidaLeyWorker
                                     where ids.Contains(svw.SctrVidaLeyId)
                                     join w in ctx.Worker.Include(x => x.Person) on svw.WorkerId equals w.Id
                                     select new
                                     {
                                         svw.SctrVidaLeyId,
                                         svw.WorkerId,
                                         svw.FechaInicioCobertura,
                                         ApellidoNombre = w.Person != null ? w.Person.FullName : null,
                                         Dni = w.Person != null ? w.Person.DocumentIdentityCode : null
                                     }).ToListAsync();

            foreach (var w in workersData.Where(x => x.ApellidoNombre == null).Take(3))
                _logger.LogWarning("Worker sin nombre: workerId={Id} sctrId={SctrId}", w.WorkerId, w.SctrVidaLeyId);

            var sctrItem = await ctx.SsItemTrabajador
                .Where(i => i.EsSctrVidaley)
                .ToListAsync();

            var workerIds = workersData.Select(w => w.WorkerId).Distinct().ToList();
            var sctrItemIds = sctrItem.Select(i => i.Id).ToList();

            var habs = await ctx.SsHabTrabajador
                .Where(h => workerIds.Contains(h.WorkerId) && sctrItemIds.Contains(h.ItemId))
                .ToListAsync();

            var tasks = entities.Select(async e =>
            {
                var workersDeEste = workersData.Where(x => x.SctrVidaLeyId == e.Id).ToList();
                var itemTipo = sctrItem.FirstOrDefault(i =>
                    e.Tipo == "VIDA_LEY" ? i.Nombre.Contains("Vida") : i.Nombre.Contains("SCTR"));

                var workersDto = workersDeEste.Select(w =>
                {
                    var aprobado = false;
                    var estadoWorker = "Falta";
                    int? sctrHabId = null;
                    DateTime? fechaVencimiento = null;
                    if (itemTipo is not null)
                    {
                        var hab = habs.FirstOrDefault(h => h.WorkerId == w.WorkerId && h.ItemId == itemTipo.Id);
                        if (hab is not null)
                        {
                            aprobado = hab.Estado == "Aprobado";
                            estadoWorker = hab.Estado ?? "Falta";
                            sctrHabId = hab.Id;
                            fechaVencimiento = hab.Vigencia;
                        }
                    }
                    return new SctrWorkerDto
                    {
                        WorkerId = w.WorkerId,
                        ApellidoNombre = w.ApellidoNombre ?? string.Empty,
                        Dni = w.Dni ?? string.Empty,
                        Aprobado = aprobado,
                        Estado = estadoWorker,
                        SctrHabId = sctrHabId,
                        FechaInicioCobertura = w.FechaInicioCobertura,
                        FechaVencimiento = fechaVencimiento
                    };
                }).ToList();

                string? archivoUrl = null;
                string? archivoUrl2 = null;

                if (!string.IsNullOrEmpty(e.ArchivoUrl))
                {
                    try { archivoUrl = await _sharePoint.GetDownloadUrlAsync(e.ArchivoUrl); }
                    catch (Exception ex) { _logger.LogError(ex, "Error resolviendo URL: {Path}", e.ArchivoUrl); archivoUrl = null; }
                }

                if (!string.IsNullOrEmpty(e.ArchivoUrl2))
                {
                    try { archivoUrl2 = await _sharePoint.GetDownloadUrlAsync(e.ArchivoUrl2); }
                    catch (Exception ex) { _logger.LogError(ex, "Error resolviendo URL: {Path}", e.ArchivoUrl2); archivoUrl2 = null; }
                }

                return new SctrVidaLeyDto
                {
                    Id = e.Id,
                    EmpresaId = e.EmpresaId,
                    EmpresaNombre = empresaMap.TryGetValue(e.EmpresaId, out var en) ? en : string.Empty,
                    ProyectoId = e.ProyectoId,
                    ProyectoNombre = e.ProyectoId.HasValue && proyectoMap.TryGetValue(e.ProyectoId.Value, out var pn) ? pn ?? string.Empty : string.Empty,
                    Tipo = e.Tipo,
                    TipoPoliza = e.TipoPoliza,
                    FechaInicio = e.FechaInicio,
                    Mes = e.Mes,
                    Anio = e.Anio,
                    ArchivoUrl = archivoUrl,
                    ArchivoUrl2 = archivoUrl2,
                    Estado = e.Estado,
                    Vigencia = e.Vigencia,
                    ObsAbril = e.ObsAbril,
                    Workers = workersDto
                };
            });

            return (await Task.WhenAll(tasks)).ToList();
        }
    }
}
