using System.Text.Json;
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

        public async Task<ActividadListResponseDTO> GetActividades(
            int? proyectoId,
            string? tipo,
            int? etapaId,
            string? search,
            bool? soloActivas,
            int pagina,
            int porPagina)
        {
            if (pagina < 1) pagina = 1;
            if (porPagina < 1) porPagina = 100;
            if (porPagina > 500) porPagina = 500;

            using var ctx = _factory.CreateDbContext();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var baseQuery = from a in ctx.AcActividad
                            join p in ctx.Proyecto on a.ProjectId equals p.Id
                            from e in ctx.AcEtapa.Where(x => x.Id == a.EtapaId).DefaultIfEmpty()
                            from w in ctx.Worker.Where(x => x.Id == a.UserId).DefaultIfEmpty()
                            select new
                            {
                                Actividad = a,
                                ProjectNombre = p.Nombre,
                                Encargado1 = p.ResponsableArqCom,
                                EtapaNombre = e != null ? e.Nombre : null,
                                ResponsableNombre = w != null ? w.ApellidoNombre : null,
                            };

            if (proyectoId.HasValue && proyectoId.Value > 0)
                baseQuery = baseQuery.Where(x => x.Actividad.ProjectId == proyectoId.Value);

            if (!string.IsNullOrWhiteSpace(tipo))
                baseQuery = baseQuery.Where(x => x.Actividad.Tipo == tipo);

            if (etapaId.HasValue && etapaId.Value > 0)
                baseQuery = baseQuery.Where(x => x.Actividad.EtapaId == etapaId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                baseQuery = baseQuery.Where(x =>
                    x.Actividad.Nombre != null && x.Actividad.Nombre.ToLower().Contains(s));
            }

            if (soloActivas.HasValue && soloActivas.Value)
                baseQuery = baseQuery.Where(x => x.Actividad.Activo);

            int total = await baseQuery.CountAsync();

            var rows = await baseQuery
                .OrderBy(x => x.Actividad.Indice)
                .ThenBy(x => x.Actividad.Id)
                .Skip((pagina - 1) * porPagina)
                .Take(porPagina)
                .ToListAsync();

            var items = rows.Select(x =>
            {
                var a = x.Actividad;

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

                int? retraso = a.FinProgramado.HasValue
                    ? today.DayNumber - a.FinProgramado.Value.DayNumber
                    : (int?)null;

                return new ActividadListItemDTO
                {
                    Id = a.Id,
                    ProjectId = a.ProjectId,
                    ProjectNombre = x.ProjectNombre,
                    Indice = a.Indice,
                    Nombre = a.Nombre,
                    Tipo = a.Tipo,
                    EtapaId = a.EtapaId,
                    EtapaNombre = x.EtapaNombre,
                    UserId = a.UserId,
                    ResponsableNombre = x.ResponsableNombre,
                    Encargado1 = x.Encargado1,
                    InicioProgramado = a.InicioProgramado,
                    FinProgramado = a.FinProgramado,
                    InicioEfectivo = a.InicioEfectivo,
                    FinEfectivo = a.FinEfectivo,
                    Observaciones = a.Observaciones,
                    Activo = a.Activo,
                    Estado = estado,
                    Retraso = retraso,
                };
            }).ToList();

            return new ActividadListResponseDTO
            {
                Total = total,
                Pagina = pagina,
                PorPagina = porPagina,
                Items = items,
            };
        }

        public async Task<ActividadListItemDTO?> PatchActividad(int id, Dictionary<string, JsonElement> body)
        {
            using var ctx = _factory.CreateDbContext();
            var actividad = await ctx.AcActividad.FirstOrDefaultAsync(a => a.Id == id);
            if (actividad == null) return null;

            var oldInicioProgramado = actividad.InicioProgramado;
            bool inicioProgramadoTouched = false;

            foreach (var kvp in body)
            {
                switch (kvp.Key.ToLowerInvariant())
                {
                    case "inicioprogramado":
                        actividad.InicioProgramado = ParseDateOrNull(kvp.Value);
                        inicioProgramadoTouched = true;
                        break;
                    case "finprogramado":
                        actividad.FinProgramado = ParseDateOrNull(kvp.Value);
                        break;
                    case "inicioefectivo":
                        actividad.InicioEfectivo = ParseDateOrNull(kvp.Value);
                        break;
                    case "finefectivo":
                        actividad.FinEfectivo = ParseDateOrNull(kvp.Value);
                        break;
                    case "userid":
                        actividad.UserId = ParseIntOrNull(kvp.Value);
                        break;
                    case "observaciones":
                        actividad.Observaciones = ParseStringOrNull(kvp.Value);
                        break;
                }
            }

            if (inicioProgramadoTouched)
            {
                bool wasNull = !oldInicioProgramado.HasValue;
                bool isNull = !actividad.InicioProgramado.HasValue;
                if (wasNull && !isNull) actividad.Activo = true;
                else if (!wasNull && isNull) actividad.Activo = false;
            }

            await ctx.SaveChangesAsync();

            return await GetActividadItemById(ctx, id);
        }

        private async Task<ActividadListItemDTO?> GetActividadItemById(AppDbContext ctx, int id)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var row = await (from a in ctx.AcActividad
                             join p in ctx.Proyecto on a.ProjectId equals p.Id
                             from e in ctx.AcEtapa.Where(x => x.Id == a.EtapaId).DefaultIfEmpty()
                             from w in ctx.Worker.Where(x => x.Id == a.UserId).DefaultIfEmpty()
                             where a.Id == id
                             select new
                             {
                                 Actividad = a,
                                 ProjectNombre = p.Nombre,
                                 Encargado1 = p.ResponsableArqCom,
                                 EtapaNombre = e != null ? e.Nombre : null,
                                 ResponsableNombre = w != null ? w.ApellidoNombre : null,
                             }).FirstOrDefaultAsync();

            if (row == null) return null;

            var a = row.Actividad;
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

            int? retraso = a.FinProgramado.HasValue
                ? today.DayNumber - a.FinProgramado.Value.DayNumber
                : (int?)null;

            return new ActividadListItemDTO
            {
                Id = a.Id,
                ProjectId = a.ProjectId,
                ProjectNombre = row.ProjectNombre,
                Indice = a.Indice,
                Nombre = a.Nombre,
                Tipo = a.Tipo,
                EtapaId = a.EtapaId,
                EtapaNombre = row.EtapaNombre,
                UserId = a.UserId,
                ResponsableNombre = row.ResponsableNombre,
                Encargado1 = row.Encargado1,
                InicioProgramado = a.InicioProgramado,
                FinProgramado = a.FinProgramado,
                InicioEfectivo = a.InicioEfectivo,
                FinEfectivo = a.FinEfectivo,
                Observaciones = a.Observaciones,
                Activo = a.Activo,
                Estado = estado,
                Retraso = retraso,
            };
        }

        private static DateOnly? ParseDateOrNull(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Null || el.ValueKind == JsonValueKind.Undefined)
                return null;
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (string.IsNullOrWhiteSpace(s)) return null;
                return DateOnly.Parse(s);
            }
            return null;
        }

        private static int? ParseIntOrNull(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Null || el.ValueKind == JsonValueKind.Undefined)
                return null;
            if (el.ValueKind == JsonValueKind.Number)
                return el.GetInt32();
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (string.IsNullOrWhiteSpace(s)) return null;
                return int.TryParse(s, out var v) ? v : null;
            }
            return null;
        }

        private static string? ParseStringOrNull(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Null || el.ValueKind == JsonValueKind.Undefined)
                return null;
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            return null;
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
