using Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Application.Dtos;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Infrastructure.Repositories;

public class DesempenoSupervisorRepository(IDbContextFactory<AppDbContext> factory)
{
    private const int MetaRacs            = 4;
    private const int MetaOpt             = 2;
    private const int MetaInspecciones    = 2;
    private const int MetaCharlas         = 2;
    private const int MetaLeccion         = 1;
    private const int MetaEvalContratista = 1;
    private const int MetaEvalResidente   = 1;

    public async Task<List<DesempenoSupervisorDto>> GetDesempenoAsync(int mes, int anio, int? proyectoId)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        var inicio = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var fin    = inicio.AddMonths(1);

        // ── 1. Supervisores SSOMA ──────────────────────────────────────────
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

        // ── 2. Filtrar solo STAFF: User → Person → Worker (excluye Oficina Central) ──
        var personasPorUser = await ctx.Person
            .Where(p => p.UserId != null && supervisorUserIds.Contains(p.UserId.Value))
            .Select(p => new { UserId = p.UserId!.Value, p.PersonId })
            .ToListAsync();

        var personIds = personasPorUser.Select(p => p.PersonId).ToList();

        var staffWorkers = await ctx.Worker
            .Where(w => w.PersonId != null
                     && personIds.Contains(w.PersonId.Value)
                     && w.ObraOficina != "Oficina Central")
            .Select(w => new { w.Id, w.PersonId })
            .ToListAsync();

        var userToWorker = personasPorUser
            .Join(staffWorkers,
                  p => p.PersonId,
                  w => w.PersonId,
                  (p, w) => new { p.UserId, WorkerId = w.Id })
            .ToDictionary(x => x.UserId, x => x.WorkerId);

        supervisorUserIds = supervisorUserIds.Where(uid => userToWorker.ContainsKey(uid)).ToList();
        if (!supervisorUserIds.Any()) return [];

        // ── 3. Nombres de supervisores ─────────────────────────────────────
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
        var nombreToSupId     = supervisores.ToDictionary(s => s.Nombre.ToUpper().Trim(), s => s.UserId);

        // ── 4. Queries con fechas para los 4 indicadores core (FechaLogro100) ─
        var racsRaw = await ctx.SsomaRacs
            .Where(r => r.FechaReporte >= inicio && r.FechaReporte < fin
                     && (proyectoId == null || r.ProyectoId == proyectoId)
                     && (
                         (r.CreatedBy != null && supervisorUserIds.Contains(r.CreatedBy.Value)) ||
                         (r.CreatedBy == null && r.ReportanteNombre != null && supervisorNombres.Contains(r.ReportanteNombre.ToUpper().Trim()))
                     ))
            .Select(r => new {
                SupId = r.CreatedBy != null ? r.CreatedBy.Value : -1,
                r.ReportanteNombre,
                r.ProyectoId,
                Fecha = r.FechaReporte
            })
            .ToListAsync();

        var racsDetalle = racsRaw
            .Select(r => new {
                SupId = r.SupId > 0
                    ? r.SupId
                    : (r.ReportanteNombre != null && nombreToSupId.ContainsKey(r.ReportanteNombre.ToUpper().Trim())
                        ? nombreToSupId[r.ReportanteNombre.ToUpper().Trim()]
                        : 0),
                r.ProyectoId,
                r.Fecha
            })
            .Where(r => r.SupId > 0)
            .ToList();

        var optRaw = await ctx.SsomaOpt
            .Where(o => o.Fecha >= inicio && o.Fecha < fin
                     && (proyectoId == null || o.ProyectoId == proyectoId)
                     && (
                         (o.CreatedBy != null && supervisorUserIds.Contains(o.CreatedBy.Value)) ||
                         (o.CreatedBy == null && o.ObservadorNombre != null && supervisorNombres.Contains(o.ObservadorNombre.ToUpper().Trim()))
                     ))
            .Select(o => new {
                SupId = o.CreatedBy != null ? o.CreatedBy.Value : -1,
                o.ObservadorNombre,
                o.ProyectoId,
                o.Fecha
            })
            .ToListAsync();

        var optDetalle = optRaw
            .Select(o => new {
                SupId = o.SupId > 0
                    ? o.SupId
                    : (o.ObservadorNombre != null && nombreToSupId.ContainsKey(o.ObservadorNombre.ToUpper().Trim())
                        ? nombreToSupId[o.ObservadorNombre.ToUpper().Trim()]
                        : 0),
                o.ProyectoId,
                o.Fecha
            })
            .Where(o => o.SupId > 0)
            .ToList();

