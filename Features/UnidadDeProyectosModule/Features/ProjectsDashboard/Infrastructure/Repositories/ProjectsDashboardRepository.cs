using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Infrastructure.Repositories
{
    public class ProjectsDashboardRepository : IProjectsDashboardRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogger<ProjectsDashboardRepository> _logger;

        public ProjectsDashboardRepository(
            IDbContextFactory<AppDbContext> factory,
            ILogger<ProjectsDashboardRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        // -----------------------------------------------------------------------
        // Filters
        // -----------------------------------------------------------------------

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

        // -----------------------------------------------------------------------
        // Dashboard principal
        // -----------------------------------------------------------------------

        public async Task<List<ProyectoDetalleDto>> GetDashboardDataAsync(
            int? proyectoId, string? estado, int? responsableArqComId, DateOnly today)
        {
            try
            {
                using var ctx = _factory.CreateDbContext();

                var projects = await BuildProjectQueryAsync(ctx, proyectoId, estado, responsableArqComId);
                _logger.LogInformation("DASHBOARD_DEBUG projects={Count}", projects.Count);
                if (projects.Count == 0) return new();

                var projectIds = projects.Select(p => p.ProjectId).ToList();
                var (historyToProject, latestHistoryIds) = await GetLatestHistoriesAsync(ctx, projectIds);
                _logger.LogInformation("DASHBOARD_DEBUG latestHistories={Count}", latestHistoryIds.Count);
                if (latestHistoryIds.Count == 0)
                    return projects.Select(p => EmptyProyectoDetalle(p.ProjectId, p.ProjectDescription, p.Estado, p.ResponsableArqCom)).ToList();

                var schedules = await GetSchedulesAsync(ctx, latestHistoryIds);
                _logger.LogInformation("DASHBOARD_DEBUG schedules={Count}", schedules.Count);
                var schedulesByProject = GroupSchedulesByProject(schedules, historyToProject);

                return projects.Select(p =>
                {
                    var items = schedulesByProject.TryGetValue(p.ProjectId, out var list) ? list : new();
                    return BuildProyectoDetalle(p.ProjectId, p.ProjectDescription, p.Estado, p.ResponsableArqCom, items, today);
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DASHBOARD_ERROR");
                throw;
            }
        }

        // -----------------------------------------------------------------------
        // Ranking de responsables
        // -----------------------------------------------------------------------

        public async Task<List<ResponsableRankingDto>> GetRankingResponsablesAsync(
            int? proyectoId, string? estado, int? responsableArqComId, DateOnly today)
        {
            using var ctx = _factory.CreateDbContext();

            var projects = await BuildProjectQueryAsync(ctx, proyectoId, estado, responsableArqComId);
            if (projects.Count == 0) return new();

            var projectIds = projects.Select(p => p.ProjectId).ToList();
            var (historyToProject, latestHistoryIds) = await GetLatestHistoriesAsync(ctx, projectIds);
            if (latestHistoryIds.Count == 0) return new();

            var schedules = await GetSchedulesAsync(ctx, latestHistoryIds);
            var schedulesByProject = GroupSchedulesByProject(schedules, historyToProject);

            // Proyectos con responsable asignado
            var proyectosConResponsable = projects
                .Where(p => p.ResponsableArqComId.HasValue)
                .ToList();

            if (proyectosConResponsable.Count == 0) return new();

            var workerIds = proyectosConResponsable.Select(p => p.ResponsableArqComId!.Value).Distinct().ToList();
            var workers = await ctx.Worker
                .Where(w => workerIds.Contains(w.Id))
                .Select(w => new { w.Id, Nombre = w.Person != null ? w.Person.FullName : null })
                .ToDictionaryAsync(w => w.Id, w => w.Nombre);

            return proyectosConResponsable
                .GroupBy(p => p.ResponsableArqComId!.Value)
                .Select(g =>
                {
                    var responsableId = g.Key;
                    var totalProyectos = g.Count();

                    var allItems = g
                        .SelectMany(p => schedulesByProject.TryGetValue(p.ProjectId, out var s) ? s : new())
                        .ToList();

                    var totalActs = allItems.Count;
                    var completadas = allItems.Count(m => m.FechaRealFin != null);
                    var vencidas = allItems.Count(m => m.PlannedEndDate < today && m.FechaRealFin == null);
                    var score = totalActs > 0
                        ? Math.Max(0d, Math.Round((double)completadas / totalActs * 100 - vencidas * 5, 1))
                        : 0d;

                    workers.TryGetValue(responsableId, out var nombre);

                    return new ResponsableRankingDto
                    {
                        ResponsableId = responsableId,
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

        // -----------------------------------------------------------------------
        // Heatmap de carga
        // -----------------------------------------------------------------------

        public async Task<List<HeatmapCargaItemDto>> GetHeatmapCargaAsync(
            int? proyectoId, string? estado, int? responsableArqComId, DateOnly fechaDesde, DateOnly fechaHasta)
        {
            using var ctx = _factory.CreateDbContext();

            var projects = await BuildProjectQueryAsync(ctx, proyectoId, estado, responsableArqComId);
            var proyectosConResponsable = projects.Where(p => p.ResponsableArqComId.HasValue).ToList();
            if (proyectosConResponsable.Count == 0) return new();

            var projectIds = proyectosConResponsable.Select(p => p.ProjectId).ToList();
            var (historyToProject, latestHistoryIds) = await GetLatestHistoriesAsync(ctx, projectIds);
            if (latestHistoryIds.Count == 0) return new();

            var schedules = await GetSchedulesAsync(ctx, latestHistoryIds,
                fechaDesde: fechaDesde, fechaHasta: fechaHasta);

            if (schedules.Count == 0) return new();

            // Mapa projectId → responsableArqComId
            var projectResponsable = proyectosConResponsable
                .ToDictionary(p => p.ProjectId, p => p.ResponsableArqComId!.Value);

            var workerIds = proyectosConResponsable.Select(p => p.ResponsableArqComId!.Value).Distinct().ToList();
            var workers = await ctx.Worker
                .Where(w => workerIds.Contains(w.Id))
                .Select(w => new { w.Id, Nombre = w.Person != null ? w.Person.FullName : null })
                .ToDictionaryAsync(w => w.Id, w => w.Nombre);

            return schedules
                .Where(s => historyToProject.TryGetValue(s.MilestoneScheduleHistoryId, out var pid)
                            && projectResponsable.ContainsKey(pid)
                            && s.PlannedEndDate.HasValue)
                .GroupBy(s =>
                {
                    historyToProject.TryGetValue(s.MilestoneScheduleHistoryId, out var pid);
                    return new { ResponsableId = projectResponsable[pid], Semana = ToIsoWeekLabel(s.PlannedEndDate!.Value) };
                })
                .Select(g =>
                {
                    workers.TryGetValue(g.Key.ResponsableId, out var nombre);
                    return new HeatmapCargaItemDto
                    {
                        ResponsableId = g.Key.ResponsableId,
                        ResponsableNombre = nombre,
                        Semana = g.Key.Semana,
                        CantidadActividades = g.Count()
                    };
                })
                .OrderBy(h => h.Semana).ThenBy(h => h.ResponsableNombre)
                .ToList();
        }

        // -----------------------------------------------------------------------
        // Detalle por proyecto
        // -----------------------------------------------------------------------

        public async Task<ProyectoDetailDashboardDto> GetProyectoDetailAsync(int proyectoId, DateOnly today)
        {
            using var ctx = _factory.CreateDbContext();

            // Proyecto y responsable
            var project = await ctx.Project
                .Where(p => p.ProjectId == proyectoId)
                .Select(p => new { p.ProjectId, p.ResponsableArqComId, p.ResponsableArqCom })
                .FirstOrDefaultAsync();

            string? responsableNombre = project?.ResponsableArqCom;

            // Latest history
            var latestHistoryId = await ctx.MilestoneScheduleHistory
                .Where(h => h.ProjectId == proyectoId && h.State)
                .Select(h => (int?)h.MilestoneScheduleHistoryId)
                .MaxAsync();

            if (latestHistoryId == null)
            {
                return new ProyectoDetailDashboardDto
                {
                    Kpis = new ProyectoDetailKpisDto { Semaforo = "verde" }
                };
            }

            var schedules = await GetSchedulesAsync(ctx, new List<int> { latestHistoryId.Value });

            // Nombres de hitos
            var milestoneIds = schedules.Select(s => s.MilestoneId).Distinct().ToList();
            var milestoneNames = await ctx.Milestone
                .Where(m => milestoneIds.Contains(m.MilestoneId))
                .ToDictionaryAsync(m => m.MilestoneId, m => m.MilestoneDescription);

            var total = schedules.Count;
            var culminadas = schedules.Count(m => m.FechaRealFin != null);
            var vencidas = schedules.Count(m => m.PlannedEndDate < today && m.FechaRealFin == null);
            var enProceso = schedules.Count(m =>
                m.FechaRealFin == null
                && m.PlannedStartDate.HasValue && m.PlannedStartDate <= today
                && (m.PlannedEndDate == null || m.PlannedEndDate >= today));
            var avance = total > 0 ? Math.Round((double)culminadas / total * 100, 1) : 0d;

            var diasRetraso = vencidas > 0
                ? schedules
                    .Where(m => m.PlannedEndDate < today && m.FechaRealFin == null && m.PlannedEndDate.HasValue)
                    .Max(m => today.DayNumber - m.PlannedEndDate!.Value.DayNumber)
                : 0;

            var semaforo = diasRetraso == 0 ? "verde" : diasRetraso <= 7 ? "amarillo" : "rojo";

            milestoneNames.TryGetValue(0, out _); // warm-up

            var actVencidas = schedules
                .Where(m => m.PlannedEndDate < today && m.FechaRealFin == null && m.PlannedEndDate.HasValue)
                .Select(m =>
                {
                    milestoneNames.TryGetValue(m.MilestoneId, out var nombre);
                    return new ActividadVencidaDto
                    {
                        Id = m.MilestoneScheduleId,
                        Nombre = nombre ?? string.Empty,
                        Tipo = null,
                        ResponsableNombre = responsableNombre,
                        FinProgramado = m.PlannedEndDate,
                        DiasRetraso = today.DayNumber - m.PlannedEndDate!.Value.DayNumber
                    };
                })
                .OrderByDescending(a => a.DiasRetraso)
                .ToList();

            var gantt = schedules
                .Select(m =>
                {
                    milestoneNames.TryGetValue(m.MilestoneId, out var nombre);
                    return new ActividadGanttDto
                    {
                        Id = m.MilestoneScheduleId,
                        Nombre = nombre ?? string.Empty,
                        InicioProgramado = m.PlannedStartDate,
                        FinProgramado = m.PlannedEndDate,
                        FinEfectivo = m.FechaRealFin,
                        Estado = ComputeEstado(m.PlannedStartDate, m.PlannedEndDate, m.FechaRealFin, today),
                        ResponsableNombre = responsableNombre
                    };
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

        // -----------------------------------------------------------------------
        // Helpers privados
        // -----------------------------------------------------------------------

        private async Task<List<ProjectFlat>> BuildProjectQueryAsync(
            AppDbContext ctx, int? proyectoId, string? estado, int? responsableArqComId)
        {
            var q = ctx.Project.Where(p => p.State);
            if (proyectoId.HasValue) q = q.Where(p => p.ProjectId == proyectoId.Value);
            if (estado != null) q = q.Where(p => p.Estado == estado);
            if (responsableArqComId.HasValue) q = q.Where(p => p.ResponsableArqComId == responsableArqComId.Value);

            return await q
                .Select(p => new ProjectFlat
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                    Estado = p.Estado,
                    ResponsableArqCom = p.ResponsableArqCom,
                    ResponsableArqComId = p.ResponsableArqComId
                })
                .ToListAsync();
        }

        private async Task<(Dictionary<int, int> HistoryToProject, List<int> LatestHistoryIds)>
            GetLatestHistoriesAsync(AppDbContext ctx, List<int> projectIds)
        {
            var allHistories = await ctx.MilestoneScheduleHistory
                .Where(h => projectIds.Contains(h.ProjectId) && h.State)
                .Select(h => new { h.MilestoneScheduleHistoryId, h.ProjectId })
                .ToListAsync();

            // Un cronograma por proyecto: el de mayor id
            var latestPerProject = allHistories
                .GroupBy(h => h.ProjectId)
                .Select(g => g.OrderByDescending(h => h.MilestoneScheduleHistoryId).First())
                .ToList();

            var historyToProject = latestPerProject.ToDictionary(h => h.MilestoneScheduleHistoryId, h => h.ProjectId);
            var ids = latestPerProject.Select(h => h.MilestoneScheduleHistoryId).ToList();

            return (historyToProject, ids);
        }

        private async Task<List<MilestoneScheduleFlat>> GetSchedulesAsync(
            AppDbContext ctx, List<int> historyIds,
            DateOnly? fechaDesde = null, DateOnly? fechaHasta = null)
        {
            var q = ctx.MilestoneSchedule
                .Where(s => historyIds.Contains(s.MilestoneScheduleHistoryId) && s.State);

            if (fechaDesde.HasValue)
                q = q.Where(s => s.PlannedEndDate >= fechaDesde.Value);
            if (fechaHasta.HasValue)
                q = q.Where(s => s.PlannedEndDate <= fechaHasta.Value);

            return await q
                .Select(s => new MilestoneScheduleFlat
                {
                    MilestoneScheduleId = s.MilestoneScheduleId,
                    MilestoneId = s.MilestoneId,
                    MilestoneScheduleHistoryId = s.MilestoneScheduleHistoryId,
                    PlannedStartDate = s.PlannedStartDate,
                    PlannedEndDate = s.PlannedEndDate,
                    FechaRealFin = s.FechaRealFin
                })
                .ToListAsync();
        }

        private static Dictionary<int, List<MilestoneScheduleFlat>> GroupSchedulesByProject(
            List<MilestoneScheduleFlat> schedules, Dictionary<int, int> historyToProject)
        {
            return schedules
                .GroupBy(s => historyToProject.TryGetValue(s.MilestoneScheduleHistoryId, out var pid) ? pid : -1)
                .Where(g => g.Key != -1)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private static ProyectoDetalleDto BuildProyectoDetalle(
            int projectId, string? description, string? estado, string? responsable,
            List<MilestoneScheduleFlat> items, DateOnly today)
        {
            var total = items.Count;
            var culminadas = items.Count(m => m.FechaRealFin != null);
            var vencidas = items.Count(m => m.PlannedEndDate < today && m.FechaRealFin == null);
            var enProceso = items.Count(m =>
                m.FechaRealFin == null
                && m.PlannedStartDate.HasValue && m.PlannedStartDate <= today
                && (m.PlannedEndDate == null || m.PlannedEndDate >= today));
            var avance = total > 0 ? Math.Round((double)culminadas / total * 100, 1) : 0d;

            var diasRetraso = vencidas > 0
                ? items
                    .Where(m => m.PlannedEndDate < today && m.FechaRealFin == null && m.PlannedEndDate.HasValue)
                    .Max(m => today.DayNumber - m.PlannedEndDate!.Value.DayNumber)
                : 0;

            return new ProyectoDetalleDto
            {
                ProjectId = projectId,
                ProjectDescription = description,
                Estado = estado,
                ResponsableArqCom = responsable,
                TotalActividades = total,
                Culminadas = culminadas,
                EnProceso = enProceso,
                Vencidas = vencidas,
                PorcentajeAvance = avance,
                EstaConRetraso = vencidas > 0,
                DiasRetraso = diasRetraso,
                Semaforo = diasRetraso == 0 ? "verde" : diasRetraso <= 7 ? "amarillo" : "rojo",
                EtapaNombre = null
            };
        }

        private static ProyectoDetalleDto EmptyProyectoDetalle(
            int projectId, string? description, string? estado, string? responsable) =>
            new()
            {
                ProjectId = projectId,
                ProjectDescription = description,
                Estado = estado,
                ResponsableArqCom = responsable,
                Semaforo = "verde"
            };

        private static string ComputeEstado(
            DateOnly? plannedStart, DateOnly? plannedEnd, DateOnly? fechaRealFin, DateOnly today)
        {
            if (fechaRealFin != null) return "CULMINADO";
            if (plannedEnd.HasValue && plannedEnd < today) return "VENCIDO";
            if (plannedStart.HasValue && plannedStart <= today) return "EN_PROCESO";
            return "PENDIENTE";
        }

        private static string ToIsoWeekLabel(DateOnly date)
        {
            var dt = date.ToDateTime(TimeOnly.MinValue);
            var year = System.Globalization.ISOWeek.GetYear(dt);
            var week = System.Globalization.ISOWeek.GetWeekOfYear(dt);
            return $"{year}-W{week:D2}";
        }

        private class MilestoneScheduleFlat
        {
            public int MilestoneScheduleId { get; init; }
            public int MilestoneId { get; init; }
            public int MilestoneScheduleHistoryId { get; init; }
            public DateOnly? PlannedStartDate { get; init; }
            public DateOnly? PlannedEndDate { get; init; }
            public DateOnly? FechaRealFin { get; init; }
        }

        private class ProjectFlat
        {
            public int ProjectId { get; set; }
            public string? ProjectDescription { get; set; }
            public string? Estado { get; set; }
            public string? ResponsableArqCom { get; set; }
            public int? ResponsableArqComId { get; set; }
        }
    }
}
