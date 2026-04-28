using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.SctrVidaley;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class SctrVidaLeyRepository : ISctrVidaLeyRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public SctrVidaLeyRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
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

            var entity = new SsSctrVidaley
            {
                EmpresaId = empresaId,
                ProyectoId = dto.ProyectoId,
                Tipo = dto.Tipo,
                Mes = dto.Mes,
                Anio = dto.Anio,
                ArchivoUrl = dto.ArchivoUrl,
                ArchivoUrl2 = dto.ArchivoUrl2,
                Estado = "Enviado",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            ctx.SsSctrVidaley.Add(entity);
            await ctx.SaveChangesAsync();

            foreach (var workerId in dto.WorkerIds.Distinct())
            {
                ctx.SsSctrVidaLeyWorker.Add(new SsSctrVidaLeyWorker
                {
                    SctrVidaLeyId = entity.Id,
                    WorkerId = workerId
                });
            }
            await ctx.SaveChangesAsync();

            var item = await ctx.SsItemTrabajador
                .Where(i => i.EsSctrVidaley)
                .FirstOrDefaultAsync(i => i.Nombre.Contains(dto.Tipo));

            if (item is not null)
            {
                foreach (var workerId in dto.WorkerIds.Distinct())
                {
                    var hab = await ctx.SsHabTrabajador
                        .FirstOrDefaultAsync(h => h.WorkerId == workerId && h.ItemId == item.Id);

                    if (hab is null)
                    {
                        hab = new SsHabTrabajador
                        {
                            WorkerId = workerId,
                            ItemId = item.Id,
                            Estado = "Enviado",
                            ArchivoUrl = dto.ArchivoUrl,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        ctx.SsHabTrabajador.Add(hab);
                    }
                    else
                    {
                        hab.Estado = "Enviado";
                        hab.ArchivoUrl = dto.ArchivoUrl;
                        hab.UpdatedAt = DateTime.UtcNow;
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
                    hab.Vigencia = dto.Vigencia;
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
            entity.Vigencia = dto.Vigencia;
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

        private static async Task<List<SctrVidaLeyDto>> BuildDtosAsync(
            AppDbContext ctx, List<SsSctrVidaley> entities)
        {
            if (entities.Count == 0) return new List<SctrVidaLeyDto>();

            var ids = entities.Select(e => e.Id).ToList();
            var empresaIds = entities.Select(e => e.EmpresaId).Distinct().ToList();
            var proyectoIds = entities.Select(e => e.ProyectoId).Distinct().ToList();

            var empresaMap = await ctx.SsEmpresaContratista
                .Where(e => empresaIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.RazonSocial);

            var proyectoMap = await ctx.Projects
                .Where(p => proyectoIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Nombre);

            var workersData = await (from svw in ctx.SsSctrVidaLeyWorker
                                     where ids.Contains(svw.SctrVidaLeyId)
                                     join w in ctx.Worker on svw.WorkerId equals w.Id
                                     select new
                                     {
                                         svw.SctrVidaLeyId,
                                         svw.WorkerId,
                                         w.ApellidoNombre,
                                         w.Dni
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
                        Aprobado = aprobado
                    };
                }).ToList();

                return new SctrVidaLeyDto
                {
                    Id = e.Id,
                    EmpresaId = e.EmpresaId,
                    EmpresaNombre = empresaMap.TryGetValue(e.EmpresaId, out var en) ? en : string.Empty,
                    ProyectoId = e.ProyectoId,
                    ProyectoNombre = proyectoMap.TryGetValue(e.ProyectoId, out var pn) ? pn ?? string.Empty : string.Empty,
                    Tipo = e.Tipo,
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