        var inspDetalle = await ctx.SsomaInspeccion
            .Where(i => i.CreatedBy != null && supervisorUserIds.Contains(i.CreatedBy.Value)
                     && i.Fecha >= inicio && i.Fecha < fin
                     && (proyectoId == null || i.ProyectoId == proyectoId))
            .Select(i => new { SupId = i.CreatedBy!.Value, i.ProyectoId, i.Fecha })
            .ToListAsync();

        var charlasDetalle = await ctx.SsCharlas
            .Where(c => c.CreadoPorId != null && supervisorUserIds.Contains(c.CreadoPorId.Value)
                     && c.Fecha >= inicio && c.Fecha < fin
                     && (proyectoId == null || c.ProyectoId == proyectoId))
            .Select(c => new { SupId = c.CreadoPorId!.Value, ProyId = c.ProyectoId ?? 0, c.Fecha })
            .ToListAsync();

        // ── 5. Nuevos indicadores ──────────────────────────────────────────
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

        // ── 6. Combinaciones (supervisor, proyecto) con actividad en el mes ─
        List<(int SupId, int ProyId)> combinaciones;
        if (proyectoId.HasValue)
        {
            combinaciones = supervisorUserIds.Select(sid => (SupId: sid, ProyId: proyectoId.Value)).ToList();
        }
        else
        {
            combinaciones = racsDetalle.Select(r => (r.SupId, r.ProyectoId))
                .Concat(optDetalle    .Select(o => (o.SupId, o.ProyectoId)))
                .Concat(inspDetalle   .Select(i => (i.SupId, i.ProyectoId)))
                .Concat(charlasDetalle.Where(c => c.ProyId > 0).Select(c => (c.SupId, c.ProyId)))
                .Concat(leccionesPorSupervisor.Where(l => l.ProyId > 0).Select(l => (l.SupId, l.ProyId)))
                .Concat(evalContratistaPorSupervisor.Select(e => (SupId: e.SupId, ProyId: e.ProyectoId)))
                .Concat(evalResidentePorSupervisor.Where(e => e.ProyId > 0).Select(e => (e.SupId, e.ProyId)))
                .Where(c => c.Item2 > 0)
                .Distinct().ToList();
        }

        var proyectoIds = combinaciones.Select(c => c.ProyId).Distinct().ToList();

        var proyectos = await ctx.Project
            .Where(p => proyectoIds.Contains(p.ProjectId))
            .Select(p => new { p.ProjectId, Nombre = p.ProjectDescription })
            .ToListAsync();

        // ── 7. Construir resultados ────────────────────────────────────────
        var resultado = new List<DesempenoSupervisorDto>();

