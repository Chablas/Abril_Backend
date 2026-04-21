using Abril_Backend.Application.DTOs.ArquitecturaComercial;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Infrastructure.Repositories
{
    public class ArquitecturaComercialRepository : IArquitecturaComercialRepository
    {
        private const string EstadoCulminado = "CULMINADO";
        private const string EstadoEnProceso = "EN PROCESO";
        private const string EstadoVencido = "VENCIDO";
        private const string EstadoPendiente = "PENDIENTE";
        private const string EstadoVacio = "VACIO";

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
            var in14Days = today.AddDays(14);

            // ── Base query: AcActividad + Project + Person (left join via UserId) ──
            var query = from a in ctx.AcActividad
                        join p in ctx.Project on a.ProjectId equals p.ProjectId
                        from pe in ctx.Person
                            .Where(pe => pe.UserId == a.UserId && pe.State)
                            .DefaultIfEmpty()
                        where a.Activo && p.State
                        select new
                        {
                            a.Id,
                            a.Nombre,
                            a.ProjectId,
                            ProjectDescription = p.ProjectDescription,
                            a.UserId,
                            SupervisorFullName = pe != null ? pe.FullName : null,
                            a.InicioProgramado,
                            a.FinProgramado,
                            a.InicioEfectivo,
                            a.FinEfectivo,
                        };

            // ── Apply filters ──
            if (proyectoId.HasValue && proyectoId.Value > 0)
                query = query.Where(x => x.ProjectId == proyectoId.Value);

            if (!string.IsNullOrEmpty(semana) && DateOnly.TryParse(semana, out var semanaDate))
            {
                var semanaEnd = semanaDate.AddDays(6);
                query = query.Where(x =>
                    x.InicioProgramado <= semanaEnd && x.FinProgramado >= semanaDate);
            }

            if (!string.IsNullOrEmpty(mes) && DateOnly.TryParse(mes + "-01", out var mesDate))
            {
                var mesEnd = mesDate.AddMonths(1).AddDays(-1);
                query = query.Where(x =>
                    x.InicioProgramado <= mesEnd && x.FinProgramado >= mesDate);
            }

            var activities = await query.ToListAsync();

            // ── Compute estado in real-time ──
            var classified = activities.Select(a =>
            {
                string estado;
                if (a.FinEfectivo.HasValue)
                    estado = EstadoCulminado;
                else if (a.InicioEfectivo.HasValue)
                    estado = a.FinProgramado.HasValue && a.FinProgramado.Value < today
                        ? EstadoVencido
                        : EstadoEnProceso;
                else if (a.InicioProgramado.HasValue)
                    estado = EstadoPendiente;
                else
                    estado = EstadoVacio;

                return new
                {
                    a.Id,
                    a.Nombre,
                    a.ProjectId,
                    a.ProjectDescription,
                    a.UserId,
                    a.SupervisorFullName,
                    a.InicioProgramado,
                    a.FinProgramado,
                    a.InicioEfectivo,
                    a.FinEfectivo,
                    Estado = estado,
                };
            }).ToList();

            int total = classified.Count;
            int culminadas = classified.Count(x => x.Estado == EstadoCulminado);
            int enProceso = classified.Count(x => x.Estado == EstadoEnProceso);
            int vencidas = classified.Count(x => x.Estado == EstadoVencido);
            int pendientes = classified.Count(x => x.Estado == EstadoPendiente);
            int vacias = classified.Count(x => x.Estado == EstadoVacio);

            double eficiencia = total > 0
                ? Math.Round((double)culminadas / total * 100, 1) : 0;
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
            int vencenEstaSemana = classified.Count(x =>
                x.Estado != EstadoCulminado
                && x.FinProgramado.HasValue
                && x.FinProgramado.Value >= startOfWeek
                && x.FinProgramado.Value <= endOfWeek);
            int arrancanEstaSemana = classified.Count(x =>
                x.InicioProgramado.HasValue
                && x.InicioProgramado.Value >= startOfWeek
                && x.InicioProgramado.Value <= endOfWeek);
            int hitosProximos = classified.Count(x =>
                x.Estado != EstadoCulminado
                && x.FinProgramado.HasValue
                && x.FinProgramado.Value >= today
                && x.FinProgramado.Value <= in14Days);

            var alertas = new ArqComercialAlertDTO
            {
                VencidasSinCerrar = vencidas,
                VencenEstaSemana = vencenEstaSemana,
                ArrancanEstaSemana = arrancanEstaSemana,
                HitosProximos14Dias = hitosProximos,
            };

            // ── Ranking Eficiencia (by project) ──
            var rankingEficiencia = classified
                .GroupBy(x => x.ProjectDescription)
                .Select(g =>
                {
                    int gTotal = g.Count();
                    int gCompleted = g.Count(x => x.Estado == EstadoCulminado);
                    return new ArqComercialChartItemDTO
                    {
                        Label = g.Key,
                        Value = gTotal > 0
                            ? Math.Round((double)gCompleted / gTotal * 100, 1) : 0,
                    };
                })
                .OrderByDescending(x => x.Value)
                .Take(10)
                .ToList();

            // ── Distribución por Estado ──
            var distribucionEstado = new List<ArqComercialChartItemDTO>
            {
                new() { Label = "Culminadas", Value = culminadas },
                new() { Label = "En Proceso", Value = enProceso },
                new() { Label = "Vencidas", Value = vencidas },
                new() { Label = "Pendientes", Value = pendientes },
                new() { Label = "Vacías", Value = vacias },
            };

            // ── Tendencia Eficiencia últimas 5 semanas ──
            var tendenciaEficiencia = new List<EficienciaSemanalDTO>();
            for (int i = 4; i >= 0; i--)
            {
                var weekStart = startOfWeek.AddDays(-7 * i);
                var weekEnd = weekStart.AddDays(6);

                var weekActivities = classified.Where(x =>
                    x.FinProgramado.HasValue && x.FinProgramado.Value <= weekEnd).ToList();

                int weekTotal = weekActivities.Count;
                int weekCompleted = weekActivities.Count(x => x.Estado == EstadoCulminado);
                double weekEff = weekTotal > 0
                    ? Math.Round((double)weekCompleted / weekTotal * 100, 1) : 0;

                tendenciaEficiencia.Add(new EficienciaSemanalDTO
                {
                    Semana = $"S{weekStart:dd/MM}",
                    Valor = weekEff,
                });
            }

            // ── Supervisores (por Person vía user_id en la actividad) ──
            var supervisorGroups = classified
                .Where(x => !string.IsNullOrEmpty(x.SupervisorFullName))
                .GroupBy(x => x.SupervisorFullName!)
                .Select(g => new
                {
                    Nombre = g.Key,
                    Total = g.Count(),
                    Completadas = g.Count(x => x.Estado == EstadoCulminado),
                    EnProceso = g.Count(x => x.Estado == EstadoEnProceso),
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

            // ── Hitos Críticos (próximos 14 días sin culminar) ──
            var hitosCriticos = classified
                .Where(x => x.Estado != EstadoCulminado
                    && x.FinProgramado.HasValue
                    && x.FinProgramado.Value >= today
                    && x.FinProgramado.Value <= in14Days)
                .OrderBy(x => x.FinProgramado)
                .Select(x => new HitoCriticoDTO
                {
                    Nombre = x.Nombre,
                    Proyecto = x.ProjectDescription,
                    FechaLimite = x.FinProgramado!.Value.ToString("dd/MM/yyyy"),
                    DiasRestantes = x.FinProgramado.Value.DayNumber - today.DayNumber,
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

        public async Task<List<ProyectoConActividadesDTO>> GetProyectosConActividades()
        {
            using var ctx = _factory.CreateDbContext();

            var proyectos = await (
                from p in ctx.Proyecto
                select new ProyectoConActividadesDTO
                {
                    Id = p.Id,
                    Nombre = p.Nombre ?? string.Empty,
                    Estado = p.Estado ?? string.Empty,
                    ResponsableArqCom = p.ResponsableArqCom,
                    TotalActividades = ctx.AcActividad.Count(a => a.ProjectId == p.Id),
                    Activas = ctx.AcActividad.Count(a => a.ProjectId == p.Id && a.Activo),
                }
            ).ToListAsync();

            foreach (var p in proyectos)
            {
                p.SinActividades = p.TotalActividades == 0;
            }

            return proyectos.OrderBy(p => p.Nombre).ToList();
        }

        public async Task<List<SupervisorAcDTO>> GetSupervisoresAc()
        {
            using var ctx = _factory.CreateDbContext();

            return await ctx.Worker
                .Where(w =>
                    w.Estado == "ACTIVO"
                    && (w.Categoria == "Arquitecto Comercial"
                        || (w.Ocupacion != null && EF.Functions.Like(w.Ocupacion, "%Arquitectura%"))))
                .OrderBy(w => w.ApellidoNombre)
                .Select(w => new SupervisorAcDTO
                {
                    Id = w.Id,
                    ApellidoNombre = w.ApellidoNombre ?? string.Empty,
                })
                .ToListAsync();
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
