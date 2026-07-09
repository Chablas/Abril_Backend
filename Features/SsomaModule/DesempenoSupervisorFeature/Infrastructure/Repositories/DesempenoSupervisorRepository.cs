using Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Infrastructure.Models;
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

    // Samuel Justiniani (sjustiniani@abril.pe) — excepción explícita a pedido suyo
    // ("los Coordinadores SSOMA y yo"). Su ocupación real es "SSOMA", no calza con el
    // texto "Coordinador SSOMA" que sí usan los demás coordinadores.
    private const int WorkerIdSamuel = 12305;

    /// <summary>
    /// El usuario logueado tiene permiso para ocultar/mostrar (cualquier tarjeta) si
    /// su propio worker es Coordinador SSOMA — revisa tanto "ocupacion" como
    /// "categoria" porque ese dato no siempre vive en el mismo campo — o si es Samuel.
    /// </summary>
    public async Task<bool> EsCoordinadorSsomaAsync(int userId)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var datos = await ctx.Person
            .Where(p => p.UserId == userId)
            .Join(ctx.Worker, p => p.PersonId, w => w.PersonId, (p, w) => new { w.Id, w.Ocupacion, w.Categoria })
            .FirstOrDefaultAsync();
        if (datos is null) return false;
        if (datos.Id == WorkerIdSamuel) return true;

        // "Coordinador" y "SSOMA" pueden venir en el mismo campo o repartidos entre
        // Categoria y Ocupacion (ej. Categoria="Coordinador", Ocupacion="SSOMA").
        var textoCombinado = $"{datos.Categoria} {datos.Ocupacion}";

        return textoCombinado.Contains("Coordinador", StringComparison.OrdinalIgnoreCase)
            && textoCombinado.Contains("SSOMA", StringComparison.OrdinalIgnoreCase);
    }

    public async Task OcultarAsync(int workerId, string? motivo, int userId)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        var existe = await ctx.SsDesempenoSupervisorExcluidos.FirstOrDefaultAsync(e => e.WorkerId == workerId);
        if (existe is not null) return;
        ctx.SsDesempenoSupervisorExcluidos.Add(new SsDesempenoSupervisorExcluido
        {
            WorkerId = workerId,
            Motivo = motivo,
            ExcluidoPor = userId,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
    }

    public async Task MostrarAsync(int workerId)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var existe = await ctx.SsDesempenoSupervisorExcluidos.FirstOrDefaultAsync(e => e.WorkerId == workerId);
        if (existe is null) return;
        ctx.SsDesempenoSupervisorExcluidos.Remove(existe);
        await ctx.SaveChangesAsync();
    }

    public async Task<List<DesempenoSupervisorDto>> GetDesempenoAsync(int mes, int anio, int? proyectoId, bool incluirOcultos = false, bool puedeOcultar = false)
    {
        await using var ctx = await factory.CreateDbContextAsync();

        var inicio = new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Utc);
        var fin    = inicio.AddMonths(1);

        var excluidoIds = (await ctx.SsDesempenoSupervisorExcluidos
            .Select(e => e.WorkerId)
            .ToListAsync())
            .ToHashSet();

        // ── 1. Base: staff activo con proyecto ACTUAL (ss_hab_worker_proyecto, no
        // worker_vinculaciones — esa tabla es de vinculación laboral/empresa, no de
        // asignación de obra). No importa el rol del usuario ni si tiene cuenta.
        //
        // El "proyecto actual" de un worker es su vinculación activa en worker_vinculaciones
        // (1 sola fila con fecha_fin IS NULL) — la misma fuente que usa la ficha del
        // trabajador en Habilitación para el campo "OBRA". ss_hab_worker_proyecto
        // (WorkerProyecto) es otra cosa: la lista de "Proyectos asignados", que sí
        // admite varias filas activas a la vez a propósito — no sirve para "actual".
        var vinculacionesActivas = await ctx.WorkerVinculacion
            .Where(v => v.FechaFin == null && v.ProyectoId != null)
            .ToListAsync();

        var proyectoActualPorWorker = vinculacionesActivas
            .GroupBy(v => v.WorkerId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.FechaInicio).ThenByDescending(x => x.Id).First().ProyectoId!.Value);

        var staffBase = (await ctx.Worker
            .Where(w => w.ObraOficina == "Staff" && w.Estado == "ACTIVO")
            .Select(w => new { WorkerId = w.Id, w.PersonId, w.Ocupacion, w.ApellidoNombre })
            .ToListAsync())
            .Where(s => proyectoActualPorWorker.ContainsKey(s.WorkerId))
            .Select(s => new { s.WorkerId, s.PersonId, s.Ocupacion, s.ApellidoNombre, ProyectoId = proyectoActualPorWorker[s.WorkerId] })
            .ToList();

        if (proyectoId.HasValue)
            staffBase = staffBase.Where(s => s.ProyectoId == proyectoId.Value).ToList();

        if (!incluirOcultos)
            staffBase = staffBase.Where(s => !excluidoIds.Contains(s.WorkerId)).ToList();

        if (!staffBase.Any()) return [];

        var supWorkerIds = staffBase.Select(s => s.WorkerId).Distinct().ToList();

        var proyectoPorWorker = staffBase
            .GroupBy(s => s.WorkerId)
            .ToDictionary(g => g.Key, g => g.First().ProyectoId);

        // Un worker puede tener más de una fila activa en ss_hab_worker_proyecto
        // (varias obras a la vez); para nombre/usuario basta con una sola fila por worker.
        var staffPorWorker = staffBase
            .GroupBy(s => s.WorkerId)
            .Select(g => g.First())
            .ToList();

        // ── 2. Nombres y User vinculado (si existe) por worker ─────────────
        var personIds = staffPorWorker.Where(s => s.PersonId != null).Select(s => s.PersonId!.Value).Distinct().ToList();

        var personas = await ctx.Person
            .Where(p => personIds.Contains(p.PersonId))
            .Select(p => new
            {
                p.PersonId,
                p.UserId,
                Nombre = p.FullName ?? (p.FirstNames + " " + p.FirstLastName).Trim()
            })
            .ToListAsync();

        var personaPorPersonId = personas.ToDictionary(p => p.PersonId);

        string NombreDeWorker(int workerId, int? personId, string? apellidoNombre)
        {
            if (personId != null && personaPorPersonId.TryGetValue(personId.Value, out var p) && !string.IsNullOrWhiteSpace(p.Nombre))
                return p.Nombre;
            return string.IsNullOrWhiteSpace(apellidoNombre) ? "Sin nombre" : apellidoNombre;
        }

        var nombrePorWorker = staffPorWorker.ToDictionary(
            s => s.WorkerId,
            s => NombreDeWorker(s.WorkerId, s.PersonId, s.ApellidoNombre));

        var workerToUser = staffPorWorker
            .Where(s => s.PersonId != null
                     && personaPorPersonId.TryGetValue(s.PersonId.Value, out var p)
                     && p.UserId != null)
            .ToDictionary(s => s.WorkerId, s => personaPorPersonId[s.PersonId!.Value].UserId!.Value);

        var userToWorker = workerToUser
            .GroupBy(kv => kv.Value)
            .ToDictionary(g => g.Key, g => g.First().Key);

        var supervisorUserIds = workerToUser.Values.Distinct().ToList();

        var nombreToWorkerId = nombrePorWorker
            .GroupBy(kv => kv.Value.ToUpper().Trim())
            .ToDictionary(g => g.Key, g => g.First().Key);

        var supervisorNombres = nombreToWorkerId.Keys.ToList();

        var residenteWorkerIds = staffBase
            .Where(s => string.Equals(s.Ocupacion, "Residencia", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.WorkerId)
            .ToHashSet();

        // ── 3. Queries de actividad — SIN filtro de proyecto (lo que importa es el supervisor) ─
        int WorkerIdDesdeUserONombre(int? createdBy, string? nombre)
        {
            if (createdBy != null)
                return userToWorker.TryGetValue(createdBy.Value, out var wid) ? wid : 0;
            if (nombre != null && nombreToWorkerId.TryGetValue(nombre.ToUpper().Trim(), out var wid2))
                return wid2;
            return 0;
        }

        var racsRaw = await ctx.SsomaRacs
            .Where(r => r.FechaReporte >= inicio && r.FechaReporte < fin
                     && (
                         (r.CreatedBy != null && supervisorUserIds.Contains(r.CreatedBy.Value)) ||
                         (r.CreatedBy == null && r.ReportanteNombre != null && supervisorNombres.Contains(r.ReportanteNombre.ToUpper().Trim()))
                     ))
            .Select(r => new { r.CreatedBy, r.ReportanteNombre, Fecha = r.FechaReporte })
            .ToListAsync();

        var racsDetalle = racsRaw
            .Select(r => new { SupId = WorkerIdDesdeUserONombre(r.CreatedBy, r.ReportanteNombre), r.Fecha })
            .Where(r => r.SupId > 0)
            .ToList();

        var optRaw = await ctx.SsomaOpt
            .Where(o => o.Fecha >= inicio && o.Fecha < fin
                     && (
                         (o.CreatedBy != null && supervisorUserIds.Contains(o.CreatedBy.Value)) ||
                         (o.CreatedBy == null && o.ObservadorNombre != null && supervisorNombres.Contains(o.ObservadorNombre.ToUpper().Trim()))
                     ))
            .Select(o => new { o.CreatedBy, o.ObservadorNombre, o.Fecha })
            .ToListAsync();

        var optDetalle = optRaw
            .Select(o => new { SupId = WorkerIdDesdeUserONombre(o.CreatedBy, o.ObservadorNombre), o.Fecha })
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
            .Select(i => new { SupId = WorkerIdDesdeUserONombre(i.CreatedBy, i.InspectorNombre), i.Fecha })
            .Where(i => i.SupId > 0)
            .ToList();

        // Charlas: SupervisorId ya es WorkerId directamente
        var charlasDetalle = await ctx.SsCharlas
            .Where(c => c.SupervisorId != null
                     && supWorkerIds.Contains(c.SupervisorId.Value)
                     && c.Fecha >= inicio && c.Fecha < fin
                     && c.State)
            .Select(c => new { SupId = c.SupervisorId!.Value, c.Fecha })
            .ToListAsync();

        // ── 4. Nuevos indicadores — dependen de tener cuenta de usuario ────
        var leccionesPorSupervisor = await ctx.Lesson
            .Where(l => supervisorUserIds.Contains(l.CreatedUserId)
                     && l.CreatedDateTime >= inicio && l.CreatedDateTime < fin
                     && l.Active && l.State
                     && l.ApprovalStatus != "RECHAZADA")
            .GroupBy(l => l.CreatedUserId)
            .Select(g => new { UserId = g.Key, N = g.Count() })
            .ToListAsync();

        var leccionesPorWorker = leccionesPorSupervisor
            .Where(x => userToWorker.ContainsKey(x.UserId))
            .ToDictionary(x => userToWorker[x.UserId], x => x.N);

        // ss_eval_supervisor es una tabla legacy sin ningun flujo que la escriba hoy;
        // la evaluacion de contratistas real vive en ev_evaluacion_contratista
        // (feature "Evaluar Contratista" del modulo Evaluaciones).
        var evalContratistaPorSupervisor = await ctx.EvEvaluacionesContratista
            .Where(e => supervisorUserIds.Contains(e.EvaluadorUserId)
                     && e.Periodo!.Mes == mes && e.Periodo!.Anio == anio)
            .GroupBy(e => e.EvaluadorUserId)
            .Select(g => new { UserId = g.Key, N = g.Count() })
            .ToListAsync();

        var evalContratistaPorWorker = evalContratistaPorSupervisor
            .Where(x => userToWorker.ContainsKey(x.UserId))
            .ToDictionary(x => userToWorker[x.UserId], x => x.N);

        // Se filtra por el mes/año del período de evaluación (ev_periodo), no por CreatedAt:
        // el período de junio cierra el 4 de julio, así que evaluaciones de junio creadas
        // en esos primeros días de julio quedaban contadas como "julio" y no marcaban el check.
        var evalResidentePorSupervisor = await ctx.EvEvaluacionesResidente
            .Where(e => e.EvaluadorUserId != null && supervisorUserIds.Contains(e.EvaluadorUserId.Value)
                     && e.Periodo!.Mes == mes && e.Periodo!.Anio == anio
                     && !e.NoAplica)
            .GroupBy(e => e.EvaluadorUserId)
            .Select(g => new { UserId = g.Key!.Value, N = g.Count() })
            .ToListAsync();

        var evalResidentePorWorker = evalResidentePorSupervisor
            .Where(x => userToWorker.ContainsKey(x.UserId))
            .ToDictionary(x => userToWorker[x.UserId], x => x.N);

        // ── 5. Combinaciones supervisor(worker) → proyecto actual ─────────
        var combinaciones = supWorkerIds
            .Select(wid => (SupId: wid, ProyId: proyectoPorWorker[wid]))
            .ToList();

        var proyectoIds = combinaciones.Select(c => c.ProyId).Distinct().ToList();

        var proyectos = await ctx.Project
            .Where(p => proyectoIds.Contains(p.ProjectId))
            .Select(p => new { p.ProjectId, Nombre = p.ProjectDescription })
            .ToListAsync();

        // ── 6. Construir resultados ────────────────────────────────────────
        var resultado = new List<DesempenoSupervisorDto>();

        foreach (var (supId, proyId) in combinaciones)
        {
            if (proyId == 0) continue;

            var proy = proyectos.FirstOrDefault(p => p.ProjectId == proyId);
            if (proy is null) continue;

            var racs    = racsDetalle   .Count(r => r.SupId == supId);
            var opt     = optDetalle    .Count(o => o.SupId == supId);
            var insp    = inspDetalle   .Count(i => i.SupId == supId);
            var charlas = charlasDetalle.Count(c => c.SupId == supId);
            var leccion       = leccionesPorWorker      .GetValueOrDefault(supId);
            var evalContrat   = evalContratistaPorWorker.GetValueOrDefault(supId);
            var evalResidente = evalResidentePorWorker  .GetValueOrDefault(supId);

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
                SupervisorNombre:      nombrePorWorker[supId],
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
                EsResidente:              residenteWorkerIds.Contains(supId),
                EsOculto:                 excluidoIds.Contains(supId),
                PuedeOcultarse:           puedeOcultar
            ));
        }

        // ── 7. Marcar al primero en llegar al 100% por proyecto ────────────
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

        // ── 8. Tendencia vs mes anterior ──────────────────────────────────────
        var mesAnt    = mes == 1 ? 12 : mes - 1;
        var anioAnt   = mes == 1 ? anio - 1 : anio;
        var inicioAnt = new DateTime(anioAnt, mesAnt, 1, 0, 0, 0, DateTimeKind.Utc);
        var finAnt    = inicioAnt.AddMonths(1);

        var supIds2 = resultado.Select(r => r.SupervisorId).Distinct().ToList();
        var userIds2 = supIds2.Where(wid => workerToUser.ContainsKey(wid)).Select(wid => workerToUser[wid]).ToList();

        var racsAntRaw = await ctx.SsomaRacs
            .Where(r => r.FechaReporte >= inicioAnt && r.FechaReporte < finAnt
                     && r.CreatedBy != null && userIds2.Contains(r.CreatedBy.Value))
            .GroupBy(r => r.CreatedBy)
            .Select(g => new { UserId = g.Key!.Value, N = g.Count() })
            .ToListAsync();
        var racsAnt = racsAntRaw.Where(x => userToWorker.ContainsKey(x.UserId))
            .ToDictionary(x => userToWorker[x.UserId], x => x.N);

        var optAntRaw = await ctx.SsomaOpt
            .Where(o => o.Fecha >= inicioAnt && o.Fecha < finAnt
                     && o.CreatedBy != null && userIds2.Contains(o.CreatedBy.Value))
            .GroupBy(o => o.CreatedBy)
            .Select(g => new { UserId = g.Key!.Value, N = g.Count() })
            .ToListAsync();
        var optAnt = optAntRaw.Where(x => userToWorker.ContainsKey(x.UserId))
            .ToDictionary(x => userToWorker[x.UserId], x => x.N);

        var inspAntRaw = await ctx.SsomaInspeccion
            .Where(i => i.Fecha >= inicioAnt && i.Fecha < finAnt
                     && i.CreatedBy != null && userIds2.Contains(i.CreatedBy.Value))
            .GroupBy(i => i.CreatedBy)
            .Select(g => new { UserId = g.Key!.Value, N = g.Count() })
            .ToListAsync();
        var inspAnt = inspAntRaw.Where(x => userToWorker.ContainsKey(x.UserId))
            .ToDictionary(x => userToWorker[x.UserId], x => x.N);

        var charlasAnt = await ctx.SsCharlas
            .Where(c => c.Fecha >= inicioAnt && c.Fecha < finAnt
                     && c.SupervisorId != null && supIds2.Contains(c.SupervisorId.Value)
                     && c.State && (c.Estado == "Enviado" || c.Estado == "Aprobado"))
            .GroupBy(c => c.SupervisorId)
            .Select(g => new { SupId = g.Key!.Value, N = g.Count() })
            .ToListAsync();
        var charlasAntPorWorker = charlasAnt.ToDictionary(x => x.SupId, x => x.N);

        resultado = resultado.Select(r =>
        {
            var rAnt = racsAnt.GetValueOrDefault(r.SupervisorId);
            var oAnt = optAnt.GetValueOrDefault(r.SupervisorId);
            var iAnt = inspAnt.GetValueOrDefault(r.SupervisorId);
            var cAnt = charlasAntPorWorker.GetValueOrDefault(r.SupervisorId);
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
