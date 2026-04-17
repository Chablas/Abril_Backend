using Abril_Backend.Application.DTOs.ArquitecturaComercial;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Infrastructure.Repositories
{
    public class ArquitecturaComercialRepository : IArquitecturaComercialRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ArquitecturaComercialRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<ArqComercialDashboardDTO> GetDashboardData(string? semana, string? mes, int? proyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            var endOfWeek = startOfWeek.AddDays(6);

            // ── Base query: MilestoneSchedule + History + Milestone + Project ──
            var query = from ms in ctx.MilestoneSchedule
                        join msh in ctx.MilestoneScheduleHistory
                            on ms.MilestoneScheduleHistoryId equals msh.MilestoneScheduleHistoryId
                        join m in ctx.Milestone
                            on ms.MilestoneId equals m.MilestoneId
                        join p in ctx.Project
                            on msh.ProjectId equals p.ProjectId
                        where ms.State && msh.State && m.State && p.State
                        select new
                        {
                            ms.MilestoneScheduleId,
                            ms.MilestoneId,
                            MilestoneDescription = m.MilestoneDescription,
                            ms.PlannedStartDate,
                            ms.PlannedEndDate,
                            ms.Active,
                            ms.CreatedDateTime,
                            msh.ProjectId,
                            ProjectDescription = p.ProjectDescription,
                            msh.MilestoneScheduleHistoryId,
                        };

            // ── Apply filters ──
            if (proyectoId.HasValue && proyectoId.Value > 0)
                query = query.Where(x => x.ProjectId == proyectoId.Value);

            if (!string.IsNullOrEmpty(semana))
            {
                if (DateOnly.TryParse(semana, out var semanaDate))
                {
                    var semanaEnd = semanaDate.AddDays(6);
                    query = query.Where(x =>
                        x.PlannedStartDate <= semanaEnd && x.PlannedEndDate >= semanaDate);
                }
            }

            if (!string.IsNullOrEmpty(mes))
            {
                if (DateOnly.TryParse(mes + "-01", out var mesDate))
                {
                    var mesEnd = mesDate.AddMonths(1).AddDays(-1);
                    query = query.Where(x =>
                        x.PlannedStartDate <= mesEnd && x.PlannedEndDate >= mesDate);
                }
            }

            var activities = await query.ToListAsync();

            // ── Classify by status ──
            // Active=false + State=true → Culminada (completed)
            // PlannedEndDate < today && Active → Vencida
            // PlannedStartDate <= today && PlannedEndDate >= today && Active → En Proceso
            // PlannedStartDate > today && Active → Pendiente
            var classified = activities.Select(a =>
            {
                string estado;
                if (!a.Active)
                    estado = "Culminada";
                else if (a.PlannedEndDate.HasValue && a.PlannedEndDate.Value < today)
                    estado = "Vencida";
                else if (a.PlannedStartDate.HasValue && a.PlannedStartDate.Value <= today
                      && a.PlannedEndDate.HasValue && a.PlannedEndDate.Value >= today)
                    estado = "En Proceso";
                else
                    estado = "Pendiente";

                return new
                {
                    a.MilestoneScheduleId,
                    a.MilestoneId,
                    a.MilestoneDescription,
                    a.PlannedStartDate,
                    a.PlannedEndDate,
                    a.ProjectId,
                    a.ProjectDescription,
                    a.MilestoneScheduleHistoryId,
                    a.CreatedDateTime,
                    Estado = estado,
                };
            }).ToList();

            int total = classified.Count;
            int culminadas = classified.Count(x => x.Estado == "Culminada");
            int enProceso = classified.Count(x => x.Estado == "En Proceso");
            int vencidas = classified.Count(x => x.Estado == "Vencida");
            int pendientes = classified.Count(x => x.Estado == "Pendiente");
            double eficiencia = total > 0 ? Math.Round((double)culminadas / total * 100, 1) : 0;
            double progreso = total > 0
                ? Math.Round((double)(culminadas + enProceso) / total * 100, 1) : 0;

            // ── KPIs ──
            var kpis = new ArqComercialKpiDTO
            {
                TotalActividades = total,
                Culminadas = culminadas,
                EnProceso = enProceso,
                Vencidas = vencidas,
                Pendientes = pendientes,
                EficienciaMedia = eficiencia,
                ProgresoGlobal = progreso,
            };

            // ── Alerts ──
            int vencidasSinCerrar = classified.Count(x => x.Estado == "Vencida");
            int vencenEstaSemana = classified.Count(x =>
                x.Estado != "Culminada"
                && x.PlannedEndDate.HasValue
                && x.PlannedEndDate.Value >= startOfWeek
                && x.PlannedEndDate.Value <= endOfWeek);
            int arrancanEstaSemana = classified.Count(x =>
                x.PlannedStartDate.HasValue
                && x.PlannedStartDate.Value >= startOfWeek
                && x.PlannedStartDate.Value <= endOfWeek);

            // Hitos próximos 14 días
            var in14Days = today.AddDays(14);
            int hitosProximos = classified.Count(x =>
                x.Estado != "Culminada"
                && x.PlannedEndDate.HasValue
                && x.PlannedEndDate.Value >= today
                && x.PlannedEndDate.Value <= in14Days);

            var alertas = new ArqComercialAlertDTO
            {
                VencidasSinCerrar = vencidasSinCerrar,
                VencenEstaSemana = vencenEstaSemana,
                ArrancanEstaSemana = arrancanEstaSemana,
                HitosProximos14Dias = hitosProximos,
            };

            // ── Ranking Eficiencia (by project) ──
            var projectGroups = classified
                .GroupBy(x => x.ProjectDescription)
                .Select(g => new
                {
                    Label = g.Key,
                    Total = g.Count(),
                    Completed = g.Count(x => x.Estado == "Culminada"),
                })
                .OrderByDescending(x => x.Total)
                .Take(10)
                .ToList();

            var rankingEficiencia = projectGroups
                .Where(x => x.Total > 0)
                .Select(x => new ChartItemDTO
                {
                    Label = x.Label,
                    Value = Math.Round((double)x.Completed / x.Total * 100, 1),
                })
                .OrderByDescending(x => x.Value)
                .ToList();

            // ── Distribución por Estado ──
            var distribucionEstado = new List<ChartItemDTO>
            {
                new() { Label = "Culminadas", Value = culminadas },
                new() { Label = "En Proceso", Value = enProceso },
                new() { Label = "Vencidas", Value = vencidas },
                new() { Label = "Pendientes", Value = pendientes },
            };

            // ── Tendencia Eficiencia últimas 5 semanas ──
            var tendenciaEficiencia = new List<EficienciaSemanalDTO>();
            for (int i = 4; i >= 0; i--)
            {
                var weekStart = startOfWeek.AddDays(-7 * i);
                var weekEnd = weekStart.AddDays(6);

                var weekActivities = classified.Where(x =>
                    x.PlannedEndDate.HasValue && x.PlannedEndDate.Value <= weekEnd).ToList();

                int weekTotal = weekActivities.Count;
                int weekCompleted = weekActivities.Count(x => x.Estado == "Culminada");
                double weekEff = weekTotal > 0
                    ? Math.Round((double)weekCompleted / weekTotal * 100, 1) : 0;

                tendenciaEficiencia.Add(new EficienciaSemanalDTO
                {
                    Semana = $"S{weekStart:dd/MM}",
                    Valor = weekEff,
                });
            }

            // ── Supervisores (ProjectResident → User → Person) ──
            var projectIds = classified.Select(x => x.ProjectId).Distinct().ToList();

            var supervisorData = await (
                from pr in ctx.ProjectResident
                join u in ctx.User on pr.UserId equals u.UserId
                join pe in ctx.Person on u.UserId equals pe.UserId
                where projectIds.Contains(pr.ProjectId) && pr.Active && pr.State && pe.State
                select new
                {
                    pr.ProjectId,
                    pe.FullName,
                }
            ).ToListAsync();

            var supervisorGroups = supervisorData
                .GroupBy(x => x.FullName ?? "Sin nombre")
                .Select(g =>
                {
                    var supProjectIds = g.Select(x => x.ProjectId).Distinct().ToList();
                    var supActivities = classified
                        .Where(x => supProjectIds.Contains(x.ProjectId)).ToList();
                    int supTotal = supActivities.Count;
                    int supCompleted = supActivities.Count(x => x.Estado == "Culminada");
                    int supInProgress = supActivities.Count(x => x.Estado == "En Proceso");

                    return new
                    {
                        Nombre = g.Key,
                        Total = supTotal,
                        Completadas = supCompleted,
                        EnProceso = supInProgress,
                    };
                })
                .Where(x => x.Total > 0)
                .ToList();

            var supervisores = supervisorGroups
                .Select(x => new SupervisorProgresoDTO
                {
                    Nombre = x.Nombre,
                    Total = x.Total,
                    Completadas = x.Completadas,
                    Progreso = x.Total > 0
                        ? Math.Round((double)x.Completadas / x.Total * 100, 1) : 0,
                })
                .OrderByDescending(x => x.Progreso)
                .Take(10)
                .ToList();

            // ── Proyección de Avance (by supervisor) ──
            var proyeccionGroups = supervisorGroups
                .OrderByDescending(x => x.Total)
                .Take(10)
                .ToList();

            var proyeccionAvance = new ProyeccionAvanceDTO
            {
                Labels = proyeccionGroups.Select(x => x.Nombre).ToList(),
                Programado = proyeccionGroups.Select(x => (double)x.Total).ToList(),
                Real = proyeccionGroups.Select(x => (double)x.Completadas).ToList(),
                Proyeccion = proyeccionGroups
                    .Select(x => (double)(x.Completadas + x.EnProceso)).ToList(),
            };

            // ── Hitos Críticos (próximos 14 días sin completar) ──
            var hitosCriticos = classified
                .Where(x => x.Estado != "Culminada"
                    && x.PlannedEndDate.HasValue
                    && x.PlannedEndDate.Value >= today
                    && x.PlannedEndDate.Value <= in14Days)
                .OrderBy(x => x.PlannedEndDate)
                .Select(x => new HitoCriticoDTO
                {
                    Nombre = x.MilestoneDescription,
                    Proyecto = x.ProjectDescription,
                    FechaLimite = x.PlannedEndDate!.Value.ToString("dd/MM/yyyy"),
                    DiasRestantes = x.PlannedEndDate.Value.DayNumber - today.DayNumber,
                    Estado = x.Estado,
                })
                .ToList();

            return new ArqComercialDashboardDTO
            {
                Kpis = kpis,
                Alertas = alertas,
                ProyeccionAvance = proyeccionAvance,
                RankingEficiencia = rankingEficiencia,
                DistribucionEstado = distribucionEstado,
                TendenciaEficiencia = tendenciaEficiencia,
                Supervisores = supervisores,
                HitosCriticos = hitosCriticos,
            };
        }

        public async Task<ArqComercialFiltersDTO> GetFilters()
        {
            using var ctx = _factory.CreateDbContext();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Semanas: últimas 12 semanas (lunes de cada una)
            var semanas = new List<ArqComercialFilterOptionDTO>();
            var currentMonday = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            for (int i = 0; i < 12; i++)
            {
                var weekStart = currentMonday.AddDays(-7 * i);
                var weekEnd = weekStart.AddDays(6);
                semanas.Add(new ArqComercialFilterOptionDTO
                {
                    Value = weekStart.ToString("yyyy-MM-dd"),
                    Label = $"Sem {weekStart:dd/MM} - {weekEnd:dd/MM}",
                });
            }

            // Meses: últimos 12 meses
            var meses = new List<ArqComercialFilterOptionDTO>();
            for (int i = 0; i < 12; i++)
            {
                var month = today.AddMonths(-i);
                var firstOfMonth = new DateOnly(month.Year, month.Month, 1);
                meses.Add(new ArqComercialFilterOptionDTO
                {
                    Value = firstOfMonth.ToString("yyyy-MM"),
                    Label = firstOfMonth.ToString("MMMM yyyy",
                        System.Globalization.CultureInfo.GetCultureInfo("es-PE")),
                });
            }

            // Proyectos activos
            var proyectos = await ctx.Project
                .Where(p => p.State && p.Active)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new ArqComercialProjectOptionDTO
                {
                    Id = p.ProjectId,
                    Nombre = p.ProjectDescription,
                })
                .ToListAsync();

            return new ArqComercialFiltersDTO
            {
                Semanas = semanas,
                Meses = meses,
                Proyectos = proyectos,
            };
        }
    }
}
