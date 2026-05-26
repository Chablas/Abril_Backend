using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Infrastructure.Repositories
{
    public class ProjectsDashboardRepository : IProjectsDashboardRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ProjectsDashboardRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<(List<string> Estados, List<ResponsableArqComSimpleDto> ResponsablesArqCom)> GetFiltersDataFactory()
        {
            using var ctx1 = _factory.CreateDbContext();
            using var ctx2 = _factory.CreateDbContext();

            var estadosTask = ctx1.Project
                .Where(p => p.State && p.Estado != null)
                .Select(p => p.Estado!)
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();

            var workerIdsTask = ctx2.Project
                .Where(p => p.State && p.ResponsableArqComId.HasValue)
                .Select(p => p.ResponsableArqComId!.Value)
                .Distinct()
                .ToListAsync();

            await Task.WhenAll(estadosTask, workerIdsTask);

            var workerIds = workerIdsTask.Result;
            using var ctx3 = _factory.CreateDbContext();
            var responsables = await ctx3.Worker
                .Where(w => workerIds.Contains(w.Id))
                .Select(w => new ResponsableArqComSimpleDto
                {
                    WorkerId = w.Id,
                    FullName = w.Person != null ? w.Person.FullName : null
                })
                .OrderBy(r => r.FullName)
                .ToListAsync();

            return (estadosTask.Result, responsables);
        }

        public async Task<List<ProyectoDetalleDto>> GetDashboardDataAsync(int? proyectoId, string? estado, int? responsableArqComId, DateOnly today)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.Project.Where(p => p.State);
            if (proyectoId.HasValue) query = query.Where(p => p.ProjectId == proyectoId.Value);
            if (estado != null) query = query.Where(p => p.Estado == estado);
            if (responsableArqComId.HasValue) query = query.Where(p => p.ResponsableArqComId == responsableArqComId.Value);

            var projects = await query
                .Select(p => new { p.ProjectId, p.ProjectDescription, p.Estado, p.ResponsableArqCom })
                .ToListAsync();

            var projectIds = projects.Select(p => p.ProjectId).ToList();

            var actividades = await ctx.AcActividad
                .Where(a => projectIds.Contains(a.ProjectId) && a.Activo)
                .Select(a => new
                {
                    a.Id,
                    a.ProjectId,
                    a.FinEfectivo,
                    a.FinProgramado,
                    a.InicioEfectivo,
                    a.EtapaId
                })
                .ToListAsync();

            var etapaIds = actividades
                .Where(a => a.EtapaId.HasValue)
                .Select(a => a.EtapaId!.Value)
                .Distinct()
                .ToList();

            var etapas = etapaIds.Count > 0
                ? await ctx.AcEtapa
                    .Where(e => etapaIds.Contains(e.Id))
                    .ToDictionaryAsync(e => e.Id, e => e.Nombre)
                : new Dictionary<int, string>();

            var actsByProject = actividades
                .GroupBy(a => a.ProjectId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return projects.Select(p =>
            {
                var acts = actsByProject.TryGetValue(p.ProjectId, out var list) ? list : new();
                var total = acts.Count;
                var culminadas = acts.Count(a => a.FinEfectivo != null);
                var vencidas = acts.Count(a => a.FinProgramado < today && a.FinEfectivo == null);
                var enProceso = acts.Count(a => a.FinEfectivo == null && a.InicioEfectivo != null);
                var avance = total > 0 ? Math.Round((double)culminadas / total * 100, 1) : 0d;

                var diasRetraso = vencidas > 0
                    ? acts
                        .Where(a => a.FinProgramado < today && a.FinEfectivo == null && a.FinProgramado.HasValue)
                        .Max(a => today.DayNumber - a.FinProgramado!.Value.DayNumber)
                    : 0;

                var semaforo = diasRetraso == 0 ? "verde" : diasRetraso <= 7 ? "amarillo" : "rojo";

                var lastEtapaId = acts
                    .Where(a => a.EtapaId.HasValue)
                    .OrderByDescending(a => a.Id)
                    .FirstOrDefault()?.EtapaId;
                var etapaNombre = lastEtapaId.HasValue && etapas.TryGetValue(lastEtapaId.Value, out var nombre)
                    ? nombre
                    : null;

                return new ProyectoDetalleDto
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                    Estado = p.Estado,
                    ResponsableArqCom = p.ResponsableArqCom,
                    TotalActividades = total,
                    Culminadas = culminadas,
                    EnProceso = enProceso,
                    Vencidas = vencidas,
                    PorcentajeAvance = avance,
                    EstaConRetraso = vencidas > 0,
                    DiasRetraso = diasRetraso,
                    Semaforo = semaforo,
                    EtapaNombre = etapaNombre
                };
            }).ToList();
        }

        public async Task<List<ResponsableRankingDto>> GetRankingResponsablesAsync(int? proyectoId, string? estado, int? responsableArqComId, DateOnly today)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.Project.Where(p => p.State);
            if (proyectoId.HasValue) query = query.Where(p => p.ProjectId == proyectoId.Value);
            if (estado != null) query = query.Where(p => p.Estado == estado);
            if (responsableArqComId.HasValue) query = query.Where(p => p.ResponsableArqComId == responsableArqComId.Value);

            var projectIds = await query.Select(p => p.ProjectId).ToListAsync();
            if (projectIds.Count == 0) return new();

            var actividades = await ctx.AcActividad
                .Where(a => projectIds.Contains(a.ProjectId) && a.Activo && a.UserId.HasValue)
                .Select(a => new { a.ProjectId, a.UserId, a.FinEfectivo, a.FinProgramado })
                .ToListAsync();

            if (actividades.Count == 0) return new();

            var workerIds = actividades.Select(a => a.UserId!.Value).Distinct().ToList();
            var workers = await ctx.Worker
                .Where(w => workerIds.Contains(w.Id))
                .Select(w => new { w.Id, Nombre = w.Person != null ? w.Person.FullName : null })
                .ToDictionaryAsync(w => w.Id, w => w.Nombre);

            return actividades
                .GroupBy(a => a.UserId!.Value)
                .Select(g =>
                {
                    var acts = g.ToList();
                    var totalActs = acts.Count;
                    var completadas = acts.Count(a => a.FinEfectivo != null);
                    var vencidas = acts.Count(a => a.FinProgramado < today && a.FinEfectivo == null);
                    var totalProyectos = acts.Select(a => a.ProjectId).Distinct().Count();
                    var score = totalActs > 0
                        ? Math.Max(0d, Math.Round((double)completadas / totalActs * 100 - vencidas * 5, 1))
                        : 0d;
                    workers.TryGetValue(g.Key, out var nombre);

                    return new ResponsableRankingDto
                    {
                        ResponsableId = g.Key,
                        ResponsableNombre = nombre,
                        TotalProyectos = totalProyectos,
                        ActividadesCompletadas = completadas,
                        ActividadesVencidas = vencidas,
                        TotalActividades = totalActs,
                        Score = score
                    };
                })
                .OrderByDescending(r => r.Score)
                .ToList();
        }

        public async Task<List<HeatmapCargaItemDto>> GetHeatmapCargaAsync(int? proyectoId, string? estado, int? responsableArqComId, DateOnly fechaDesde, DateOnly fechaHasta)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.Project.Where(p => p.State);
            if (proyectoId.HasValue) query = query.Where(p => p.ProjectId == proyectoId.Value);
            if (estado != null) query = query.Where(p => p.Estado == estado);
            if (responsableArqComId.HasValue) query = query.Where(p => p.ResponsableArqComId == responsableArqComId.Value);

            var projectIds = await query.Select(p => p.ProjectId).ToListAsync();
            if (projectIds.Count == 0) return new();

            var actividades = await ctx.AcActividad
                .Where(a => projectIds.Contains(a.ProjectId) && a.Activo
                    && a.UserId.HasValue
                    && a.FinProgramado >= fechaDesde
                    && a.FinProgramado <= fechaHasta)
                .Select(a => new { a.UserId, a.FinProgramado })
                .ToListAsync();

            if (actividades.Count == 0) return new();

            var workerIds = actividades.Select(a => a.UserId!.Value).Distinct().ToList();
            var workers = await ctx.Worker
                .Where(w => workerIds.Contains(w.Id))
                .Select(w => new { w.Id, Nombre = w.Person != null ? w.Person.FullName : null })
                .ToDictionaryAsync(w => w.Id, w => w.Nombre);

            return actividades
                .GroupBy(a => new { UserId = a.UserId!.Value, Semana = ToIsoWeekLabel(a.FinProgramado!.Value) })
                .Select(g =>
                {
                    workers.TryGetValue(g.Key.UserId, out var nombre);
                    return new HeatmapCargaItemDto
                    {
                        ResponsableId = g.Key.UserId,
                        ResponsableNombre = nombre,
                        Semana = g.Key.Semana,
                        CantidadActividades = g.Count()
                    };
                })
                .OrderBy(h => h.Semana).ThenBy(h => h.ResponsableNombre)
                .ToList();
        }

        public async Task<ProyectoDetailDashboardDto> GetProyectoDetailAsync(int proyectoId, DateOnly today)
        {
            using var ctx = _factory.CreateDbContext();

            var actividades = await ctx.AcActividad
                .Where(a => a.ProjectId == proyectoId && a.Activo)
                .Select(a => new
                {
                    a.Id,
                    a.Nombre,
                    a.Tipo,
                    a.UserId,
                    a.FinProgramado,
                    a.FinEfectivo,
                    a.InicioProgramado,
                    a.InicioEfectivo,
                    a.Estado
                })
                .ToListAsync();

            var workerIds = actividades
                .Where(a => a.UserId.HasValue)
                .Select(a => a.UserId!.Value)
                .Distinct()
                .ToList();

            var workers = workerIds.Count > 0
                ? await ctx.Worker
                    .Where(w => workerIds.Contains(w.Id))
                    .Select(w => new { w.Id, Nombre = w.Person != null ? w.Person.FullName : null })
                    .ToDictionaryAsync(w => w.Id, w => w.Nombre)
                : new Dictionary<int, string?>();

            var total = actividades.Count;
            var culminadas = actividades.Count(a => a.FinEfectivo != null);
            var vencidas = actividades.Count(a => a.FinProgramado < today && a.FinEfectivo == null);
            var enProceso = actividades.Count(a => a.FinEfectivo == null && a.InicioEfectivo != null);
            var avance = total > 0 ? Math.Round((double)culminadas / total * 100, 1) : 0d;

            var diasRetraso = vencidas > 0
                ? actividades
                    .Where(a => a.FinProgramado < today && a.FinEfectivo == null && a.FinProgramado.HasValue)
                    .Max(a => today.DayNumber - a.FinProgramado!.Value.DayNumber)
                : 0;

            var semaforo = diasRetraso == 0 ? "verde" : diasRetraso <= 7 ? "amarillo" : "rojo";

            string? GetNombre(int? userId)
            {
                if (!userId.HasValue) return null;
                workers.TryGetValue(userId.Value, out var n);
                return n;
            }

            var actVencidas = actividades
                .Where(a => a.FinProgramado < today && a.FinEfectivo == null && a.FinProgramado.HasValue)
                .Select(a => new ActividadVencidaDto
                {
                    Id = a.Id,
                    Nombre = a.Nombre,
                    Tipo = a.Tipo,
                    ResponsableNombre = GetNombre(a.UserId),
                    FinProgramado = a.FinProgramado,
                    DiasRetraso = today.DayNumber - a.FinProgramado!.Value.DayNumber
                })
                .OrderByDescending(a => a.DiasRetraso)
                .ToList();

            var gantt = actividades
                .Select(a => new ActividadGanttDto
                {
                    Id = a.Id,
                    Nombre = a.Nombre,
                    InicioProgramado = a.InicioProgramado,
                    FinProgramado = a.FinProgramado,
                    FinEfectivo = a.FinEfectivo,
                    Estado = a.Estado,
                    ResponsableNombre = GetNombre(a.UserId)
                })
                .OrderBy(a => a.InicioProgramado)
                .ToList();

            return new ProyectoDetailDashboardDto
            {
                Kpis = new ProyectoDetailKpisDto
                {
                    TotalActividades = total,
                    Culminadas = culminadas,
                    EnProceso = enProceso,
                    Vencidas = vencidas,
                    AvancePct = avance,
                    DiasRetraso = diasRetraso,
                    Semaforo = semaforo
                },
                ActividadesVencidas = actVencidas,
                Gantt = gantt
            };
        }

        private static string ToIsoWeekLabel(DateOnly date)
        {
            var dt = date.ToDateTime(TimeOnly.MinValue);
            var year = System.Globalization.ISOWeek.GetYear(dt);
            var week = System.Globalization.ISOWeek.GetWeekOfYear(dt);
            return $"{year}-W{week:D2}";
        }
    }
}
