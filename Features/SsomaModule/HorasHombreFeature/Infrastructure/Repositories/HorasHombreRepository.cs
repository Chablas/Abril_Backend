using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.SsomaModule.HorasHombreFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.HorasHombreFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.HorasHombreFeature.Infrastructure.Repositories
{
    /// <summary>
    /// Horas Hombre se calcula a partir del Tareo diario (Control de Acceso): cada registro de
    /// SsTareo (proyecto + fecha) trae el conteo de personas de Casa y de cada empresa contratista
    /// ese día. Horas Hombre = personas del día × 8h, igual que en IndicadoresProactivosRepository.
    /// No hay carga manual de "horas hombre" — se reutiliza el tareo que ya suben las contratas.
    /// </summary>
    public class HorasHombreRepository : IHorasHombreRepository
    {
        private const int HorasPorPersonaDia = 8;
        private readonly IDbContextFactory<AppDbContext> _factory;

        public HorasHombreRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<PagedResult<HorasHombreDiaDto>> GetTablaAsync(HorasHombreFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.SsTareo.AsQueryable();

            if (filter.ProyectoId.HasValue)
                query = query.Where(t => t.ProyectoId == filter.ProyectoId.Value);
            if (filter.FechaDesde.HasValue)
                query = query.Where(t => t.Fecha >= filter.FechaDesde.Value);
            if (filter.FechaHasta.HasValue)
                query = query.Where(t => t.Fecha <= filter.FechaHasta.Value);
            if (filter.EmpresaId.HasValue)
                query = query.Where(t => ctx.SsTareoDetalleContratista
                    .Any(d => d.TareoId == t.Id && d.EmpresaId == filter.EmpresaId.Value && d.CantidadPersonas > 0));

            var total = await query.CountAsync();
            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize <= 0 ? 20 : Math.Min(filter.PageSize, 200);

            var rows = await query
                .OrderByDescending(t => t.Fecha).ThenByDescending(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.ProyectoId,
                    t.Fecha,
                    ProyectoNombre = t.Proyecto != null ? t.Proyecto.ProjectDescription : "",
                    PersonasCasa = ctx.SsTareoDetalleCasa.Where(d => d.TareoId == t.Id).Sum(d => (int?)d.CantidadPersonas) ?? 0,
                    PersonasContratista = ctx.SsTareoDetalleContratista.Where(d => d.TareoId == t.Id).Sum(d => (int?)d.CantidadPersonas) ?? 0,
                })
                .ToListAsync();

            var items = rows.Select(r =>
            {
                var totalPersonas = r.PersonasCasa + r.PersonasContratista;
                return new HorasHombreDiaDto
                {
                    Fecha = r.Fecha,
                    ProyectoId = r.ProyectoId,
                    ProyectoNombre = r.ProyectoNombre ?? "",
                    PersonasCasa = r.PersonasCasa,
                    PersonasContratista = r.PersonasContratista,
                    TotalPersonas = totalPersonas,
                    HorasHombre = totalPersonas * HorasPorPersonaDia,
                };
            }).ToList();

            return new PagedResult<HorasHombreDiaDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Data = items,
            };
        }

        public async Task<HorasHombreDashboardDto> GetDashboardAsync(int? proyectoId, int? mes, int? anio)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.SsTareo.AsQueryable();
            if (proyectoId.HasValue)
                query = query.Where(t => t.ProyectoId == proyectoId.Value);
            if (mes.HasValue)
                query = query.Where(t => t.Fecha.Month == mes.Value);
            if (anio.HasValue)
                query = query.Where(t => t.Fecha.Year == anio.Value);

            var tareos = await query
                .Select(t => new
                {
                    t.Id,
                    t.ProyectoId,
                    t.Fecha,
                    ProyectoNombre = t.Proyecto != null ? t.Proyecto.ProjectDescription : "",
                })
                .ToListAsync();

            var result = new HorasHombreDashboardDto { ProyectoId = proyectoId, Mes = mes, Anio = anio };
            if (tareos.Count == 0) return result;

            var tareoIds = tareos.Select(t => t.Id).ToList();

            var detalleCasa = await ctx.SsTareoDetalleCasa
                .Where(d => tareoIds.Contains(d.TareoId))
                .Select(d => new { d.TareoId, d.CantidadPersonas })
                .ToListAsync();

            var detalleContratista = await ctx.SsTareoDetalleContratista
                .Where(d => tareoIds.Contains(d.TareoId))
                .Select(d => new { d.TareoId, d.EmpresaId, d.CantidadPersonas })
                .ToListAsync();

            var empresaIds = detalleContratista.Select(d => d.EmpresaId).Distinct().ToList();
            var empresaNombres = await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId))
                .ToDictionaryAsync(c => c.ContributorId, c => c.ContributorName);

            var casaPorTareo = detalleCasa.GroupBy(d => d.TareoId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.CantidadPersonas));
            var contratistaPorTareo = detalleContratista.GroupBy(d => d.TareoId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.CantidadPersonas));

            long totalHHCasa = 0, totalHHContratista = 0;
            long totalPersonasAcumulado = 0;

            var serieDiaria = tareos
                .GroupBy(t => t.Fecha)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    long hhCasa = 0, hhContr = 0;
                    foreach (var t in g)
                    {
                        var casa = casaPorTareo.GetValueOrDefault(t.Id);
                        var contr = contratistaPorTareo.GetValueOrDefault(t.Id);
                        hhCasa += (long)casa * HorasPorPersonaDia;
                        hhContr += (long)contr * HorasPorPersonaDia;
                        totalPersonasAcumulado += casa + contr;
                    }
                    totalHHCasa += hhCasa;
                    totalHHContratista += hhContr;
                    return new HorasHombreSerieDiaDto
                    {
                        Fecha = g.Key,
                        HorasHombreCasa = hhCasa,
                        HorasHombreContratista = hhContr,
                        HorasHombreTotal = hhCasa + hhContr,
                    };
                })
                .ToList();

            var porEmpresa = detalleContratista
                .GroupBy(d => d.EmpresaId)
                .Select(g => new HorasHombrePorEmpresaDto
                {
                    EmpresaId = g.Key,
                    EmpresaNombre = empresaNombres.GetValueOrDefault(g.Key, "—"),
                    TotalPersonasDia = g.Sum(x => (long)x.CantidadPersonas),
                    HorasHombre = g.Sum(x => (long)x.CantidadPersonas) * HorasPorPersonaDia,
                })
                .OrderByDescending(e => e.HorasHombre)
                .ToList();

            List<HorasHombreProyectoResumenDto> porProyecto = new();
            if (!proyectoId.HasValue)
            {
                porProyecto = tareos
                    .GroupBy(t => new { t.ProyectoId, t.ProyectoNombre })
                    .Select(g =>
                    {
                        long hh = 0;
                        foreach (var t in g)
                        {
                            var casa = casaPorTareo.GetValueOrDefault(t.Id);
                            var contr = contratistaPorTareo.GetValueOrDefault(t.Id);
                            hh += (long)(casa + contr) * HorasPorPersonaDia;
                        }
                        return new HorasHombreProyectoResumenDto
                        {
                            ProyectoId = g.Key.ProyectoId,
                            ProyectoNombre = g.Key.ProyectoNombre ?? "",
                            HorasHombre = hh,
                        };
                    })
                    .OrderByDescending(p => p.HorasHombre)
                    .ToList();
            }

            result.TotalHorasHombreCasa = totalHHCasa;
            result.TotalHorasHombreContratista = totalHHContratista;
            result.TotalHorasHombre = totalHHCasa + totalHHContratista;
            result.DiasRegistrados = tareos.Select(t => t.Fecha).Distinct().Count();
            result.PromedioPersonasPorDia = result.DiasRegistrados > 0
                ? Math.Round((double)totalPersonasAcumulado / result.DiasRegistrados, 1)
                : 0;
            result.UltimaFechaRegistrada = tareos.Max(t => (DateOnly?)t.Fecha);
            result.SerieDiaria = serieDiaria;
            result.PorEmpresa = porEmpresa;
            result.PorProyecto = porProyecto;

            return result;
        }
    }
}
