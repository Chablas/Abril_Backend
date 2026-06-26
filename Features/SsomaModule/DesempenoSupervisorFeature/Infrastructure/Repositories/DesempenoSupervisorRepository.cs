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
    private const int MetaLeccion = 1;
    private const int MetaEvalContratista = 1;
    private const int MetaEvalResidente = 1;

    public async Task<List<DesempenoSupervisorDto>> GetDesempenoAsync(int mes, int anio, int? proyectoId)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        var inicio = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var fin = inicio.AddMonths(1);

        var ssomaRoleIds = await ctx.Role
            .Where(r => r.RoleDescription == "ADMINISTRADOR SSOMA" ||
                        r.RoleDescription == "SALUD OCUPACIONAL")
            .Select(r => r.RoleId)
            .ToListAsync();

        var todosSupIds = await ctx.UserRole
            .Where(ur => ssomaRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync();

        if (!todosSupIds.Any()) return [];

        List<int> supervisorUserIds;
        if (proyectoId.HasValue)
        {
            // Proyecto actual del worker = última vinculación con fecha_fin IS NULL
            var staffDelProyecto = await ctx.Person
                .Where(p => p.UserId != null)
                .Join(ctx.Worker.Where(w => w.PersonId != null),
                    p => p.PersonId,
                    w => w.PersonId,
                    (p, w) => new { p.UserId, WorkerId = w.Id })
                .Join(ctx.WorkerVinculacion.Where(v => v.FechaFin == null && v.ProyectoId == proyectoId.Value),
                    x => x.WorkerId,
                    v => v.WorkerId,
                    (x, v) => x.UserId!.Value)
                .Distinct()
                .ToListAsync();
            supervisorUserIds = staffDelProyecto;
        }
        else
        {
            supervisorUserIds = todosSupIds;
        }
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

        var supervisorNombres = supervisores.Select(s => s.Nombre.ToUpper().Trim()).ToList();

        var racsPorSupervisor = await ctx.SsomaRacs
            .Where(r => r.FechaReporte >= inicio && r.FechaReporte < fin
                        && (proyectoId == null || r.ProyectoId == proyectoId)
                        && (
                            (r.CreatedBy != null && supervisorUserIds.Contains(r.CreatedBy.Value)) ||
                            (r.CreatedBy == null && r.ReportanteNombre != null && supervisorNombres.Contains(r.ReportanteNombre.ToUpper().Trim()))
                        ))
            .Select(r => new {
                SupId = r.CreatedBy != null ? r.CreatedBy.Value : -1,
                ReportanteNombre = r.ReportanteNombre,
                r.ProyectoId,
            })
            .ToListAsync();

        // Para los que tienen created_by null, resolvemos el SupId por nombre
        var nombreToSupId = supervisores.ToDictionary(s => s.Nombre.ToUpper().Trim(), s => s.UserId);
        var racsPorSupervisorGrouped = racsPorSupervisor
            .Select(r => new {
                SupId = r.SupId > 0 ? r.SupId : (r.ReportanteNombre != null && nombreToSupId.ContainsKey(r.ReportanteNombre.ToUpper().Trim()) ? nombreToSupId[r.ReportanteNombre.ToUpper().Trim()] : 0),
                r.ProyectoId
            })
            .Where(r => r.SupId > 0)
            .GroupBy(r => new { r.SupId, r.ProyectoId })
            .Select(g => new { SupId = g.Key.SupId, ProyectoId = g.Key.ProyectoId, N = g.Count() })
            .ToList();

        var optRaw = await ctx.SsomaOpt
            .Where(o => o.Fecha >= inicio && o.Fecha < fin
                        && (proyectoId == null || o.ProyectoId == proyectoId)
                        && (
                            (o.CreatedBy != null && supervisorUserIds.Contains(o.CreatedBy.Value)) ||
                            (o.CreatedBy == null && o.ObservadorNombre != null && supervisorNombres.Contains(o.ObservadorNombre.ToUpper().Trim()))
                        ))
            .Select(o => new { SupId = o.CreatedBy != null ? o.CreatedBy.Value : -1, ObservadorNombre = o.ObservadorNombre, o.ProyectoId })
            .ToListAsync();

        var optPorSupervisor = optRaw
            .Select(o => new {
                SupId = o.SupId > 0 ? o.SupId : (o.ObservadorNombre != null && nombreToSupId.ContainsKey(o.ObservadorNombre.ToUpper().Trim()) ? nombreToSupId[o.ObservadorNombre.ToUpper().Trim()] : 0),
                o.ProyectoId
            })
            .Where(o => o.SupId > 0)
            .GroupBy(o => new { o.SupId, o.ProyectoId })
            .Select(g => new { SupId = g.Key.SupId, ProyectoId = g.Key.ProyectoId, N = g.Count() })
            .ToList();

        var inspPorSupervisor = await ctx.SsomaInspeccion
            .Where(i => i.CreatedBy != null && supervisorUserIds.Contains(i.CreatedBy.Value)
                        && i.Fecha >= inicio && i.Fecha < fin
                        && (proyectoId == null || i.ProyectoId == proyectoId))
            .GroupBy(i => new { i.CreatedBy, i.ProyectoId })
            .Select(g => new { SupId = g.Key.CreatedBy!.Value, g.Key.ProyectoId, N = g.Count() })
            .ToListAsync();

        var charlasPorSupervisor = await ctx.SsCharlas
            .Where(c => c.CreadoPorId != null && supervisorUserIds.Contains(c.CreadoPorId.Value)
                        && c.Fecha >= inicio && c.Fecha < fin
                        && (proyectoId == null || c.ProyectoId == proyectoId))
            .GroupBy(c => new { c.CreadoPorId, c.ProyectoId })
            .Select(g => new { SupId = g.Key.CreadoPorId!.Value, ProyId = g.Key.ProyectoId ?? 0, N = g.Count() })
            .ToListAsync();

        var leccionesPorSupervisor = await ctx.Lesson
            .Where(l => supervisorUserIds.Contains(l.CreatedUserId)
                        && l.CreatedDateTime >= inicio && l.CreatedDateTime < fin
                        && l.Active
                        && (proyectoId == null || l.ProjectId == proyectoId))
            .GroupBy(l => new { l.CreatedUserId, l.ProjectId })
            .Select(g => new { SupId = g.Key.CreatedUserId, ProyId = g.Key.ProjectId ?? 0, N = g.Count() })
            .ToListAsync();

        var evalContratistaPorSupervisor = await ctx.SsEvalSupervisor
            .Where(e => e.EvaluadorUserId != null && supervisorUserIds.Contains(e.EvaluadorUserId.Value)
                        && e.Mes == mes && e.Anio == anio
                        && (proyectoId == null || e.ProyectoId == proyectoId))
            .GroupBy(e => new { e.EvaluadorUserId, e.ProyectoId })
            .Select(g => new { SupId = g.Key.EvaluadorUserId!.Value, g.Key.ProyectoId, N = g.Count() })
            .ToListAsync();

        var evalResidentePorSupervisor = await ctx.EvEvaluacionesResidente
            .Where(e => e.EvaluadorUserId != null && supervisorUserIds.Contains(e.EvaluadorUserId.Value)
                        && e.CreatedAt >= inicio && e.CreatedAt < fin
                        && (proyectoId == null || e.ProjectId == proyectoId))
            .GroupBy(e => new { e.EvaluadorUserId, e.ProjectId })
            .Select(g => new { SupId = g.Key.EvaluadorUserId!.Value, ProyId = g.Key.ProjectId ?? 0, N = g.Count() })
            .ToListAsync();

        List<(int SupId, int ProyId)> combinaciones;
        if (proyectoId.HasValue)
        {
            combinaciones = supervisorUserIds.Select(sid => (SupId: sid, ProyId: proyectoId.Value)).ToList();
        }
        else
        {
            combinaciones = new List<int>()
                .Concat(racsPorSupervisorGrouped.Select(r => r.ProyectoId))
                .Concat(optPorSupervisor.Select(o => o.ProyectoId))
                .Concat(inspPorSupervisor.Select(i => i.ProyectoId))
                .Concat(charlasPorSupervisor.Where(c => c.ProyId > 0).Select(c => c.ProyId))
                .Concat(leccionesPorSupervisor.Where(l => l.ProyId > 0).Select(l => l.ProyId))
                .Concat(evalContratistaPorSupervisor.Select(e => e.ProyectoId))
                .Concat(evalResidentePorSupervisor.Where(e => e.ProyId > 0).Select(e => e.ProyId))
                .Distinct()
                .SelectMany(pid => supervisorUserIds
                    .Where(sid =>
                        racsPorSupervisorGrouped.Any(r => r.SupId == sid && r.ProyectoId == pid) ||
                        optPorSupervisor.Any(o => o.SupId == sid && o.ProyectoId == pid) ||
                        inspPorSupervisor.Any(i => i.SupId == sid && i.ProyectoId == pid) ||
                        charlasPorSupervisor.Any(c => c.SupId == sid && c.ProyId == pid) ||
                        leccionesPorSupervisor.Any(l => l.SupId == sid && l.ProyId == pid) ||
                        evalContratistaPorSupervisor.Any(e => e.SupId == sid && e.ProyectoId == pid) ||
                        evalResidentePorSupervisor.Any(e => e.SupId == sid && e.ProyId == pid))
                    .Select(sid => (SupId: sid, ProyId: pid)))
                .Distinct().ToList();
        }

        var proyectoIds = combinaciones.Select(c => c.ProyId).Distinct().ToList();

        var proyectos = await ctx.Project
            .Where(p => proyectoIds.Contains(p.ProjectId))
            .Select(p => new { p.ProjectId, Nombre = p.ProjectDescription })
            .ToListAsync();

        var resultado = new List<DesempenoSupervisorDto>();

        foreach (var combo in combinaciones)
        {
            int supId = combo.SupId;
            int proyId = combo.ProyId;
            if (proyId == 0) continue;

            var sup  = supervisores.FirstOrDefault(s => s.UserId == supId);
            var proy = proyectos.FirstOrDefault(p => p.ProjectId == proyId);
            if (sup is null || proy is null) continue;

            var racs           = racsPorSupervisorGrouped.FirstOrDefault(r => r.SupId == supId && r.ProyectoId == proyId)?.N ?? 0;
            var opt            = optPorSupervisor.FirstOrDefault(o => o.SupId == supId && o.ProyectoId == proyId)?.N ?? 0;
            var insp           = inspPorSupervisor.FirstOrDefault(i => i.SupId == supId && i.ProyectoId == proyId)?.N ?? 0;
            var charlas        = charlasPorSupervisor.FirstOrDefault(c => c.SupId == supId && c.ProyId == proyId)?.N ?? 0;
            var leccion        = leccionesPorSupervisor.FirstOrDefault(l => l.SupId == supId && l.ProyId == proyId)?.N ?? 0;
            var evalContrat    = evalContratistaPorSupervisor.FirstOrDefault(e => e.SupId == supId && e.ProyectoId == proyId)?.N ?? 0;
            var evalResidente  = evalResidentePorSupervisor.FirstOrDefault(e => e.SupId == supId && e.ProyId == proyId)?.N ?? 0;

            var pctRacs        = Math.Min(100m, Pct(racs, MetaRacs));
            var pctOpt         = Math.Min(100m, Pct(opt, MetaOpt));
            var pctInsp        = Math.Min(100m, Pct(insp, MetaInspecciones));
            var pctCharlas     = Math.Min(100m, Pct(charlas, MetaCharlas));
            var pctLeccion     = leccion > 0 ? 100m : 0m;
            var pctEvalContrat = evalContrat > 0 ? 100m : 0m;
            var pctEvalRes     = evalResidente > 0 ? 100m : 0m;
            // Pesos: RAC+OPT+Insp+Charlas = 17.5% c/u (70% total), Leccion+EvalContrat+EvalRes = 10% c/u
            var pctGeneral = Math.Round(
                pctRacs * 0.175m + pctOpt * 0.175m + pctInsp * 0.175m + pctCharlas * 0.175m +
                pctLeccion * 0.10m + pctEvalContrat * 0.10m + pctEvalRes * 0.10m, 1);

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
                MetaLeccion: MetaLeccion,
                MetaEvalContratista: MetaEvalContratista,
                MetaEvalResidente: MetaEvalResidente,
                ActualRacs: racs,
                ActualOpt: opt,
                ActualInspecciones: insp,
                ActualCharlas: charlas,
                ActualLeccion: leccion,
                ActualEvalContratista: evalContrat,
                ActualEvalResidente: evalResidente,
                PctRacs: pctRacs,
                PctOpt: pctOpt,
                PctInspecciones: pctInsp,
                PctCharlas: pctCharlas,
                PctLeccion: pctLeccion,
                PctEvalContratista: pctEvalContrat,
                PctEvalResidente: pctEvalRes,
                PctGeneral: pctGeneral
            ));
        }

        return resultado.OrderByDescending(r => r.PctGeneral).ToList();
    }

    private static decimal Pct(int actual, int meta) =>
        meta == 0 ? 100m : Math.Round((decimal)actual / meta * 100m, 1);
}
