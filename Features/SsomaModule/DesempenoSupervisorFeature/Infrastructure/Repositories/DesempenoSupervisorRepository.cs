using Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Application.Dtos;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Infrastructure.Repositories;

public class DesempenoSupervisorRepository(IDbContextFactory<AppDbContext> factory)
{
    private const int MetaRacs         = 4;
    private const int MetaOpt          = 2;
    private const int MetaInspecciones = 2;
    private const int MetaCharlas      = 2;

    public async Task<List<DesempenoSupervisorDto>> GetDesempenoAsync(int mes, int anio)
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

        var supervisorUserIds = await ctx.UserRole
            .Where(ur => ssomaRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync();

        if (!supervisorUserIds.Any()) return [];

        // ── 2. Filtrar solo STAFF: User → Person → Worker (ContributorId IS NULL) ──
        // Relación: Person.UserId = User.UserId
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

        // UserId → WorkerId (solo staff)
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

        // ── 5. Bulk queries con fechas individuales ────────────────────────
        var racsDetalle = await ctx.SsomaRacs
            .Where(r => r.CreatedBy != null && supervisorUserIds.Contains(r.CreatedBy.Value)
                     && r.FechaReporte >= inicio && r.FechaReporte < fin)
            .Select(r => new { SupId = r.CreatedBy!.Value, r.ProyectoId, Fecha = r.FechaReporte })
            .ToListAsync();

        var optDetalle = await ctx.SsomaOpt
            .Where(o => o.CreatedBy != null && supervisorUserIds.Contains(o.CreatedBy.Value)
                     && o.Fecha >= inicio && o.Fecha < fin)
            .Select(o => new { SupId = o.CreatedBy!.Value, o.ProyectoId, Fecha = o.Fecha })
            .ToListAsync();

        var inspDetalle = await ctx.SsomaInspeccion
            .Where(i => i.CreatedBy != null && supervisorUserIds.Contains(i.CreatedBy.Value)
                     && i.Fecha >= inicio && i.Fecha < fin)
            .Select(i => new { SupId = i.CreatedBy!.Value, i.ProyectoId, Fecha = i.Fecha })
            .ToListAsync();

        var charlasDetalle = await ctx.SsCharlas
            .Where(c => c.CreadoPorId != null && supervisorUserIds.Contains(c.CreadoPorId.Value)
                     && c.Fecha >= inicio && c.Fecha < fin)
            .Select(c => new { SupId = c.CreadoPorId!.Value, ProyId = c.ProyectoId ?? 0, Fecha = c.Fecha })
            .ToListAsync();

        // ── 6. Proyectos involucrados ──────────────────────────────────────
        var proyectoIds = racsDetalle.Select(r => r.ProyectoId)
            .Union(optDetalle        .Select(o => o.ProyectoId))
            .Union(inspDetalle       .Select(i => i.ProyectoId))
            .Union(charlasDetalle.Where(c => c.ProyId > 0).Select(c => c.ProyId))
            .Distinct().ToList();

        var proyectos = await ctx.Project
            .Where(p => proyectoIds.Contains(p.ProjectId))
            .Select(p => new { p.ProjectId, Nombre = p.ProjectDescription })
            .ToListAsync();

        // ── 7. Combinaciones (supervisor, proyecto) con actividad en el mes ─
        // El filtro de staff (paso 2) ya excluye a no-staff como Justiniani.
        var combinaciones = racsDetalle.Select(r => (r.SupId, r.ProyectoId))
            .Concat(optDetalle  .Select(o => (o.SupId, o.ProyectoId)))
            .Concat(inspDetalle .Select(i => (i.SupId, i.ProyectoId)))
            .Concat(charlasDetalle.Where(c => c.ProyId > 0).Select(c => (c.SupId, c.ProyId)))
            .Distinct()
            .ToList();

        // ── 8. Construir resultados ────────────────────────────────────────
        var resultado = new List<DesempenoSupervisorDto>();

        foreach (var (supId, proyId) in combinaciones)
        {
            var sup  = supervisores.FirstOrDefault(s => s.UserId == supId);
            var proy = proyectos   .FirstOrDefault(p => p.ProjectId == proyId);
            if (sup is null || proy is null) continue;

            var racs    = racsDetalle   .Count(r => r.SupId == supId && r.ProyectoId == proyId);
            var opt     = optDetalle    .Count(o => o.SupId == supId && o.ProyectoId == proyId);
            var insp    = inspDetalle   .Count(i => i.SupId == supId && i.ProyectoId == proyId);
            var charlas = charlasDetalle.Count(c => c.SupId == supId && c.ProyId    == proyId);

            var pctRacs    = Math.Min(100m, Pct(racs,    MetaRacs));
            var pctOpt     = Math.Min(100m, Pct(opt,     MetaOpt));
            var pctInsp    = Math.Min(100m, Pct(insp,    MetaInspecciones));
            var pctCharlas = Math.Min(100m, Pct(charlas, MetaCharlas));
            var pctGeneral = (pctRacs + pctOpt + pctInsp + pctCharlas) / 4m;

            // Fecha del N-ésimo registro de cada componente
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

            // FechaLogro100 = cuando cayó el último componente que faltaba
            DateTime? fechaLogro100 = null;
            if (fechaRacs.HasValue && fechaOpt.HasValue && fechaInsp.HasValue && fechaCharlas.HasValue)
                fechaLogro100 = new[] { fechaRacs.Value, fechaOpt.Value, fechaInsp.Value, fechaCharlas.Value }.Max();

            resultado.Add(new DesempenoSupervisorDto(
                SupervisorId:       supId,
                SupervisorNombre:   sup.Nombre,
                ProyectoId:         proyId,
                ProyectoNombre:     proy.Nombre ?? $"Proyecto {proyId}",
                Mes:                mes,
                Anio:               anio,
                MetaRacs:           MetaRacs,
                MetaOpt:            MetaOpt,
                MetaInspecciones:   MetaInspecciones,
                MetaCharlas:        MetaCharlas,
                ActualRacs:         racs,
                ActualOpt:          opt,
                ActualInspecciones: insp,
                ActualCharlas:      charlas,
                PctRacs:            pctRacs,
                PctOpt:             pctOpt,
                PctInspecciones:    pctInsp,
                PctCharlas:         pctCharlas,
                PctGeneral:         pctGeneral,
                FechaLogro100:      fechaLogro100,
                EsPrimeroEnProyecto: false   // se resuelve abajo
            ));
        }

        // ── 9. Marcar al primero en llegar a 100% por proyecto ─────────────
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
