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
                     && w.ObraOficina != "Oficina Central"
                     && w.Estado == "ACTIVO")
            .Select(w => new { w.Id, w.PersonId, w.Ocupacion })
            .ToListAsync();

        var userToWorker = personasPorUser
            .Join(staffWorkers,
                  p => p.PersonId,
                  w => w.PersonId,
                  (p, w) => new { p.UserId, WorkerId = w.Id })
            .ToDictionary(x => x.UserId, x => x.WorkerId);

        var residenteUserIds = personasPorUser
            .Join(staffWorkers.Where(w => string.Equals(w.Ocupacion, "Residencia", StringComparison.OrdinalIgnoreCase)),
                  p => p.PersonId,
                  w => w.PersonId,
                  (p, w) => p.UserId)
            .ToHashSet();

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

        // ── 4. Queries de actividad — SIN filtro de proyecto (lo que importa es el supervisor) ─
        var racsRaw = await ctx.SsomaRacs
            .Where(r => r.FechaReporte >= inicio && r.FechaReporte < fin
                     && (
                         (r.CreatedBy != null && supervisorUserIds.Contains(r.CreatedBy.Value)) ||
                         (r.CreatedBy == null && r.ReportanteNombre != null && supervisorNombres.Contains(r.ReportanteNombre.ToUpper().Trim()))
                     ))
            .Select(r => new {
                SupId = r.CreatedBy != null ? r.CreatedBy.Value : -1,
                r.ReportanteNombre,
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
                r.Fecha
            })
            .Where(r => r.SupId > 0)
            .ToList();

        var optRaw = await ctx.SsomaOpt
            .Where(o => o.Fecha >= inicio && o.Fecha < fin
                     && (
                         (o.CreatedBy != null && supervisorUserIds.Contains(o.CreatedBy.Value)) ||
                         (o.CreatedBy == null && o.ObservadorNombre != null && supervisorNombres.Contains(o.ObservadorNombre.ToUpper().Trim()))
                     ))
            .Select(o => new {
                SupId = o.CreatedBy != null ? o.CreatedBy.Value : -1,
                o.ObservadorNombre,
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
                o.Fecha
            })
            .Where(o => o.SupId > 0)
            .ToList();

        var inspRaw = await ctx.SsomaInspeccion
            .Where(i => i.Fecha >= inicio && i.Fecha < fin
                     && i.Estado != "Borrador"
                     && (
                         (i.CreatedBy != null && supervisorUserIds.Contains(i.CreatedBy.Value)) ||
                         (i.CreatedBy == null && i.InspectorNombre != null && supervisorNombres.Contains(i.InspectorNombre.ToUpper().Trim()))
                     ))
            .Select(i => new { i.CreatedBy, i.InspectorNombre, i.Fecha })
            .ToListAsync();

        var inspDetalle = inspRaw
            .Select(i => new {
                SupId = i.CreatedBy != null
                    ? i.CreatedBy.Value
                    : (i.InspectorNombre != null && nombreToSupId.ContainsKey(i.InspectorNombre.ToUpper().Trim())
                        ? nombreToSupId[i.InspectorNombre.ToUpper().Trim()]
                        : 0),
                i.Fecha
            })
            .Where(i => i.SupId > 0)
            .ToList();

        // Charlas: SupervisorId es WorkerId — invertir userToWorker para mapear de vuelta a UserId
        var workerToUser = userToWorker.ToDictionary(kv => kv.Value, kv => kv.Key);
        var supWorkerIds  = userToWorker.Values.ToList();

        var charlasRaw = await ctx.SsCharlas
            .Where(c => c.SupervisorId != null
                     && supWorkerIds.Contains(c.SupervisorId.Value)
                     && c.Fecha >= inicio && c.Fecha < fin
                     && c.State)
            .Select(c => new { c.SupervisorId, c.Fecha })
            .ToListAsync();

        var charlasDetalle = charlasRaw
            .Where(c => workerToUser.ContainsKey(c.SupervisorId!.Value))
            .Select(c => new { SupId = workerToUser[c.SupervisorId!.Value], c.Fecha })
            .ToList();

        // ── 5. Nuevos indicadores ──────────────────────────────────────────
        var leccionesPorSupervisor = await ctx.Lesson
            .Where(l => supervisorUserIds.Contains(l.CreatedUserId)
                     && l.CreatedDateTime >= inicio && l.CreatedDateTime < fin
                     && l.Active && l.State
                     && l.ApprovalStatus != "RECHAZADA")
            .GroupBy(l => l.CreatedUserId)
            .Select(g => new { SupId = g.Key, N = g.Count() })
            .ToListAsync();

        var evalContratistaPorSupervisor = await ctx.SsEvalSupervisor
            .Where(e => e.EvaluadorUserId != null && supervisorUserIds.Contains(e.EvaluadorUserId.Value)
                     && e.Mes == mes && e.Anio == anio)
            .GroupBy(e => e.EvaluadorUserId)
            .Select(g => new { SupId = g.Key!.Value, N = g.Count() })
            .ToListAsync();

        // Se filtra por el mes/año del período de evaluación (ev_periodo), no por CreatedAt:
        // el período de junio cierra el 4 de julio, así que evaluaciones de junio creadas
        // en esos primeros días de julio quedaban contadas como "julio" y no marcaban el check.
        var evalResidentePorSupervisor = await ctx.EvEvaluacionesResidente
            .Where(e => e.EvaluadorUserId != null && supervisorUserIds.Contains(e.EvaluadorUserId.Value)
                     && e.Periodo!.Mes == mes && e.Periodo!.Anio == anio
                     && !e.NoAplica)
            .GroupBy(e => e.EvaluadorUserId)
            .Select(g => new { SupId = g.Key!.Value, N = g.Count() })
            .ToListAsync();

        // ── 6. Combinaciones: supervisor → su proyecto actual de vinculación ─
        // El proyecto se determina por WorkerVinculacion, NO por dónde registró la actividad
        var vinculacionActual = await ctx.WorkerVinculacion
            .Where(v => supWorkerIds.Contains(v.WorkerId) && v.FechaFin == null && v.ProyectoId != null)
            .Select(v => new { v.WorkerId, ProyectoId = v.ProyectoId!.Value })
            .ToListAsync();

        var proyectoPorUser = userToWorker
            .Where(kv => vinculacionActual.Any(v => v.WorkerId == kv.Value))
            .ToDictionary(
                kv => kv.Key,
                kv => vinculacionActual.First(v => v.WorkerId == kv.Value).ProyectoId);

        List<(int SupId, int ProyId)> combinaciones;
        if (proyectoId.HasValue)
        {
            combinaciones = supervisorUserIds.Select(sid => (SupId: sid, ProyId: proyectoId.Value)).ToList();
        }
        else
        {
            combinaciones = supervisorUserIds
                .Where(sid => proyectoPorUser.ContainsKey(sid))
                .Select(sid => (SupId: sid, ProyId: proyectoPorUser[sid]))
                .Where(c => c.ProyId > 0)
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

            var racs    = racsDetalle   .Count(r => r.SupId == supId);
            var opt     = optDetalle    .Count(o => o.SupId == supId);
            var insp    = inspDetalle   .Count(i => i.SupId == supId);
            var charlas = charlasDetalle.Count(c => c.SupId == supId);
            var leccion       = leccionesPorSupervisor      .FirstOrDefault(l => l.SupId == supId)?.N ?? 0;
            var evalContrat   = evalContratistaPorSupervisor.FirstOrDefault(e => e.SupId == supId)?.N ?? 0;
            var evalResidente = evalResidentePorSupervisor  .FirstOrDefault(e => e.SupId == supId)?.N ?? 0;

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
                .Where(r => r.SupId == supId)
                .OrderBy(r => r.Fecha).Skip(MetaRacs - 1)
                .Select(r => (DateTime?)r.Fecha).FirstOrDefault();

            DateTime? fechaOpt = optDetalle
                .Where(o => o.SupId == supId)
                .OrderBy(o => o.Fecha).Skip(MetaOpt - 1)
                .Select(o => (DateTime?)o.Fecha).FirstOrDefault();

            DateTime? fechaInsp = inspDetalle
                .Where(i => i.SupId == supId)
                .OrderBy(i => i.Fecha).Skip(MetaInspecciones - 1)
                .Select(i => (DateTime?)i.Fecha).FirstOrDefault();

            DateTime? fechaCharlas = charlasDetalle
                .Where(c => c.SupId == supId)
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
                FechaLogro100:            fechaLogro100,
                EsPrimeroEnProyecto:      false,
                PctGeneralMesAnterior:    null,
                EsResidente:              residenteUserIds.Contains(supId)
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

        // ── 9. Tendencia vs mes anterior ──────────────────────────────────────
        var mesAnt    = mes == 1 ? 12 : mes - 1;
        var anioAnt   = mes == 1 ? anio - 1 : anio;
        var inicioAnt = new DateTime(anioAnt, mesAnt, 1, 0, 0, 0, DateTimeKind.Utc);
        var finAnt    = inicioAnt.AddMonths(1);

        var supIds2   = resultado.Select(r => r.SupervisorId).Distinct().ToList();
        var wIds2     = supIds2.Where(uid => userToWorker.ContainsKey(uid)).Select(uid => userToWorker[uid]).ToList();

        var racsAnt = await ctx.SsomaRacs
            .Where(r => r.FechaReporte >= inicioAnt && r.FechaReporte < finAnt
                     && r.CreatedBy != null && supIds2.Contains(r.CreatedBy.Value))
            .GroupBy(r => r.CreatedBy)
            .Select(g => new { SupId = g.Key!.Value, N = g.Count() })
            .ToListAsync();

        var optAnt = await ctx.SsomaOpt
            .Where(o => o.Fecha >= inicioAnt && o.Fecha < finAnt
                     && o.CreatedBy != null && supIds2.Contains(o.CreatedBy.Value))
            .GroupBy(o => o.CreatedBy)
            .Select(g => new { SupId = g.Key!.Value, N = g.Count() })
            .ToListAsync();

        var inspAnt = await ctx.SsomaInspeccion
            .Where(i => i.Fecha >= inicioAnt && i.Fecha < finAnt
                     && i.CreatedBy != null && supIds2.Contains(i.CreatedBy.Value))
            .GroupBy(i => i.CreatedBy)
            .Select(g => new { SupId = g.Key!.Value, N = g.Count() })
            .ToListAsync();

        var charlasAntRaw = await ctx.SsCharlas
            .Where(c => c.Fecha >= inicioAnt && c.Fecha < finAnt
                     && c.SupervisorId != null && wIds2.Contains(c.SupervisorId.Value)
                     && c.State && (c.Estado == "Enviado" || c.Estado == "Aprobado"))
            .Select(c => new { c.SupervisorId })
            .ToListAsync();

        var charlasAnt = charlasAntRaw
            .Where(c => workerToUser.ContainsKey(c.SupervisorId!.Value))
            .GroupBy(c => workerToUser[c.SupervisorId!.Value])
            .Select(g => new { SupId = g.Key, N = g.Count() })
            .ToList();

        resultado = resultado.Select(r =>
        {
            var rAnt = racsAnt   .FirstOrDefault(x => x.SupId == r.SupervisorId)?.N ?? 0;
            var oAnt = optAnt    .FirstOrDefault(x => x.SupId == r.SupervisorId)?.N ?? 0;
            var iAnt = inspAnt   .FirstOrDefault(x => x.SupId == r.SupervisorId)?.N ?? 0;
            var cAnt = charlasAnt.FirstOrDefault(x => x.SupId == r.SupervisorId)?.N ?? 0;
            var pctAnt = Math.Round(
                Math.Min(100m, Pct(rAnt, MetaRacs))         * 0.25m +
                Math.Min(100m, Pct(oAnt, MetaOpt))          * 0.25m +
                Math.Min(100m, Pct(iAnt, MetaInspecciones)) * 0.25m +
                Math.Min(100m, Pct(cAnt, MetaCharlas))      * 0.25m, 1);
            return r with { PctGeneralMesAnterior = pctAnt };
        }).ToList();

        return resultado.OrderByDescending(r => r.PctGeneral).ToList();
    }

    private static decimal Pct(int actual, int meta) =>
        meta == 0 ? 100m : Math.Round((decimal)actual / meta * 100m, 1);
}
