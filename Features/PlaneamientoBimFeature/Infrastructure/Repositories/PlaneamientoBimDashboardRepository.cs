using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Services;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Repositories
{
    public class PlaneamientoBimDashboardRepository : IPlaneamientoBimDashboardRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public PlaneamientoBimDashboardRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<AvanceProyectoDto?> GetAvance(int projectId, DateOnly? desde, DateOnly? hasta)
        {
            using var ctx = _factory.CreateDbContext();

            var existeProyecto = await ctx.Project.AnyAsync(p => p.ProjectId == projectId);
            if (!existeProyecto)
                return null;

            // Los diccionarios de nombres se arman en memoria (no en el Select) porque
            // ToDictionary() sobre una colección de navegación no es traducible a SQL por EF Core.
            var zonasEntidades = await ctx.BimProyectoTorre
                .Where(t => t.ProjectId == projectId)
                .Include(t => t.Niveles)
                .OrderBy(t => t.Orden)
                .ToListAsync();

            var zonas = zonasEntidades.Select(z => new
            {
                z.Id,
                z.Nombre,
                Niveles = z.Niveles.ToDictionary(n => n.Id, n => n.Nombre),
            }).ToList();

            var celdas = await ctx.BimRegistroDiario
                .Where(r => r.ProjectId == projectId
                    && (!desde.HasValue || r.Fecha >= desde.Value)
                    && (!hasta.HasValue || r.Fecha <= hasta.Value))
                .GroupBy(r => new { r.TorreId, r.NivelId, r.SectorId })
                .Select(g => new
                {
                    g.Key.TorreId,
                    g.Key.NivelId,
                    g.Key.SectorId,
                    Total = g.Count(),
                    SumaPorcentaje = g.Sum(x => x.PorcentajeAvance),
                })
                .ToListAsync();

            var zonasDto = new List<ZonaAvanceDto>();
            foreach (var zona in zonas)
            {
                // Sectores ya no tienen nombre propio (ver BimZonaSector.cs): bajo el modelo
                // nuevo son un número derivado de Torre.CantidadSectoresX + Nivel.TipoEstructura,
                // no una entidad con Nombre. SectorNombre queda como el número crudo — cero
                // consultas a bim_zona_sector acá.
                var celdasZona = celdas.Where(c => c.TorreId == zona.Id)
                    .Select(c => new CeldaAvanceDto
                    {
                        NivelId = c.NivelId,
                        NivelNombre = zona.Niveles.GetValueOrDefault(c.NivelId, string.Empty),
                        SectorId = c.SectorId,
                        SectorNombre = c.SectorId.ToString(),
                        TotalRegistros = c.Total,
                        CumplidosRegistros = c.SumaPorcentaje,
                        PorcentajeAvance = PorcentajeDe(c.SumaPorcentaje, c.Total),
                    })
                    .ToList();

                var totalZona = celdasZona.Sum(c => c.TotalRegistros);
                var cumplidosZona = celdasZona.Sum(c => c.CumplidosRegistros);

                zonasDto.Add(new ZonaAvanceDto
                {
                    ZonaId = zona.Id,
                    ZonaNombre = zona.Nombre,
                    TotalRegistros = totalZona,
                    CumplidosRegistros = cumplidosZona,
                    PorcentajeAvance = PorcentajeDe(cumplidosZona, totalZona),
                    Celdas = celdasZona,
                });
            }

            var totalProyecto = zonasDto.Sum(z => z.TotalRegistros);
            var cumplidosProyecto = zonasDto.Sum(z => z.CumplidosRegistros);

            return new AvanceProyectoDto
            {
                Desde = desde,
                Hasta = hasta,
                TotalRegistros = totalProyecto,
                CumplidosRegistros = cumplidosProyecto,
                PorcentajeAvance = PorcentajeDe(cumplidosProyecto, totalProyecto),
                Zonas = zonasDto,
            };
        }

        public async Task<PpcHistoricoDto?> GetPpcHistorico(int projectId, DateOnly? desde, DateOnly? hasta)
        {
            using var ctx = _factory.CreateDbContext();

            var existeProyecto = await ctx.Project.AnyAsync(p => p.ProjectId == projectId);
            if (!existeProyecto)
                return null;

            var dias = await ctx.BimRegistroDiario
                .Where(r => r.ProjectId == projectId
                    && (!desde.HasValue || r.Fecha >= desde.Value)
                    && (!hasta.HasValue || r.Fecha <= hasta.Value))
                .GroupBy(r => r.Fecha)
                .Select(g => new
                {
                    Fecha = g.Key,
                    Total = g.Count(),
                    SumaPorcentaje = g.Sum(x => x.PorcentajeAvance),
                })
                .OrderBy(d => d.Fecha)
                .ToListAsync();

            return new PpcHistoricoDto
            {
                MetaPpc = PlaneamientoBimConfiguracionService.MetaPpcEstandar,
                Dias = dias.Select(d => new PpcDiaDto
                {
                    Fecha = d.Fecha,
                    TotalProgramadas = d.Total,
                    Cumplidas = d.SumaPorcentaje,
                    PorcentajePpc = PorcentajeDe(d.SumaPorcentaje, d.Total),
                }).ToList(),
            };
        }

        public async Task<List<MetaSemanalDto>?> GetMetasSemanales(int projectId)
        {
            using var ctx = _factory.CreateDbContext();

            var existeProyecto = await ctx.Project.AnyAsync(p => p.ProjectId == projectId);
            if (!existeProyecto)
                return null;

            return await ctx.BimMetaSemanal
                .Where(m => m.ProjectId == projectId)
                .OrderBy(m => m.MacroActividad.Orden).ThenBy(m => m.FechaInicioSemana)
                .Select(m => new MetaSemanalDto
                {
                    Id = m.Id,
                    MacroActividadId = m.MacroActividadId,
                    MacroActividadNombre = m.MacroActividad.Nombre,
                    FechaInicioSemana = m.FechaInicioSemana,
                    FechaFinSemana = m.FechaFinSemana,
                    MetaAvance = m.MetaAvance,
                })
                .ToListAsync();
        }

        public async Task GuardarMetasSemanales(int projectId, MetaSemanalUpdateDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var existeProyecto = await ctx.Project.AnyAsync(p => p.ProjectId == projectId);
            if (!existeProyecto)
                throw new AbrilException("El proyecto no existe.", 404);

            var macroActividadesValidas = await ctx.BimMacroActividad.Select(m => m.Id).ToListAsync();
            var idsInvalidos = dto.Items.Select(i => i.MacroActividadId).Except(macroActividadesValidas).ToList();
            if (idsInvalidos.Count > 0)
                throw new AbrilException($"Macro-actividad inválida: {string.Join(", ", idsInvalidos)}.", 400);

            var existentes = await ctx.BimMetaSemanal
                .Where(m => m.ProjectId == projectId)
                .ToListAsync();

            var ahora = DateTimeOffset.UtcNow;

            foreach (var item in dto.Items)
            {
                var meta = existentes.FirstOrDefault(m =>
                    m.MacroActividadId == item.MacroActividadId && m.FechaInicioSemana == item.FechaInicioSemana);

                if (meta == null)
                {
                    meta = new BimMetaSemanal
                    {
                        ProjectId = projectId,
                        MacroActividadId = item.MacroActividadId,
                        FechaInicioSemana = item.FechaInicioSemana,
                        CreatedUserId = userId,
                        CreatedDateTime = ahora,
                    };
                    ctx.BimMetaSemanal.Add(meta);
                }
                else
                {
                    meta.UpdatedUserId = userId;
                    meta.UpdatedDateTime = ahora;
                }

                meta.FechaFinSemana = item.FechaFinSemana;
                meta.MetaAvance = item.MetaAvance;
            }

            await ctx.SaveChangesAsync();
        }

        public async Task<List<PlanMaestroSemanaDto>?> GetPlanMaestro(int projectId)
        {
            using var ctx = _factory.CreateDbContext();

            var existeProyecto = await ctx.Project.AnyAsync(p => p.ProjectId == projectId);
            if (!existeProyecto)
                return null;

            var semanas = await ctx.BimMetaSemanal
                .Where(m => m.ProjectId == projectId)
                .OrderBy(m => m.MacroActividad.Orden).ThenBy(m => m.FechaInicioSemana)
                .Select(m => new
                {
                    m.MacroActividadId,
                    MacroActividadNombre = m.MacroActividad.Nombre,
                    m.FechaInicioSemana,
                    m.FechaFinSemana,
                    m.MetaAvance,
                    TotalReal = ctx.BimRegistroDiario.Count(r =>
                        r.ProjectId == projectId && r.Fecha <= m.FechaFinSemana && r.Actividad.MacroActividadId == m.MacroActividadId),
                    SumaPorcentajeReal = ctx.BimRegistroDiario
                        .Where(r => r.ProjectId == projectId && r.Fecha <= m.FechaFinSemana && r.Actividad.MacroActividadId == m.MacroActividadId)
                        .Sum(r => (decimal?)r.PorcentajeAvance) ?? 0m,
                })
                .ToListAsync();

            return semanas.Select(s => new PlanMaestroSemanaDto
            {
                MacroActividadId = s.MacroActividadId,
                MacroActividadNombre = s.MacroActividadNombre,
                FechaInicioSemana = s.FechaInicioSemana,
                FechaFinSemana = s.FechaFinSemana,
                MetaAvance = s.MetaAvance,
                AvanceReal = PorcentajeDe(s.SumaPorcentajeReal, s.TotalReal),
            }).ToList();
        }

        public async Task<CausasParetoDto?> GetCausasPareto(int projectId, DateOnly? desde, DateOnly? hasta)
        {
            using var ctx = _factory.CreateDbContext();

            var existeProyecto = await ctx.Project.AnyAsync(p => p.ProjectId == projectId);
            if (!existeProyecto)
                return null;

            var causas = await ctx.BimRegistroDiario
                .Where(r => r.ProjectId == projectId && r.PorcentajeAvance < 100 && r.CausaId != null
                    && (!desde.HasValue || r.Fecha >= desde.Value)
                    && (!hasta.HasValue || r.Fecha <= hasta.Value))
                .GroupBy(r => new { r.CausaId, CausaNombre = r.Causa!.Nombre })
                .Select(g => new { g.Key.CausaId, g.Key.CausaNombre, Cantidad = g.Count() })
                .OrderByDescending(c => c.Cantidad)
                .ToListAsync();

            var total = causas.Sum(c => c.Cantidad);

            return new CausasParetoDto
            {
                TotalNoCumplidas = total,
                Causas = causas.Select(c => new CausaParetoDto
                {
                    CausaId = c.CausaId!.Value,
                    CausaNombre = c.CausaNombre,
                    Cantidad = c.Cantidad,
                    Porcentaje = PorcentajeDe(c.Cantidad, total),
                }).ToList(),
            };
        }

        /// <summary>parte es una SUMA de porcentajes (0-100 por registro), no un conteo — por eso
        /// no se multiplica por 100 acá: ese factor ya está incluido en cada término de la suma.</summary>
        private static decimal PorcentajeDe(decimal parte, int total)
            => total == 0 ? 0 : Math.Round(parte / total, 2);
    }
}