        foreach (var (supId, proyId) in combinaciones)
        {
            if (proyId == 0) continue;

            var sup  = supervisores.FirstOrDefault(s => s.UserId == supId);
            var proy = proyectos.FirstOrDefault(p => p.ProjectId == proyId);
            if (sup is null || proy is null) continue;

            var racs    = racsDetalle   .Count(r => r.SupId == supId && r.ProyectoId == proyId);
            var opt     = optDetalle    .Count(o => o.SupId == supId && o.ProyectoId == proyId);
            var insp    = inspDetalle   .Count(i => i.SupId == supId && i.ProyectoId == proyId);
            var charlas = charlasDetalle.Count(c => c.SupId == supId && c.ProyId     == proyId);
            var leccion       = leccionesPorSupervisor.FirstOrDefault(l => l.SupId == supId && l.ProyId == proyId)?.N ?? 0;
            var evalContrat   = evalContratistaPorSupervisor.FirstOrDefault(e => e.SupId == supId && e.ProyectoId == proyId)?.N ?? 0;
            var evalResidente = evalResidentePorSupervisor.FirstOrDefault(e => e.SupId == supId && e.ProyId == proyId)?.N ?? 0;

            var pctRacs        = Math.Min(100m, Pct(racs,    MetaRacs));
            var pctOpt         = Math.Min(100m, Pct(opt,     MetaOpt));
            var pctInsp        = Math.Min(100m, Pct(insp,    MetaInspecciones));
            var pctCharlas     = Math.Min(100m, Pct(charlas, MetaCharlas));
            var pctLeccion     = leccion > 0 ? 100m : 0m;
            var pctEvalContrat = evalContrat > 0 ? 100m : 0m;
            var pctEvalRes     = evalResidente > 0 ? 100m : 0m;
            // RAC+OPT+Insp+Charlas = 25% c/u; Leccion/EvalContratista/EvalResidente solo informativo
            var pctGeneral = Math.Round(
                pctRacs * 0.25m + pctOpt * 0.25m + pctInsp * 0.25m + pctCharlas * 0.25m, 1);

            DateTime? fechaRacs = racsDetalle
                .Where(r => r.SupId == supId && r.ProyectoId == proyId)
                .OrderBy(r => r.Fecha).Skip(MetaRacs - 1)
                .Select(r => (DateTime?)r.Fecha).FirstOrDefault();

            DateTime? fechaOpt = optDetalle
                .Where(o => o.SupId == supId && o.ProyectoId == proyId)
                .OrderBy(o => o.Fecha).Skip(MetaOpt - 1)
                .Select(o => (DateTime?)o.Fecha).FirstOrDefault();

            DateTime? fechaInsp = inspDetalle
                .Where(i => i.SupId == supId && i.ProyectoId == proyId)
                .OrderBy(i => i.Fecha).Skip(MetaInspecciones - 1)
                .Select(i => (DateTime?)i.Fecha).FirstOrDefault();

            DateTime? fechaCharlas = charlasDetalle
                .Where(c => c.SupId == supId && c.ProyId == proyId)
                .OrderBy(c => c.Fecha).Skip(MetaCharlas - 1)
                .Select(c => (DateTime?)c.Fecha).FirstOrDefault();

            DateTime? fechaLogro100 = null;
            if (fechaRacs.HasValue && fechaOpt.HasValue && fechaInsp.HasValue && fechaCharlas.HasValue)
                fechaLogro100 = new[] { fechaRacs.Value, fechaOpt.Value, fechaInsp.Value, fechaCharlas.Value }.Max();

            resultado.Add(new DesempenoSupervisorDto(
                SupervisorId:          supId,
                SupervisorNombre:      sup.Nombre,
                ProyectoId:            proyId,
                ProyectoNombre:        proy.Nombre ?? $"Proyecto {proyId}",
                Mes:                   mes,
                Anio:                  anio,
                MetaRacs:              MetaRacs,
                MetaOpt:               MetaOpt,
                MetaInspecciones:      MetaInspecciones,
                MetaCharlas:           MetaCharlas,
                MetaLeccion:           MetaLeccion,
                MetaEvalContratista:   MetaEvalContratista,
                MetaEvalResidente:     MetaEvalResidente,
                ActualRacs:            racs,
                ActualOpt:             opt,
                ActualInspecciones:    insp,
                ActualCharlas:         charlas,
                ActualLeccion:         leccion,
                ActualEvalContratista: evalContrat,
                ActualEvalResidente:   evalResidente,
                PctRacs:               pctRacs,
                PctOpt:                pctOpt,
                PctInspecciones:       pctInsp,
                PctCharlas:            pctCharlas,
                PctLeccion:            pctLeccion,
                PctEvalContratista:    pctEvalContrat,
                PctEvalResidente:      pctEvalRes,
                PctGeneral:            pctGeneral,
                FechaLogro100:         fechaLogro100,
                EsPrimeroEnProyecto:   false
            ));
        }

        // ── 8. Marcar al primero en llegar al 100% por proyecto ────────────
        var primerosPorProyecto = new HashSet<(int SupId, int ProyId)>();
        foreach (var grupo in resultado.Where(r => r.FechaLogro100.HasValue).GroupBy(r => r.ProyectoId))
        {
            var primero = grupo.OrderBy(r => r.FechaLogro100!.Value).First();
            primerosPorProyecto.Add((primero.SupervisorId, primero.ProyectoId));
        }

        resultado = resultado.Select(r => r with
        {
            EsPrimeroEnProyecto = r.FechaLogro100.HasValue
                               && primerosPorProyecto.Contains((r.SupervisorId, r.ProyectoId))
        }).ToList();

        return resultado.OrderByDescending(r => r.PctGeneral).ToList();
    }

    private static decimal Pct(int actual, int meta) =>
        meta == 0 ? 100m : Math.Round((decimal)actual / meta * 100m, 1);
}
