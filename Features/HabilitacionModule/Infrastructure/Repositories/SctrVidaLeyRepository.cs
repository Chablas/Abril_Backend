using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.SctrVidaley;
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

        public SctrVidaLeyRepository(IDbContextFactory<AppDbContext> factory, ILogger<SctrVidaLeyRepository> logger)
        {
            _factory = factory;
            _logger = logger;
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
            var vigenciaHab = esAbril && dto.Vigencia.HasValue
                ? DateTime.SpecifyKind(dto.Vigencia.Value, DateTimeKind.Utc)
                : (DateTime?)null;

            var item = await ctx.SsItemTrabajador
                .Where(i => i.EsSctrVidaley)
                .FirstOrDefaultAsync(i => i.Nombre.Contains(dto.Tipo));

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

            var item = await ctx.SsItemTrabajador
                .Where(i => i.EsSctrVidaley)
                .FirstOrDefaultAsync(i => i.Nombre.Contains(dto.Tipo));

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

            var item = await ctx.SsItemTrabajador
                .Where(i => i.EsSctrVidaley)
                .FirstOrDefaultAsync(i => i.Nombre.Contains(entity.Tipo));

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

            _logger.LogInformation("[GetTrabajadoresPorEmpresa] INPUT empresaId={EmpresaId} proyectoId={ProyectoId} tipo={Tipo} tipoPoliza={TipoPoliza} estadoSctr={EstadoSctr} estadoVidaLey={EstadoVidaLey}",
                empresaId, proyectoId, tipo, tipoPoliza, estadoSctr, estadoVidaLey);

            List<int> workerIds;

            if (!empresaId.HasValue)
            {
                // Sin filtro de empresa: todos los workers activos
                workerIds = await ctx.Worker
                    .Select(w => w.Id)
                    .ToListAsync();

                _logger.LogInformation("[GetTrabajadoresPorEmpresa] Sin empresaId → {Count} workers totales", workerIds.Count);
            }
            else
            {
                // Si empresaId corresponde a una empresa Abril en contributor, se usa directo.
                // Si no, es un ss_empresa_contratista.id → resolver via id_legacy.
                var contributor = await ctx.Contributor
                    .Where(c => c.ContributorId == empresaId.Value)
                    .Select(c => new { c.ContributorId, c.ContributorName })
                    .FirstOrDefaultAsync();

                int contributorId;
                if (contributor != null)
                {
                    // empresaId ya es un ContributorId válido (Abril o contratista)
                    contributorId = empresaId.Value;
                    _logger.LogInformation("[GetTrabajadoresPorEmpresa] Empresa encontrada en contributor. contributorId={ContributorId} nombre='{Nombre}'",
                        contributorId, contributor.ContributorName);
                }
                else
                {
                    // empresaId es un ss_empresa_contratista.id → resolver via id_legacy
                    var idLegacy = await ctx.SsEmpresaContratista
                        .Where(e => e.Id == empresaId.Value)
                        .Select(e => e.IdLegacy)
                        .FirstOrDefaultAsync();

                    contributorId = idLegacy ?? empresaId.Value;
                    _logger.LogInformation("[GetTrabajadoresPorEmpresa] SsId: ss_empresa_contratista.id={EmpresaId} → id_legacy={IdLegacy} → contributorId={ContributorId}",
                        empresaId.Value, idLegacy, contributorId);
                }

                // Workers activos en la empresa (y en el proyecto si se especifica)
                workerIds = await ctx.WorkerProyecto
                    .Where(wp => wp.EmpresaId == contributorId
                        && (!proyectoId.HasValue || wp.ProyectoId == proyectoId.Value)
                        && wp.FechaFin == null)
                    .Select(wp => wp.WorkerId)
                    .Distinct()
                    .ToListAsync();

                _logger.LogInformation("[GetTrabajadoresPorEmpresa] WorkerProyecto → {Count} workerIds: [{Ids}]",
                    workerIds.Count, string.Join(",", workerIds));

                // Fallback: worker_vinculaciones (empresas Casa o cuando worker_proyectos no tiene registros).
                if (workerIds.Count == 0)
                {
                    var vinculacionEmpresaIds = contributorId == empresaId.Value
                        ? new List<int> { contributorId }
                        : new List<int> { contributorId, empresaId.Value };

                    workerIds = await ctx.WorkerVinculacion
                        .Where(v => vinculacionEmpresaIds.Contains(v.EmpresaId!.Value)
                            && (!proyectoId.HasValue || v.ProyectoId == proyectoId.Value)
                            && v.FechaFin == null)
                        .Select(v => v.WorkerId)
                        .Distinct()
                        .ToListAsync();

                    _logger.LogInformation("[GetTrabajadoresPorEmpresa] Fallback WorkerVinculacion (empresaIds=[{Ids}]) → {Count} workerIds: [{WorkerIds}]",
                        string.Join(",", vinculacionEmpresaIds), workerIds.Count, string.Join(",", workerIds));
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

            var itemSctr = sctrItems.FirstOrDefault(i => i.Nombre.Contains("SCTR"));
            var itemVidaLey = sctrItems.FirstOrDefault(i => i.Nombre.Contains("VIDA_LEY") || i.Nombre.Contains("VIDA LEY"));

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
                group svw by svw.WorkerId into g
                select new { WorkerId = g.Key, SctrId = g.Max(x => x.SctrVidaLeyId) }
            ).ToDictionaryAsync(x => x.WorkerId, x => x.SctrId);

            var result = workers.Select(w =>
            {
                var estadoSctrVal = "Falta";
                var estadoVidaLeyVal = "Falta";
                int? sctrHabId = null;

                if (itemSctr is not null)
                {
                    var hab = habs.FirstOrDefault(h => h.WorkerId == w.Id && h.ItemId == itemSctr.Id);
                    if (hab is not null)
                    {
                        estadoSctrVal = hab.Estado ?? "Falta";
                        sctrHabId = hab.Id;
                    }
                }

                if (itemVidaLey is not null)
                {
                    var hab = habs.FirstOrDefault(h => h.WorkerId == w.Id && h.ItemId == itemVidaLey.Id);
                    if (hab is not null) estadoVidaLeyVal = hab.Estado ?? "Falta";
                }

                return new SctrTrabajadorEstadoDto
                {
                    WorkerId = w.Id,
                    ApellidoNombre = w.ApellidoNombre ?? string.Empty,
                    Dni = w.Dni ?? string.Empty,
                    ObraOficina = w.ObraOficina,
                    SctrId = sctrIdPorWorker.TryGetValue(w.Id, out var sid) ? sid : null,
                    SctrHabId = sctrHabId,
                    EstadoSctr = estadoSctrVal,
                    EstadoVidaLey = estadoVidaLeyVal
                };
            }).ToList();

            _logger.LogInformation("[GetTrabajadoresPorEmpresa] Antes de filtros de estado: {Total} trabajadores. itemSctr={ItemSctrId} itemVidaLey={ItemVidaLeyId}",
                result.Count, itemSctr?.Id.ToString() ?? "null", itemVidaLey?.Id.ToString() ?? "null");

            // Filtros por estado (acepta múltiples valores separados por coma)
            if (!string.IsNullOrWhiteSpace(estadoSctr))
            {
                var valoresSctr = estadoSctr.Replace("%2C", ",").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                _logger.LogInformation("[GetTrabajadoresPorEmpresa] Aplicando filtro estadoSctr='{EstadoSctr}'", estadoSctr);
                result = result.Where(r => valoresSctr.Contains(r.EstadoSctr, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(estadoVidaLey))
            {
                var valoresVidaLey = estadoVidaLey.Replace("%2C", ",").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                _logger.LogInformation("[GetTrabajadoresPorEmpresa] Aplicando filtro estadoVidaLey='{EstadoVidaLey}'", estadoVidaLey);
                result = result.Where(r => valoresVidaLey.Contains(r.EstadoVidaLey, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            _logger.LogInformation("[GetTrabajadoresPorEmpresa] RESULTADO FINAL: {Count} trabajadores", result.Count);

            return result;
        }

        private static async Task<List<SctrVidaLeyDto>> BuildDtosAsync(
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
                                     join w in ctx.Worker on svw.WorkerId equals w.Id
                                     select new
                                     {
                                         svw.SctrVidaLeyId,
                                         svw.WorkerId,
                                         svw.FechaInicioCobertura,
                                         ApellidoNombre = w.Person != null ? w.Person.FullName : null,
                                         Dni = w.Person != null ? w.Person.DocumentIdentityCode : null
                                     }).ToListAsync();

            var sctrItem = await ctx.SsItemTrabajador
                .Where(i => i.EsSctrVidaley)
                .ToListAsync();

            var workerIds = workersData.Select(w => w.WorkerId).Distinct().ToList();
            var sctrItemIds = sctrItem.Select(i => i.Id).ToList();

            var habs = await ctx.SsHabTrabajador
                .Where(h => workerIds.Contains(h.WorkerId) && sctrItemIds.Contains(h.ItemId))
                .ToListAsync();

            return entities.Select(e =>
            {
                var workersDeEste = workersData.Where(x => x.SctrVidaLeyId == e.Id).ToList();
                var itemTipo = sctrItem.FirstOrDefault(i => i.Nombre.Contains(e.Tipo));

                var workersDto = workersDeEste.Select(w =>
                {
                    var aprobado = false;
                    if (itemTipo is not null)
                    {
                        var hab = habs.FirstOrDefault(h => h.WorkerId == w.WorkerId && h.ItemId == itemTipo.Id);
                        aprobado = hab is not null && hab.Estado == "Aprobado";
                    }
                    return new SctrWorkerDto
                    {
                        WorkerId = w.WorkerId,
                        ApellidoNombre = w.ApellidoNombre ?? string.Empty,
                        Dni = w.Dni ?? string.Empty,
                        Aprobado = aprobado,
                        FechaInicioCobertura = w.FechaInicioCobertura
                    };
                }).ToList();

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
                    ArchivoUrl = e.ArchivoUrl,
                    ArchivoUrl2 = e.ArchivoUrl2,
                    Estado = e.Estado,
                    Vigencia = e.Vigencia,
                    ObsAbril = e.ObsAbril,
                    Workers = workersDto
                };
            }).ToList();
        }
    }
}
