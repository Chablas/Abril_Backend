using Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Application.Dtos;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Infrastructure.Repositories;

public class DesempenoSupervisorRepository(IDbContextFactory<AppDbContext> factory)
{
    private const int MetaRacs = 4;
    private const int MetaOpt = 2;
    private const int MetaInspecciones = 2;
    private const int MetaCharlas = 2;

    public async Task<List<DesempenoSupervisorDto>> GetDesempenoAsync(int mes, int anio)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        var inicio = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var fin = inicio.AddMonths(1);

        var ssomaRoleIds = await ctx.Role
            .Where(r => r.RoleDescription == "ADMINISTRADOR SSOMA" ||
                        r.RoleDescription == "SALUD OCUPACIONAL")
            .Select(r => r.RoleId)
            .ToListAsync();

        var supervisorUserIds = await ctx.UserRole
            .Where(ur => ssomaRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync();

        if (!supervisorUserIds.Any()) return [];

        var supervisores = await ctx.User
            .Where(u => supervisorUserIds.Contains(u.UserId))
            .Select(u => new
            {
                u.UserId,
                Nombre = u.Person != null
                    ? (u.Person.FullName ?? (u.Person.FirstNames + " " + u.Person.FirstLastName).Trim())
                    : "Sin nombre",
            })
            .ToListAsync();

        // RACs creados por supervisor en el período
        var racsPorSupervisor = await ctx.SsomaRacs
            .Where(r => r.CreatedBy != null && supervisorUserIds.Contains(r.CreatedBy.Value)
                        && r.FechaReporte >= inicio && r.FechaReporte < fin)
            .GroupBy(r => new { r.CreatedBy, r.ProyectoId })
            .Select(g => new { SupId = g.Key.CreatedBy!.Value, g.Key.ProyectoId, N = g.Count() })
            .ToListAsync();

        // OPTs creados por supervisor
        var optPorSupervisor = await ctx.SsomaOpt
            .Where(o => o.CreatedBy != null && supervisorUserIds.Contains(o.CreatedBy.Value)
                        && o.Fecha >= inicio && o.Fecha < fin)
            .GroupBy(o => new { o.CreatedBy, o.ProyectoId })
            .Select(g => new { SupId = g.Key.CreatedBy!.Value, g.Key.ProyectoId, N = g.Count() })
            .ToListAsync();

        // Inspecciones creadas por supervisor
        var inspPorSupervisor = await ctx.SsomaInspeccion
            .Where(i => i.CreatedBy != null && supervisorUserIds.Contains(i.CreatedBy.Value)
                        && i.Fecha >= inicio && i.Fecha < fin)
            .GroupBy(i => new { i.CreatedBy, i.ProyectoId })
            .Select(g => new { SupId = g.Key.CreatedBy!.Value, g.Key.ProyectoId, N = g.Count() })
            .ToListAsync();

        // Charlas creadas por supervisor (SsCharla, no SsCharlaAsistencia)
        var charlasPorSupervisor = await ctx.SsCharlas
            .Where(c => c.CreadoPorId != null && supervisorUserIds.Contains(c.CreadoPorId.Value)
                        && c.Fecha >= inicio && c.Fecha < fin)
            .GroupBy(c => new { c.CreadoPorId, c.ProyectoId })
            .Select(g => new { SupId = g.Key.CreadoPorId!.Value, ProyId = g.Key.ProyectoId ?? 0, N = g.Count() })
            .ToListAsync();

        // Proyectos involucrados
        var proyectoIds = racsPorSupervisor.Select(r => r.ProyectoId)
            .Union(optPorSupervisor.Select(o => o.ProyectoId))
            .Union(inspPorSupervisor.Select(i => i.ProyectoId))
            .Union(charlasPorSupervisor.Where(c => c.ProyId > 0).Select(c => c.ProyId))
            .Distinct().ToList();

        var proyectos = await ctx.Project
            .Where(p => proyectoIds.Contains(p.ProjectId))
            .Select(p => new { p.ProjectId, Nombre = p.ProjectDescription })
            .ToListAsync();

        // Una fila por (supervisor, proyecto) con cualquier actividad
        var combinaciones = racsPorSupervisor.Select(r => (r.SupId, r.ProyectoId))
            .Concat(optPorSupervisor.Select(o => (o.SupId, o.ProyectoId)))
            .Concat(inspPorSupervisor.Select(i => (i.SupId, i.ProyectoId)))
            .Concat(charlasPorSupervisor.Where(c => c.ProyId > 0).Select(c => (c.SupId, c.ProyId)))
            .Distinct().ToList();

        var resultado = new List<DesempenoSupervisorDto>();

        foreach (var combo in combinaciones)
        {
            int supId = combo.Item1;
            int proyId = combo.Item2;

            var sup  = supervisores.FirstOrDefault(s => s.UserId == supId);
            var proy = proyectos.FirstOrDefault(p => p.ProjectId == proyId);
            if (sup is null || proy is null) continue;

            var racs    = racsPorSupervisor.FirstOrDefault(r => r.SupId == supId && r.ProyectoId == proyId)?.N ?? 0;
            var opt     = optPorSupervisor.FirstOrDefault(o => o.SupId == supId && o.ProyectoId == proyId)?.N ?? 0;
            var insp    = inspPorSupervisor.FirstOrDefault(i => i.SupId == supId && i.ProyectoId == proyId)?.N ?? 0;
            var charlas = charlasPorSupervisor.FirstOrDefault(c => c.SupId == supId && c.ProyId == proyId)?.N ?? 0;

            var pctRacs    = Math.Min(100m, Pct(racs, MetaRacs));
            var pctOpt     = Math.Min(100m, Pct(opt, MetaOpt));
            var pctInsp    = Math.Min(100m, Pct(insp, MetaInspecciones));
            var pctCharlas = Math.Min(100m, Pct(charlas, MetaCharlas));
            var pctGeneral = (pctRacs + pctOpt + pctInsp + pctCharlas) / 4m;

            resultado.Add(new DesempenoSupervisorDto(
                SupervisorId: supId,
                SupervisorNombre: sup.Nombre,
                ProyectoId: proyId,
                ProyectoNombre: proy.Nombre ?? $"Proyecto {proyId}",
                Mes: mes,
                Anio: anio,
                MetaRacs: MetaRacs,
                MetaOpt: MetaOpt,
                MetaInspecciones: MetaInspecciones,
                MetaCharlas: MetaCharlas,
                ActualRacs: racs,
                ActualOpt: opt,
                ActualInspecciones: insp,
                ActualCharlas: charlas,
                PctRacs: pctRacs,
                PctOpt: pctOpt,
                PctInspecciones: pctInsp,
                PctCharlas: pctCharlas,
                PctGeneral: pctGeneral
            ));
        }

        return resultado.OrderByDescending(r => r.PctGeneral).ToList();
    }

    private static decimal Pct(int actual, int meta) =>
        meta == 0 ? 100m : Math.Round((decimal)actual / meta * 100m, 1);
}
