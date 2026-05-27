using System.Globalization;
using System.Text.Json;
using Abril_Backend.Application.DTOs.ArquitecturaComercial;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
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

            // ── Base query: AcActividad + Proyecto (projects) + Worker (left join via UserId) ──
            var query = from a in ctx.AcActividad
                        join p in ctx.Project on a.ProjectId equals p.ProjectId
                        from w in ctx.Worker
                            .Where(w => w.Id == a.UserId)
                            .DefaultIfEmpty()
                        where a.Activo
                        select new
                        {
                            a.Id,
                            a.Nombre,
                            a.ProjectId,
                            ProjectDescription = p.ProjectDescription ?? string.Empty,
                            a.UserId,
                            SupervisorFullName = w != null ? (w.Person != null ? w.Person.FullName : null) : null,
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
                string estado = ComputeEstado(
                    a.InicioProgramado, a.FinProgramado,
                    a.InicioEfectivo, a.FinEfectivo, today);

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
            int vacias = classified.Count(x => x.Estado == EstadoVacio);

            int vencidas = classified.Count(x =>
                x.FinProgramado.HasValue
                && x.FinProgramado.Value < today
                && !x.FinEfectivo.HasValue
                && x.InicioProgramado.HasValue);

            int pendientes = classified.Count(x =>
                x.InicioProgramado.HasValue
                && !x.InicioEfectivo.HasValue
                && x.FinProgramado.HasValue
                && x.FinProgramado.Value >= today);

            var efPorWorker = classified
                .Where(x => !string.IsNullOrEmpty(x.SupervisorFullName))
                .GroupBy(x => x.SupervisorFullName!)
                .Select(g =>
                {
                    int gTotal = g.Count();
                    int gCulminadas = g.Count(x => x.Estado == EstadoCulminado);
                    return gTotal > 0 ? (double)gCulminadas / gTotal * 100 : 0;
                })
                .ToList();
            double eficiencia = efPorWorker.Count > 0
                ? Math.Round(efPorWorker.Average(), 1) : 0;

            double progreso = total > 0
                ? Math.Round((double)culminadas / total * 100, 1) : 0;

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
                from p in ctx.Project
                from w in ctx.Worker.Where(w => w.Id == p.ResponsableArqComId).DefaultIfEmpty()
                select new ProyectoConActividadesDTO
                {
                    Id = p.ProjectId,
                    Nombre = p.ProjectDescription ?? string.Empty,
                    Estado = p.Estado ?? string.Empty,
                    ResponsableArqComId = p.ResponsableArqComId,
                    ResponsableArqCom = w != null ? (w.Person != null ? w.Person.FullName : null) : p.ResponsableArqCom,
                    TotalActividades = ctx.AcActividad.Count(a => a.ProjectId == p.ProjectId),
                    Activas = ctx.AcActividad.Count(a => a.ProjectId == p.ProjectId && a.Activo),
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
                .Where(w => w.Estado == "ACTIVO" && w.Subarea == "Arquitectura Comercial")
                .OrderBy(w => w.Person != null ? w.Person.FullName : null)
                .Select(w => new SupervisorAcDTO
                {
                    Id = w.Id,
                    ApellidoNombre = (w.Person != null ? w.Person.FullName : null) ?? string.Empty,
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
            int porPagina,
            int? userId,
            bool esUsuarioAc)
        {
            if (pagina < 1) pagina = 1;
            if (porPagina < 1) porPagina = 100;
            if (porPagina > 500) porPagina = 500;

            using var ctx = _factory.CreateDbContext();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var baseQuery = from a in ctx.AcActividad
                            join p in ctx.Project on a.ProjectId equals p.ProjectId
                            from e in ctx.AcEtapa.Where(x => x.Id == a.EtapaId).DefaultIfEmpty()
                            from w in ctx.Worker.Where(x => x.Id == a.UserId).DefaultIfEmpty()
                            from w2 in ctx.Worker.Where(x => x.Id == a.UserId2).DefaultIfEmpty()
                            from c in ctx.AcCategoria.Where(x => x.Id == a.CategoriaId).DefaultIfEmpty()
                            from s in ctx.AcEspecialidad.Where(x => x.Id == a.EspecialidadId).DefaultIfEmpty()
                            select new
                            {
                                Actividad = a,
                                ProjectNombre = p.ProjectDescription,
                                Encargado1 = p.ResponsableArqCom,
                                EtapaNombre = e != null ? e.Nombre : null,
                                ResponsableNombre = w != null ? (w.Person != null ? w.Person.FullName : null) : null,
                                ResponsableNombre2 = w2 != null ? (w2.Person != null ? w2.Person.FullName : null) : null,
                                CategoriaNombre = c != null ? c.Nombre : null,
                                EspecialidadNombre = s != null ? s.Nombre : null,
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

            if (esUsuarioAc && userId.HasValue && userId.Value > 0)
                baseQuery = baseQuery.Where(x => x.Actividad.UserId == userId || x.Actividad.UserId2 == userId);

            int total = await baseQuery.CountAsync();

            var rows = await baseQuery
                .OrderBy(x => x.Actividad.Orden)
                .ThenBy(x => x.Actividad.Id)
                .Skip((pagina - 1) * porPagina)
                .Take(porPagina)
                .ToListAsync();

            var items = rows.Select(x =>
            {
                var a = x.Actividad;

                string estado = ComputeEstado(
                    a.InicioProgramado, a.FinProgramado,
                    a.InicioEfectivo, a.FinEfectivo, today);

                int? retraso = ComputeRetraso(a.FinProgramado, a.FinEfectivo, today);

                return new ActividadListItemDTO
                {
                    Id = a.Id,
                    ProjectId = a.ProjectId,
                    ProjectNombre = x.ProjectNombre,
                    Orden = a.Orden,
                    Spi = a.Spi,
                    Nombre = a.Nombre,
                    PartidaDeControl = a.Tipo,
                    EtapaId = a.EtapaId,
                    EtapaNombre = x.EtapaNombre,
                    CategoriaId = a.CategoriaId,
                    CategoriaNombre = x.CategoriaNombre,
                    EspecialidadId = a.EspecialidadId,
                    EspecialidadNombre = x.EspecialidadNombre,
                    UserId = a.UserId,
                    ResponsableNombre = x.ResponsableNombre,
                    UserId2 = a.UserId2,
                    ResponsableNombre2 = x.ResponsableNombre2,
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
                    case "userid2":
                        actividad.UserId2 = ParseIntOrNull(kvp.Value);
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

            actividad.Spi = CalcularSpi(actividad);

            await ctx.SaveChangesAsync();

            return await GetActividadItemById(ctx, id);
        }

        private async Task<ActividadListItemDTO?> GetActividadItemById(AppDbContext ctx, int id)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var row = await (from a in ctx.AcActividad
                             join p in ctx.Project on a.ProjectId equals p.ProjectId
                             from e in ctx.AcEtapa.Where(x => x.Id == a.EtapaId).DefaultIfEmpty()
                             from w in ctx.Worker.Where(x => x.Id == a.UserId).DefaultIfEmpty()
                             from w2 in ctx.Worker.Where(x => x.Id == a.UserId2).DefaultIfEmpty()
                             from c in ctx.AcCategoria.Where(x => x.Id == a.CategoriaId).DefaultIfEmpty()
                             from s in ctx.AcEspecialidad.Where(x => x.Id == a.EspecialidadId).DefaultIfEmpty()
                             where a.Id == id
                             select new
                             {
                                 Actividad = a,
                                 ProjectNombre = p.ProjectDescription,
                                 Encargado1 = p.ResponsableArqCom,
                                 EtapaNombre = e != null ? e.Nombre : null,
                                 ResponsableNombre = w != null ? (w.Person != null ? w.Person.FullName : null) : null,
                                 ResponsableNombre2 = w2 != null ? (w2.Person != null ? w2.Person.FullName : null) : null,
                                 CategoriaNombre = c != null ? c.Nombre : null,
                                 EspecialidadNombre = s != null ? s.Nombre : null,
                             }).FirstOrDefaultAsync();

            if (row == null) return null;

            var act = row.Actividad;
            string estado = ComputeEstado(
                act.InicioProgramado, act.FinProgramado,
                act.InicioEfectivo, act.FinEfectivo, today);

            int? retraso = ComputeRetraso(act.FinProgramado, act.FinEfectivo, today);

            return new ActividadListItemDTO
            {
                Id = act.Id,
                ProjectId = act.ProjectId,
                ProjectNombre = row.ProjectNombre,
                Orden = act.Orden,
                Spi = act.Spi,
                Nombre = act.Nombre,
                PartidaDeControl = act.Tipo,
                EtapaId = act.EtapaId,
                EtapaNombre = row.EtapaNombre,
                CategoriaId = act.CategoriaId,
                CategoriaNombre = row.CategoriaNombre,
                EspecialidadId = act.EspecialidadId,
                EspecialidadNombre = row.EspecialidadNombre,
                UserId = act.UserId,
                ResponsableNombre = row.ResponsableNombre,
                UserId2 = act.UserId2,
                ResponsableNombre2 = row.ResponsableNombre2,
                Encargado1 = row.Encargado1,
                InicioProgramado = act.InicioProgramado,
                FinProgramado = act.FinProgramado,
                InicioEfectivo = act.InicioEfectivo,
                FinEfectivo = act.FinEfectivo,
                Observaciones = act.Observaciones,
                Activo = act.Activo,
                Estado = estado,
                Retraso = retraso,
            };
        }

        public async Task<List<PlantillaActividadDTO>> GetPlantilla()
        {
            using var ctx = _factory.CreateDbContext();

            return await (
                from p in ctx.AcActividadPlantilla
                from e in ctx.AcEtapa.Where(x => x.Id == p.EtapaId).DefaultIfEmpty()
                from c in ctx.AcCategoria.Where(x => x.Id == p.CategoriaId).DefaultIfEmpty()
                from s in ctx.AcEspecialidad.Where(x => x.Id == p.EspecialidadId).DefaultIfEmpty()
                orderby p.Orden, p.Id
                select new PlantillaActividadDTO
                {
                    Id = p.Id,
                    Orden = p.Orden,
                    Nombre = p.Nombre,
                    Tipo = p.Tipo,
                    EtapaId = p.EtapaId,
                    EtapaNombre = e != null ? e.Nombre : null,
                    CategoriaId = p.CategoriaId,
                    CategoriaNombre = c != null ? c.Nombre : null,
                    EspecialidadId = p.EspecialidadId,
                    EspecialidadNombre = s != null ? s.Nombre : null,
                    Activo = p.Activo,
                }
            ).ToListAsync();
        }

        public async Task<PlantillaActividadDTO> CreatePlantilla(CreatePlantillaDTO body)
        {
            if (string.IsNullOrWhiteSpace(body.Nombre))
                throw new AbrilException("El nombre es requerido.", 400);

            using var ctx = _factory.CreateDbContext();

            var plantilla = new AcActividadPlantilla
            {
                Nombre = body.Nombre.Trim(),
                Tipo = string.IsNullOrWhiteSpace(body.Tipo) ? null : body.Tipo.Trim(),
                EtapaId = body.EtapaId,
                CategoriaId = body.CategoriaId,
                EspecialidadId = body.EspecialidadId,
                Orden = body.Orden,
                Activo = body.Activo ?? true,
            };

            ctx.AcActividadPlantilla.Add(plantilla);
            await ctx.SaveChangesAsync();

            return await LoadPlantillaDto(ctx, plantilla.Id)
                ?? throw new InvalidOperationException("No se pudo releer la plantilla recien creada.");
        }

        public async Task<PlantillaActividadDTO?> PatchPlantilla(int id, Dictionary<string, JsonElement> body)
        {
            using var ctx = _factory.CreateDbContext();
            var plantilla = await ctx.AcActividadPlantilla.FirstOrDefaultAsync(p => p.Id == id);
            if (plantilla == null) return null;

            foreach (var kvp in body)
            {
                switch (kvp.Key.ToLowerInvariant())
                {
                    case "nombre":
                        var nombre = kvp.Value.ValueKind == JsonValueKind.String ? kvp.Value.GetString() : null;
                        if (string.IsNullOrWhiteSpace(nombre))
                            throw new AbrilException("El nombre no puede quedar vacio.", 400);
                        plantilla.Nombre = nombre.Trim();
                        break;
                    case "tipo":
                        plantilla.Tipo = kvp.Value.ValueKind == JsonValueKind.String
                            ? (string.IsNullOrWhiteSpace(kvp.Value.GetString()) ? null : kvp.Value.GetString()!.Trim())
                            : null;
                        break;
                    case "etapaid":
                        plantilla.EtapaId = ParseIntOrNull(kvp.Value);
                        break;
                    case "categoriaid":
                        plantilla.CategoriaId = ParseIntOrNull(kvp.Value);
                        break;
                    case "especialidadid":
                        plantilla.EspecialidadId = ParseIntOrNull(kvp.Value);
                        break;
                    case "orden":
                        plantilla.Orden = ParseIntOrNull(kvp.Value);
                        break;
                    case "activo":
                        plantilla.Activo = kvp.Value.ValueKind == JsonValueKind.True
                            || (kvp.Value.ValueKind == JsonValueKind.String && bool.TryParse(kvp.Value.GetString(), out var b) && b);
                        break;
                }
            }

            await ctx.SaveChangesAsync();
            return await LoadPlantillaDto(ctx, plantilla.Id);
        }

        public async Task<List<AcCategoriaDTO>> GetCategorias()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.AcCategoria
                .OrderBy(x => x.Nombre)
                .Select(x => new AcCategoriaDTO { Id = x.Id, Nombre = x.Nombre })
                .ToListAsync();
        }

        public async Task<List<AcEspecialidadDTO>> GetEspecialidades()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.AcEspecialidad
                .OrderBy(x => x.Nombre)
                .Select(x => new AcEspecialidadDTO { Id = x.Id, Nombre = x.Nombre })
                .ToListAsync();
        }

        public async Task<List<AcEtapaDTO>> GetEtapas()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.AcEtapa
                .OrderBy(x => x.Id)
                .Select(x => new AcEtapaDTO { Id = x.Id, Nombre = x.Nombre })
                .ToListAsync();
        }

        public async Task<ActividadListItemDTO> CreateActividad(AcActividadCreateDTO dto)
        {
            using var ctx = _factory.CreateDbContext();

            var proyectoExiste = await ctx.Project.AnyAsync(p => p.ProjectId == dto.ProjectId);
            if (!proyectoExiste)
                throw new AbrilException("Proyecto no encontrado.", 404);

            var maxOrden = await ctx.AcActividad
                .Where(a => a.ProjectId == dto.ProjectId)
                .MaxAsync(a => (int?)a.Orden) ?? 0;

            var actividad = new AcActividad
            {
                ProjectId = dto.ProjectId,
                UserId = dto.UserId,
                UserId2 = dto.UserId2,
                Nombre = dto.Nombre,
                Tipo = dto.Tipo,
                EtapaId = dto.EtapaId,
                CategoriaId = dto.CategoriaId,
                EspecialidadId = dto.EspecialidadId,
                Estado = EstadoVacio,
                Activo = true,
                Orden = maxOrden + 1,
                InicioProgramado = dto.InicioProgramado,
                FinProgramado = dto.FinProgramado,
                Observaciones = dto.Observaciones,
            };

            ctx.AcActividad.Add(actividad);
            await ctx.SaveChangesAsync();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var row = await (from a in ctx.AcActividad
                             join p in ctx.Project on a.ProjectId equals p.ProjectId
                             from e in ctx.AcEtapa.Where(x => x.Id == a.EtapaId).DefaultIfEmpty()
                             from w in ctx.Worker.Where(x => x.Id == a.UserId).DefaultIfEmpty()
                             from w2 in ctx.Worker.Where(x => x.Id == a.UserId2).DefaultIfEmpty()
                             from c in ctx.AcCategoria.Where(x => x.Id == a.CategoriaId).DefaultIfEmpty()
                             from s in ctx.AcEspecialidad.Where(x => x.Id == a.EspecialidadId).DefaultIfEmpty()
                             where a.Id == actividad.Id
                             select new
                             {
                                 Actividad = a,
                                 ProjectNombre = p.ProjectDescription,
                                 Encargado1 = p.ResponsableArqCom,
                                 EtapaNombre = e != null ? e.Nombre : null,
                                 ResponsableNombre = w != null ? (w.Person != null ? w.Person.FullName : null) : null,
                                 ResponsableNombre2 = w2 != null ? (w2.Person != null ? w2.Person.FullName : null) : null,
                                 CategoriaNombre = c != null ? c.Nombre : null,
                                 EspecialidadNombre = s != null ? s.Nombre : null,
                             }).FirstAsync();

            var act = row.Actividad;
            return new ActividadListItemDTO
            {
                Id = act.Id,
                ProjectId = act.ProjectId,
                ProjectNombre = row.ProjectNombre,
                Orden = act.Orden,
                Spi = act.Spi,
                Nombre = act.Nombre,
                PartidaDeControl = act.Tipo,
                EtapaId = act.EtapaId,
                EtapaNombre = row.EtapaNombre,
                CategoriaId = act.CategoriaId,
                CategoriaNombre = row.CategoriaNombre,
                EspecialidadId = act.EspecialidadId,
                EspecialidadNombre = row.EspecialidadNombre,
                UserId = act.UserId,
                ResponsableNombre = row.ResponsableNombre,
                UserId2 = act.UserId2,
                ResponsableNombre2 = row.ResponsableNombre2,
                Encargado1 = row.Encargado1,
                InicioProgramado = act.InicioProgramado,
                FinProgramado = act.FinProgramado,
                InicioEfectivo = act.InicioEfectivo,
                FinEfectivo = act.FinEfectivo,
                Observaciones = act.Observaciones,
                Activo = act.Activo,
                Estado = ComputeEstado(act.InicioProgramado, act.FinProgramado, act.InicioEfectivo, act.FinEfectivo, today),
                Retraso = ComputeRetraso(act.FinProgramado, act.FinEfectivo, today),
            };
        }

        public async Task<ActividadListItemDTO> UpdateActividad(int id, AcActividadUpdateDTO dto)
        {
            using var ctx = _factory.CreateDbContext();

            var actividad = await ctx.AcActividad.FirstOrDefaultAsync(a => a.Id == id)
                ?? throw new AbrilException("Actividad no encontrada.", 404);

            actividad.Nombre = dto.Nombre;
            actividad.Tipo = dto.Tipo;
            actividad.EtapaId = dto.EtapaId;
            actividad.CategoriaId = dto.CategoriaId;
            actividad.EspecialidadId = dto.EspecialidadId;
            actividad.UserId = dto.UserId;
            actividad.UserId2 = dto.UserId2;
            actividad.InicioProgramado = dto.InicioProgramado;
            actividad.FinProgramado = dto.FinProgramado;
            actividad.InicioEfectivo = dto.InicioEfectivo;
            actividad.FinEfectivo = dto.FinEfectivo;
            actividad.Observaciones = dto.Observaciones;
            actividad.Spi = CalcularSpi(actividad);

            await ctx.SaveChangesAsync();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var row = await (from a in ctx.AcActividad
                             join p in ctx.Project on a.ProjectId equals p.ProjectId
                             from e in ctx.AcEtapa.Where(x => x.Id == a.EtapaId).DefaultIfEmpty()
                             from w in ctx.Worker.Where(x => x.Id == a.UserId).DefaultIfEmpty()
                             from w2 in ctx.Worker.Where(x => x.Id == a.UserId2).DefaultIfEmpty()
                             from c in ctx.AcCategoria.Where(x => x.Id == a.CategoriaId).DefaultIfEmpty()
                             from s in ctx.AcEspecialidad.Where(x => x.Id == a.EspecialidadId).DefaultIfEmpty()
                             where a.Id == id
                             select new
                             {
                                 Actividad = a,
                                 ProjectNombre = p.ProjectDescription,
                                 Encargado1 = p.ResponsableArqCom,
                                 EtapaNombre = e != null ? e.Nombre : null,
                                 ResponsableNombre = w != null ? (w.Person != null ? w.Person.FullName : null) : null,
                                 ResponsableNombre2 = w2 != null ? (w2.Person != null ? w2.Person.FullName : null) : null,
                                 CategoriaNombre = c != null ? c.Nombre : null,
                                 EspecialidadNombre = s != null ? s.Nombre : null,
                             }).FirstAsync();

            var act = row.Actividad;
            return new ActividadListItemDTO
            {
                Id = act.Id,
                ProjectId = act.ProjectId,
                ProjectNombre = row.ProjectNombre,
                Orden = act.Orden,
                Spi = act.Spi,
                Nombre = act.Nombre,
                PartidaDeControl = act.Tipo,
                EtapaId = act.EtapaId,
                EtapaNombre = row.EtapaNombre,
                CategoriaId = act.CategoriaId,
                CategoriaNombre = row.CategoriaNombre,
                EspecialidadId = act.EspecialidadId,
                EspecialidadNombre = row.EspecialidadNombre,
                UserId = act.UserId,
                ResponsableNombre = row.ResponsableNombre,
                UserId2 = act.UserId2,
                ResponsableNombre2 = row.ResponsableNombre2,
                Encargado1 = row.Encargado1,
                InicioProgramado = act.InicioProgramado,
                FinProgramado = act.FinProgramado,
                InicioEfectivo = act.InicioEfectivo,
                FinEfectivo = act.FinEfectivo,
                Observaciones = act.Observaciones,
                Activo = act.Activo,
                Estado = ComputeEstado(act.InicioProgramado, act.FinProgramado, act.InicioEfectivo, act.FinEfectivo, today),
                Retraso = ComputeRetraso(act.FinProgramado, act.FinEfectivo, today),
            };
        }

        public async Task DeleteActividad(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var actividad = await ctx.AcActividad.FirstOrDefaultAsync(a => a.Id == id)
                ?? throw new AbrilException("Actividad no encontrada.", 404);

            ctx.AcActividad.Remove(actividad);
            await ctx.SaveChangesAsync();
        }

        public async Task<AvanceSemanalSnapshotResultDTO> SnapshotAvanceSemanal()
        {
            using var ctx = _factory.CreateDbContext();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var semana = today.AddDays(-(int)today.DayOfWeek + 1);

            var actividades = await ctx.AcActividad
                .Where(a => a.Activo)
                .ToListAsync();

            var existentes = await ctx.AcAvanceSemanal
                .Where(x => x.Semana == semana)
                .ToDictionaryAsync(x => x.ActividadId);

            foreach (var a in actividades)
            {
                var spi = CalcularSpi(a);
                var porcentaje = CalcularPorcentajeAvance(a, today);

                if (existentes.TryGetValue(a.Id, out var row))
                {
                    row.PorcentajeAvance = porcentaje;
                    row.Spi = spi;
                }
                else
                {
                    ctx.AcAvanceSemanal.Add(new AcAvanceSemanal
                    {
                        ActividadId = a.Id,
                        Semana = semana,
                        PorcentajeAvance = porcentaje,
                        Spi = spi,
                        CreatedAt = DateTime.UtcNow,
                    });
                }
            }

            await ctx.SaveChangesAsync();

            return new AvanceSemanalSnapshotResultDTO
            {
                Total = actividades.Count,
                Semana = semana,
                Message = $"Snapshot generado para la semana del {semana:yyyy-MM-dd}.",
            };
        }

        private static async Task<PlantillaActividadDTO?> LoadPlantillaDto(AppDbContext ctx, int id)
        {
            return await (
                from p in ctx.AcActividadPlantilla
                where p.Id == id
                from e in ctx.AcEtapa.Where(x => x.Id == p.EtapaId).DefaultIfEmpty()
                from c in ctx.AcCategoria.Where(x => x.Id == p.CategoriaId).DefaultIfEmpty()
                from s in ctx.AcEspecialidad.Where(x => x.Id == p.EspecialidadId).DefaultIfEmpty()
                select new PlantillaActividadDTO
                {
                    Id = p.Id,
                    Orden = p.Orden,
                    Nombre = p.Nombre,
                    Tipo = p.Tipo,
                    EtapaId = p.EtapaId,
                    EtapaNombre = e != null ? e.Nombre : null,
                    CategoriaId = p.CategoriaId,
                    CategoriaNombre = c != null ? c.Nombre : null,
                    EspecialidadId = p.EspecialidadId,
                    EspecialidadNombre = s != null ? s.Nombre : null,
                    Activo = p.Activo,
                }
            ).FirstOrDefaultAsync();
        }

        private static decimal CalcularSpi(AcActividad a)
        {
            if (!a.InicioProgramado.HasValue)
                return 0m;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            if (a.FinEfectivo.HasValue)
            {
                var diasPlan = (a.FinProgramado!.Value.ToDateTime(TimeOnly.MinValue)
                              - a.InicioProgramado.Value.ToDateTime(TimeOnly.MinValue)).TotalDays;
                if (diasPlan == 0) return 0m;
                var diasReal = (a.FinEfectivo.Value.ToDateTime(TimeOnly.MinValue)
                              - (a.InicioEfectivo ?? a.InicioProgramado.Value).ToDateTime(TimeOnly.MinValue)).TotalDays;
                if (diasReal == 0) return 0m;
                return Math.Round((decimal)(diasPlan / diasReal), 2);
            }

            if (a.InicioEfectivo.HasValue)
            {
                var diasPlan = (a.FinProgramado!.Value.ToDateTime(TimeOnly.MinValue)
                              - a.InicioProgramado.Value.ToDateTime(TimeOnly.MinValue)).TotalDays;
                if (diasPlan == 0) return 0m;
                var diasTranscurridos = (today.ToDateTime(TimeOnly.MinValue)
                                       - a.InicioEfectivo.Value.ToDateTime(TimeOnly.MinValue)).TotalDays;
                return Math.Round((decimal)(diasTranscurridos / diasPlan), 2);
            }

            return 0m;
        }

        private static decimal CalcularPorcentajeAvance(AcActividad a, DateOnly today)
        {
            if (!a.InicioProgramado.HasValue)
                return 0m;

            if (a.FinEfectivo.HasValue)
                return 100m;

            if (a.InicioEfectivo.HasValue && a.FinProgramado.HasValue)
            {
                var transcurridos = (today.ToDateTime(TimeOnly.MinValue)
                                   - a.InicioEfectivo.Value.ToDateTime(TimeOnly.MinValue)).TotalDays;
                var planificados  = (a.FinProgramado.Value.ToDateTime(TimeOnly.MinValue)
                                   - a.InicioEfectivo.Value.ToDateTime(TimeOnly.MinValue)).TotalDays;
                if (planificados == 0) return 0m;
                return Math.Min(99m, Math.Max(0m, Math.Round((decimal)(transcurridos / planificados * 100), 2)));
            }

            return 0m;
        }

        private static int? ComputeRetraso(DateOnly? finProgramado, DateOnly? finEfectivo, DateOnly today)
        {
            if (!finProgramado.HasValue) return null;
            if (finEfectivo.HasValue)
                return finEfectivo.Value.DayNumber - finProgramado.Value.DayNumber;
            if (finProgramado.Value < today)
                return today.DayNumber - finProgramado.Value.DayNumber;
            return null;
        }

        private static string ComputeEstado(
            DateOnly? inicioProgramado,
            DateOnly? finProgramado,
            DateOnly? inicioEfectivo,
            DateOnly? finEfectivo,
            DateOnly today)
        {
            if (finEfectivo.HasValue)
                return EstadoCulminado;

            if (finProgramado.HasValue
                && finProgramado.Value < today
                && inicioProgramado.HasValue)
                return EstadoVencido;

            if (inicioEfectivo.HasValue)
                return EstadoEnProceso;

            if (inicioProgramado.HasValue)
                return EstadoPendiente;

            return EstadoVacio;
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

        public async Task<ReasignarEncargadoResultDTO?> ReasignarEncargado(int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var proyecto = await ctx.Project.FirstOrDefaultAsync(p => p.ProjectId == proyectoId);
            if (proyecto == null) return null;

            var workerId = proyecto.ResponsableArqComId;
            if (workerId == null)
                return new ReasignarEncargadoResultDTO { Actualizadas = 0, WorkerNoEncontrado = true };

            var existeWorker = await ctx.Worker.AnyAsync(w => w.Id == workerId);
            if (!existeWorker)
                return new ReasignarEncargadoResultDTO { Actualizadas = 0, WorkerNoEncontrado = true };

            var actualizadas = await ctx.AcActividad
                .Where(a => a.ProjectId == proyectoId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.UserId, workerId));

            return new ReasignarEncargadoResultDTO
            {
                Actualizadas = actualizadas,
                WorkerNoEncontrado = false,
            };
        }

        public async Task<GenerarActividadesResultDTO?> GenerarActividades(int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var proyectoExiste = await ctx.Project.AnyAsync(p => p.ProjectId == proyectoId);
            if (!proyectoExiste) return null;

            var yaTiene = await ctx.AcActividad.AnyAsync(a => a.ProjectId == proyectoId);
            if (yaTiene)
                throw new AbrilException("El proyecto ya tiene actividades.", 409);

            var plantilla = await ctx.AcActividadPlantilla
                .Where(p => p.Activo)
                .OrderBy(p => p.Orden)
                .ToListAsync();

            if (plantilla.Count == 0)
                return new GenerarActividadesResultDTO { Generadas = 0 };

            var nuevas = plantilla.Select(p => new AcActividad
            {
                ProjectId = proyectoId,
                Nombre = p.Nombre,
                Tipo = p.Tipo,
                EtapaId = p.EtapaId,
                Orden = p.Orden,
                Activo = false,
            }).ToList();

            ctx.AcActividad.AddRange(nuevas);
            await ctx.SaveChangesAsync();

            return new GenerarActividadesResultDTO { Generadas = nuevas.Count };
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

            // Proyectos (todos — frontend los marca visualmente por estado)
            var proyectos = await ctx.Project
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new ArqComercialProjectOptionDTO
                {
                    Id = p.ProjectId,
                    Nombre = p.ProjectDescription ?? string.Empty,
                })
                .ToListAsync();

            return new ArqComercialFiltersDTO
            {
                Semanas = semanas,
                Meses = meses,
                Proyectos = proyectos,
            };
        }

        public async Task<List<GanttActividadDTO>> GetGantt(int? proyectoId, string? tipo, string? etapa, bool? soloActivas)
        {
            using var ctx = _factory.CreateDbContext();

            var query = from a in ctx.AcActividad
                        from e in ctx.AcEtapa.Where(x => x.Id == a.EtapaId).DefaultIfEmpty()
                        where a.InicioProgramado != null
                        select new GanttActividadDTO
                        {
                            Id = a.Id,
                            Orden = a.Orden,
                            Nombre = a.Nombre,
                            Tipo = a.Tipo,
                            EtapaId = a.EtapaId,
                            EtapaNombre = e != null ? e.Nombre : null,
                            Activo = a.Activo,
                            InicioProgramado = a.InicioProgramado,
                            FinProgramado = a.FinProgramado,
                            InicioEfectivo = a.InicioEfectivo,
                            FinEfectivo = a.FinEfectivo,
                            ProjectId = a.ProjectId,
                        };

            if (proyectoId.HasValue && proyectoId.Value > 0)
                query = query.Where(x => x.ProjectId == proyectoId.Value);

            if (!string.IsNullOrWhiteSpace(tipo))
                query = query.Where(x => x.Tipo == tipo);

            if (!string.IsNullOrWhiteSpace(etapa))
                query = query.Where(x => x.EtapaNombre == etapa);

            if (soloActivas.HasValue && soloActivas.Value)
                query = query.Where(x => x.Activo);

            return await query
                .OrderBy(x => x.Orden)
                .ThenBy(x => x.Id)
                .ToListAsync();
        }

        public async Task<ProyectoConActividadesDTO?> PatchProyecto(int id, PatchProyectoDTO body)
        {
            using var ctx = _factory.CreateDbContext();

            var proyecto = await ctx.Project.FirstOrDefaultAsync(p => p.ProjectId == id);
            if (proyecto == null) return null;

            string? nombreResuelto = null;
            if (body.ResponsableArqComId.HasValue)
            {
                nombreResuelto = await ctx.Worker
                    .Where(w => w.Id == body.ResponsableArqComId.Value)
                    .Select(w => w.Person != null ? w.Person.FullName : null)
                    .FirstOrDefaultAsync();

                if (nombreResuelto == null)
                    throw new AbrilException("Worker no encontrado.", 404);
            }

            proyecto.ResponsableArqComId = body.ResponsableArqComId;
            proyecto.ResponsableArqCom = nombreResuelto;

            await ctx.SaveChangesAsync();

            var total = await ctx.AcActividad.CountAsync(a => a.ProjectId == id);
            var activas = await ctx.AcActividad.CountAsync(a => a.ProjectId == id && a.Activo);

            return new ProyectoConActividadesDTO
            {
                Id = proyecto.ProjectId,
                Nombre = proyecto.ProjectDescription ?? string.Empty,
                Estado = proyecto.Estado ?? string.Empty,
                ResponsableArqComId = proyecto.ResponsableArqComId,
                ResponsableArqCom = proyecto.ResponsableArqCom,
                TotalActividades = total,
                Activas = activas,
                SinActividades = total == 0,
            };
        }

        public async Task<ArqComercialDashboardDTO> GetDashboardDataFiltrado(DashboardFiltroDTO filtro)
        {
            using var ctx = _factory.CreateDbContext();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var query = ctx.AcActividad.Where(a => a.Activo).AsQueryable();

            if (filtro.CategoriaId.HasValue)
                query = query.Where(a => a.CategoriaId == filtro.CategoriaId.Value);
            if (filtro.ProyectoId.HasValue && filtro.ProyectoId.Value > 0)
                query = query.Where(a => a.ProjectId == filtro.ProyectoId.Value);
            if (filtro.UserId.HasValue && filtro.UserId.Value > 0)
                query = query.Where(a => a.UserId == filtro.UserId.Value || a.UserId2 == filtro.UserId.Value);
            if (filtro.Mes.HasValue)
                query = query.Where(a =>
                    (a.InicioProgramado.HasValue && a.InicioProgramado.Value.Month == filtro.Mes.Value) ||
                    (a.FinProgramado.HasValue    && a.FinProgramado.Value.Month    == filtro.Mes.Value));
            if (filtro.Semana.HasValue)
            {
                var anio  = filtro.Anio ?? today.Year;
                var jan1  = new DateOnly(anio, 1, 1);
                var dow   = (int)jan1.DayOfWeek;
                var offset = dow == 0 ? 6 : dow - 1;
                var semanaInicio = jan1.AddDays((filtro.Semana.Value - 1) * 7 - offset);
                var semanaFin    = semanaInicio.AddDays(6);
                query = query.Where(a =>
                    (a.InicioProgramado >= semanaInicio && a.InicioProgramado <= semanaFin) ||
                    (a.FinProgramado    >= semanaInicio && a.FinProgramado    <= semanaFin));
            }

            var actividades = await query.ToListAsync();
            var total       = actividades.Count;
            var culminadas  = actividades.Count(a => a.FinEfectivo != null);
            var enProceso   = actividades.Count(a => a.InicioEfectivo != null && a.FinEfectivo == null);
            var vencidas    = actividades.Count(a => a.FinProgramado.HasValue
                                 && a.FinProgramado.Value < today
                                 && a.FinEfectivo == null);
            var pendientes  = actividades.Count(a => a.InicioProgramado.HasValue
                                 && a.InicioProgramado.Value > today
                                 && a.InicioEfectivo == null);

            var spiVals = actividades.Where(a => a.Spi.HasValue && a.Spi.Value > 0)
                .Select(a => (double)a.Spi!.Value).ToList();
            var eficienciaMedia = spiVals.Count > 0 ? Math.Round(spiVals.Average() * 100, 1) : 0.0;
            var progresoGlobal  = total > 0 ? Math.Round((double)culminadas / total * 100, 1) : 0.0;

            var semLunes   = today.AddDays(today.DayOfWeek == DayOfWeek.Sunday ? -6 : -(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            var semDomingo = semLunes.AddDays(6);

            var alertas = new ArqComercialAlertDTO
            {
                VencidasSinCerrar   = vencidas,
                VencenEstaSemana    = actividades.Count(a =>
                    a.FinEfectivo == null &&
                    a.FinProgramado >= semLunes && a.FinProgramado <= semDomingo),
                ArrancanEstaSemana  = actividades.Count(a =>
                    a.InicioEfectivo == null &&
                    a.InicioProgramado >= semLunes && a.InicioProgramado <= semDomingo),
                HitosProximos14Dias = actividades.Count(a =>
                    a.Tipo == "HITO" && a.FinEfectivo == null &&
                    a.FinProgramado >= today && a.FinProgramado <= today.AddDays(14)),
            };

            var actProjectIds = actividades.Select(a => a.ProjectId).Distinct().ToList();
            var proyectos     = await ctx.Project
                .Where(p => actProjectIds.Contains(p.ProjectId))
                .ToListAsync();
            var proyectoResponsableMap = proyectos
                .Where(p => p.ResponsableArqComId != null)
                .ToDictionary(p => p.ProjectId, p => p.ResponsableArqComId!.Value);

            var workerIds = actividades
                .SelectMany(a =>
                {
                    var resp1 = a.UserId ??
                        (proyectoResponsableMap.TryGetValue(a.ProjectId, out var rid) ? rid : (int?)null);
                    return new[] { resp1, a.UserId2 };
                })
                .Where(id => id.HasValue).Select(id => id!.Value)
                .Distinct().ToList();

            var workers = workerIds.Count > 0
                ? await ctx.Worker.Where(w => workerIds.Contains(w.Id)).Include(w => w.Person).ToListAsync()
                : [];

            var workerNameMap = workers.ToDictionary(w => w.Id, w => w.Person?.FullName ?? $"Worker {w.Id}");

            var tareasPorArquitectoDetalle = workerIds.Select(uid =>
            {
                var tareas = actividades.Where(a =>
                {
                    var resp1 = a.UserId ??
                        (proyectoResponsableMap.TryGetValue(a.ProjectId, out var rid) ? rid : (int?)null);
                    return resp1 == uid || a.UserId2 == uid;
                }).ToList();
                var completadas = tareas.Count(a => a.FinEfectivo != null);
                return new TareasPorArquitectoDTO
                {
                    UserId      = uid,
                    Nombre      = workerNameMap.GetValueOrDefault(uid, $"Worker {uid}"),
                    Hitos       = tareas.Count(a => a.Tipo == "HITO"),
                    Entregables = tareas.Count(a => a.Tipo == "ENTREGABLE"),
                    Consultas   = tareas.Count(a => a.Tipo == "CONSULTA"),
                    Total       = tareas.Count,
                    AvancePct   = tareas.Count > 0 ? Math.Round((decimal)completadas / tareas.Count * 100, 1) : 0m,
                };
            }).OrderByDescending(t => t.Total).ToList();

            var supervisores = tareasPorArquitectoDetalle.Select(t => new SupervisorProgresoDTO
            {
                Nombre      = t.Nombre,
                Progreso    = (double)t.AvancePct,
                Completadas = actividades.Count(a =>
                {
                    var resp1 = a.UserId ??
                        (proyectoResponsableMap.TryGetValue(a.ProjectId, out var rid) ? rid : (int?)null);
                    return (resp1 == t.UserId || a.UserId2 == t.UserId) && a.FinEfectivo != null;
                }),
                Total       = t.Total,
            }).ToList();

            var hitosBase = actividades
                .Where(a => a.Tipo == "HITO" && a.Estado != EstadoCulminado &&
                       a.FinProgramado.HasValue && a.FinProgramado.Value <= today.AddDays(30))
                .OrderBy(a => a.FinProgramado).Take(50).ToList();

            var hitosProjectIds = hitosBase.Select(a => a.ProjectId).Distinct().ToList();
            var hitosProjects   = hitosProjectIds.Count > 0
                ? await ctx.Project.Where(p => hitosProjectIds.Contains(p.ProjectId))
                    .ToDictionaryAsync(p => p.ProjectId, p => p.ProjectDescription ?? "")
                : [];

            var hitosCriticos = hitosBase.Select(a => new HitoCriticoDTO
            {
                Id            = a.Id,
                Nombre        = a.Nombre,
                Proyecto      = hitosProjects.GetValueOrDefault(a.ProjectId, ""),
                Estado        = a.Estado ?? "",
                FechaLimite   = a.FinProgramado?.ToString("dd/MM/yyyy") ?? "",
                DiasRestantes = a.FinProgramado.HasValue ? (a.FinProgramado.Value.DayNumber - today.DayNumber) : 0,
            }).ToList();

            var hace8Semanas = today.AddDays(-56);
            var semanaRaw = await ctx.AcAvanceSemanal
                .Where(s => s.Semana >= hace8Semanas)
                .GroupBy(s => s.Semana)
                .Select(g => new { Semana = g.Key, Real = g.Average(x => x.PorcentajeAvance) })
                .OrderBy(s => s.Semana)
                .ToListAsync();

            var semanas = semanaRaw.Select(s => new AvanceSemanalDTO
            {
                Semana     = $"Sem {ISOWeek.GetWeekOfYear(s.Semana.ToDateTime(TimeOnly.MinValue))}",
                Real       = s.Real,
                Programado = Math.Round(s.Real * 0.9m, 2),
            }).ToList();

            var hace3Semanas = today.AddDays(-21);
            var spiRaw = await ctx.AcAvanceSemanal
                .Where(s => s.Semana >= hace3Semanas)
                .GroupBy(s => s.Semana)
                .Select(g => new { Semana = g.Key, Spi = g.Average(x => x.Spi) })
                .OrderBy(s => s.Semana)
                .ToListAsync();

            var eficienciaSpi = spiRaw.Select(s => new EficienciaSpiDTO
            {
                Semana = $"Sem {ISOWeek.GetWeekOfYear(s.Semana.ToDateTime(TimeOnly.MinValue))}",
                Spi    = s.Spi,
            }).ToList();

            var categorias = await ctx.AcCategoria
                .Select(c => new CategoriaItemDTO { Id = c.Id, Nombre = c.Nombre })
                .ToListAsync();

            return new ArqComercialDashboardDTO
            {
                Kpis = new ArqComercialKpiDTO
                {
                    TotalActividades = total,
                    Culminadas       = culminadas,
                    EnProceso        = enProceso,
                    Vencidas         = vencidas,
                    Pendientes       = pendientes,
                    EficienciaMedia  = eficienciaMedia,
                    ProgresoGlobal   = progresoGlobal,
                },
                Alertas            = alertas,
                Supervisores       = supervisores,
                HitosCriticos      = hitosCriticos,
                DistribucionEstado = new List<ArqComercialChartItemDTO>
                {
                    new() { Label = "Culminadas", Value = culminadas },
                    new() { Label = "En Proceso", Value = enProceso  },
                    new() { Label = "Vencidas",   Value = vencidas   },
                    new() { Label = "Pendientes", Value = pendientes  },
                },
                RankingEficiencia  = tareasPorArquitectoDetalle.Select(t => new ArqComercialChartItemDTO
                {
                    Label = t.Nombre.Split(' ').FirstOrDefault() ?? t.Nombre,
                    Value = (double)t.AvancePct,
                }).ToList(),
                TendenciaEficiencia = eficienciaSpi.Select(e => new EficienciaSemanalDTO
                {
                    Semana = e.Semana,
                    Valor  = (double)e.Spi,
                }).ToList(),
                ProyeccionAvance = new ProyeccionAvanceDTO
                {
                    Labels     = semanas.Select(s => s.Semana).ToList(),
                    Programado = semanas.Select(s => (double)s.Programado).ToList(),
                    Real       = semanas.Select(s => (double)s.Real).ToList(),
                },
                TareasPorArquitectoDetalle = [.. tareasPorArquitectoDetalle],
                AvanceSemanal              = [.. semanas],
                EficienciaSpi              = [.. eficienciaSpi],
                Categorias                 = [.. categorias],
            };
        }

        public async Task<List<ActividadAlertaDTO>> GetActividadesPorAlerta(
            string tipoAlerta, DashboardFiltroDTO filtro)
        {
            using var ctx = _factory.CreateDbContext();
            var today      = DateOnly.FromDateTime(DateTime.UtcNow);
            var semLunes   = today.AddDays(today.DayOfWeek == DayOfWeek.Sunday ? -6 : -(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            var semDomingo = semLunes.AddDays(6);

            var query = ctx.AcActividad.Where(a => a.Activo).AsQueryable();

            if (filtro.CategoriaId.HasValue)
                query = query.Where(a => a.CategoriaId == filtro.CategoriaId.Value);
            if (filtro.ProyectoId.HasValue && filtro.ProyectoId.Value > 0)
                query = query.Where(a => a.ProjectId == filtro.ProyectoId.Value);

            query = tipoAlerta.ToUpperInvariant() switch
            {
                "VENCIDA"      => query.Where(a => a.FinEfectivo == null && a.FinProgramado.HasValue && a.FinProgramado.Value < today),
                "VENCE_SEMANA" => query.Where(a => a.FinEfectivo == null && a.FinProgramado >= semLunes && a.FinProgramado <= semDomingo),
                "ARRANQUE"     => query.Where(a => a.InicioEfectivo == null && a.InicioProgramado >= semLunes && a.InicioProgramado <= semDomingo),
                "HITO_PROXIMO" => query.Where(a => a.Tipo == "HITO" && a.FinEfectivo == null && a.FinProgramado >= today && a.FinProgramado <= today.AddDays(14)),
                _              => query,
            };

            var list = await query.OrderBy(a => a.FinProgramado).Take(200).ToListAsync();
            if (list.Count == 0) return [];

            var projectIds   = list.Select(a => a.ProjectId).Distinct().ToList();
            var proyectoList = await ctx.Project
                .Where(p => projectIds.Contains(p.ProjectId))
                .ToListAsync();
            var projects               = proyectoList.ToDictionary(p => p.ProjectId, p => p.ProjectDescription ?? "");
            var proyectoResponsableMap = proyectoList
                .Where(p => p.ResponsableArqComId != null)
                .ToDictionary(p => p.ProjectId, p => p.ResponsableArqComId!.Value);

            var workerIds = list
                .SelectMany(a =>
                {
                    var resp1 = a.UserId ??
                        (proyectoResponsableMap.TryGetValue(a.ProjectId, out var rid) ? rid : (int?)null);
                    return new[] { resp1, a.UserId2 };
                })
                .Where(id => id.HasValue).Select(id => id!.Value)
                .Distinct().ToList();

            var workers = workerIds.Count > 0
                ? await ctx.Worker.Where(w => workerIds.Contains(w.Id)).Include(w => w.Person).ToListAsync()
                : [];

            var workerNameMap  = workers.ToDictionary(w => w.Id, w => w.Person?.FullName ?? $"Worker {w.Id}");
            var workerEmailMap = workers.ToDictionary(w => w.Id, w => w.EmailPersonal ?? "");

            var categoriaIds = list.Where(a => a.CategoriaId.HasValue).Select(a => a.CategoriaId!.Value).Distinct().ToList();
            var categorias   = categoriaIds.Count > 0
                ? await ctx.AcCategoria.Where(c => categoriaIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nombre)
                : [];

            return list.Select(a =>
            {
                var resp1Id = a.UserId ??
                    (proyectoResponsableMap.TryGetValue(a.ProjectId, out var rid) ? rid : (int?)null);
                return new ActividadAlertaDTO
                {
                    Id            = a.Id,
                    Nombre        = a.Nombre,
                    Proyecto      = projects.GetValueOrDefault(a.ProjectId, ""),
                    Responsable1  = resp1Id.HasValue   ? workerNameMap.GetValueOrDefault(resp1Id.Value)   : null,
                    Responsable2  = a.UserId2.HasValue ? workerNameMap.GetValueOrDefault(a.UserId2.Value) : null,
                    EmailResp1    = resp1Id.HasValue   ? workerEmailMap.GetValueOrDefault(resp1Id.Value)  : null,
                    EmailResp2    = a.UserId2.HasValue ? workerEmailMap.GetValueOrDefault(a.UserId2.Value) : null,
                    FechaInicio   = a.InicioProgramado?.ToString("dd/MM/yyyy"),
                    FechaFin      = a.FinProgramado?.ToString("dd/MM/yyyy"),
                    Estado        = a.Estado,
                    Spi           = a.Spi,
                    Tipo          = a.Tipo ?? "",
                    Categoria     = a.CategoriaId.HasValue ? categorias.GetValueOrDefault(a.CategoriaId.Value) : null,
                    DiasRestantes = a.FinProgramado.HasValue ? (a.FinProgramado.Value.DayNumber - today.DayNumber) : 0,
                };
            }).ToList();
        }

        public async Task EnviarAlertasActividades(
            List<int> actividadIds, string tipoAlerta,
            List<string> emailsGestores, IEmailService emailService)
        {
            using var ctx = _factory.CreateDbContext();

            var actividades = await ctx.AcActividad
                .Where(a => actividadIds.Contains(a.Id))
                .ToListAsync();

            if (actividades.Count == 0) return;

            var workerIds = actividades
                .SelectMany(a => new[] { a.UserId, a.UserId2 })
                .Where(id => id.HasValue).Select(id => id!.Value)
                .Distinct().ToList();

            var emailMap = new Dictionary<int, string>();
            if (workerIds.Count > 0)
            {
                var we = await ctx.Worker
                    .Where(w => workerIds.Contains(w.Id))
                    .Select(w => new { w.Id, w.EmailPersonal })
                    .ToListAsync();
                emailMap = we.Where(x => x.EmailPersonal != null)
                    .ToDictionary(x => x.Id, x => x.EmailPersonal!);
            }

            var projectIds = actividades.Select(a => a.ProjectId).Distinct().ToList();
            var projects   = await ctx.Project
                .Where(p => projectIds.Contains(p.ProjectId))
                .ToDictionaryAsync(p => p.ProjectId, p => p.ProjectDescription ?? "");

            var tituloMap = new Dictionary<string, string>
            {
                ["VENCIDA"]      = "Actividades Vencidas Sin Cerrar",
                ["VENCE_SEMANA"] = "Actividades que Vencen Esta Semana",
                ["ARRANQUE"]     = "Actividades que Arrancan Esta Semana",
                ["HITO_PROXIMO"] = "Hitos Próximos a Vencer",
            };
            var titulo = tituloMap.GetValueOrDefault(tipoAlerta.ToUpperInvariant(), "Alerta de Actividades AC");

            var destinatarios = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in actividades)
            {
                if (a.UserId.HasValue  && emailMap.TryGetValue(a.UserId.Value,  out var e1)) destinatarios.Add(e1);
                if (a.UserId2.HasValue && emailMap.TryGetValue(a.UserId2.Value, out var e2)) destinatarios.Add(e2);
            }
            foreach (var eg in emailsGestores.Where(e => !string.IsNullOrEmpty(e)))
                destinatarios.Add(eg);

            if (destinatarios.Count == 0) return;

            var filas = string.Join("", actividades.Select(a =>
                $"<tr><td>{a.Nombre}</td><td>{projects.GetValueOrDefault(a.ProjectId, "")}</td>" +
                $"<td>{a.FinProgramado?.ToString("dd/MM/yyyy")}</td>" +
                $"<td>{a.Estado}</td><td>{a.Spi:F2}</td></tr>"));

            var html = $"""
                <h2 style="color:#1E3A5F">{titulo}</h2>
                <p>Se han identificado <b>{actividades.Count}</b> actividades que requieren atención:</p>
                <table border="1" cellpadding="6" cellspacing="0" style="border-collapse:collapse;width:100%;font-size:13px">
                  <thead style="background:#1E3A5F;color:#fff">
                    <tr><th>Actividad</th><th>Proyecto</th><th>Fecha Fin</th><th>Estado</th><th>SPI</th></tr>
                  </thead>
                  <tbody>{filas}</tbody>
                </table>
                <p style="color:#9CA3AF;font-size:11px;margin-top:16px">
                  Enviado automáticamente desde Abril AC Dashboard — {DateTime.Now:dd/MM/yyyy HH:mm}
                </p>
                """;

            await emailService.SendAsync(destinatarios.ToList(), titulo, html, isHtml: true);
        }
    }
}
