using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Repositories
{
    public class EvContratistaRepository : IEvContratistaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public EvContratistaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<EvEvaluacionContratista> CreateAsync(
            EvEvaluacionContratista eval,
            List<EvEvaluacionContratistaDetalle> detalles)
        {
            using var ctx = _factory.CreateDbContext();

            // Calcular nota normalizada a 20
            int maxPorCriterio = 4;
            int totalMax = detalles.Count * maxPorCriterio;
            decimal sumPuntajes = detalles.Sum(d => d.Puntaje);
            eval.Nota = totalMax > 0 ? Math.Round((sumPuntajes / totalMax) * 20m, 2) : 0;
            eval.Detalles = detalles;

            ctx.EvEvaluacionesContratista.Add(eval);
            await ctx.SaveChangesAsync();
            return eval;
        }

        public async Task<bool> ExisteAsync(
            int periodoId, int proyectoId, int contributorId, string areaNombre, int evaluadorUserId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.EvEvaluacionesContratista.AnyAsync(e =>
                e.PeriodoId == periodoId &&
                e.ProyectoId == proyectoId &&
                e.ContributorId == contributorId &&
                e.AreaNombre == areaNombre &&
                e.EvaluadorUserId == evaluadorUserId);
        }

        public async Task<EvContratistaInicioDto> GetInicioAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            // Período activo
            var periodo = await conn.QueryFirstOrDefaultAsync<EvPeriodoRaw>(
                "SELECT id, mes, anio, fecha_apertura, fecha_cierre, activo FROM ev_periodo WHERE activo = TRUE LIMIT 1");

            if (periodo == null)
                return new EvContratistaInicioDto();

            // Subarea del evaluador (para saber qué área evalúa)
            var evaluador = await conn.QueryFirstOrDefaultAsync<EvaluadorInfo>(
                @"SELECT w.subarea AS Subarea, w.area AS Area
                  FROM workers w
                  JOIN person p ON p.person_id = w.person_id
                  WHERE p.user_id = @UserId
                  LIMIT 1",
                new { UserId = userId });

            // Determinar área según subarea del worker
            string? areaMatch = null;
            string? puestoMatch = null;
            if (evaluador != null)
            {
                (areaMatch, puestoMatch) = ResolverArea(evaluador.Subarea ?? evaluador.Area ?? "");
            }

            if (areaMatch == null)
                return new EvContratistaInicioDto
                {
                    Periodo = MapPeriodo(periodo)
                };

            // Plantilla de criterios para esta área
            var plantilla = await conn.QueryAsync<EvContratistaCriterioDto>(
                @"SELECT id AS Id, criterio AS Criterio, orden AS Orden
                  FROM ev_contratista_plantilla
                  WHERE puesto_evaluador ILIKE @Puesto AND activo = TRUE
                  ORDER BY orden",
                new { Puesto = $"%{puestoMatch}%" });

            // Proyectos del evaluador
            var proyectosEvaluador = await conn.QueryAsync<int>(
                @"SELECT up.project_id
                  FROM user_project up
                  WHERE up.user_id = @UserId",
                new { UserId = userId });

            var proyectoIds = proyectosEvaluador.ToList();
            if (!proyectoIds.Any())
                return new EvContratistaInicioDto
                {
                    Periodo = MapPeriodo(periodo),
                    MiAreaNombre = areaMatch,
                    MiPuestoEvaluador = puestoMatch,
                    Plantilla = plantilla.ToList()
                };

            // Contratistas que tuvieron tareo en el mes/año del período activo
            // filtrados por los proyectos del evaluador
            var contratistas = await conn.QueryAsync<ContratistaRaw>(
                @"SELECT
                    c.contributor_id    AS ContributorId,
                    c.contributor_name  AS ContributorNombre,
                    c.contributor_ruc   AS ContributorRuc,
                    t.proyecto_id       AS ProyectoId,
                    pr.project_description AS ProyectoNombre,
                    COUNT(DISTINCT t.fecha) AS DiasLaborados
                  FROM ss_tareo_detalle_contratista tdc
                  JOIN ss_tareo t ON t.id = tdc.tareo_id
                  JOIN contributor c ON c.contributor_id = tdc.empresa_id
                  JOIN project pr ON pr.project_id = t.proyecto_id
                  WHERE EXTRACT(MONTH FROM t.fecha) = @Mes
                    AND EXTRACT(YEAR  FROM t.fecha) = @Anio
                    AND t.proyecto_id = ANY(@ProyectoIds)
                  GROUP BY c.contributor_id, c.contributor_name, c.contributor_ruc,
                           t.proyecto_id, pr.project_description
                  ORDER BY c.contributor_name",
                new { Mes = periodo.Mes, Anio = periodo.Anio, ProyectoIds = proyectoIds.ToArray() });

            // Verificar cuáles ya fueron evaluadas por este usuario en esta área
            var yaEvaluadas = await conn.QueryAsync<(int ContributorId, int ProyectoId, decimal Nota)>(
                @"SELECT contributor_id AS ContributorId, proyecto_id AS ProyectoId, nota AS Nota
                  FROM ev_evaluacion_contratista
                  WHERE periodo_id = @PeriodoId
                    AND evaluador_user_id = @UserId
                    AND area_nombre = @Area",
                new { PeriodoId = periodo.Id, UserId = userId, Area = areaMatch });

            var evaluadasMap = yaEvaluadas.ToDictionary(
                x => (x.ContributorId, x.ProyectoId),
                x => x.Nota);

            var aEvaluar = contratistas.Select(c =>
            {
                var key = (c.ContributorId, c.ProyectoId);
                var yaEvalue = evaluadasMap.TryGetValue(key, out var nota);
                return new EvContratistaAEvaluarDto
                {
                    ContributorId = c.ContributorId,
                    ContributorNombre = c.ContributorNombre,
                    ContributorRuc = c.ContributorRuc,
                    ProyectoId = c.ProyectoId,
                    ProyectoNombre = c.ProyectoNombre,
                    DiasLaborados = c.DiasLaborados,
                    YaEvalue = yaEvalue,
                    NotaPrevia = yaEvalue ? nota : null
                };
            }).ToList();

            return new EvContratistaInicioDto
            {
                Periodo = MapPeriodo(periodo),
                MiAreaNombre = areaMatch,
                MiPuestoEvaluador = puestoMatch,
                Plantilla = plantilla.ToList(),
                ContratistasAEvaluar = aEvaluar
            };
        }

        public async Task<EvContratistaVerInicioDto> GetVerInicioAsync(int? periodoId, int? proyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            // Periodos disponibles
            var periodos = await conn.QueryAsync<EvPeriodoRaw>(
                "SELECT id, mes, anio, fecha_apertura, fecha_cierre, activo FROM ev_periodo ORDER BY anio DESC, mes DESC LIMIT 24");

            // Proyectos con evaluaciones
            var proyectos = await conn.QueryAsync<EvContratistaProyectoFiltroDto>(
                @"SELECT DISTINCT pr.project_id AS ProyectoId, pr.project_description AS ProyectoNombre
                  FROM ev_evaluacion_contratista ec
                  JOIN project pr ON pr.project_id = ec.proyecto_id
                  ORDER BY pr.project_description");

            // Período a usar: el solicitado o el activo
            int targetPeriodo;
            if (periodoId.HasValue)
            {
                targetPeriodo = periodoId.Value;
            }
            else
            {
                var activo = await conn.QueryFirstOrDefaultAsync<int?>(
                    "SELECT id FROM ev_periodo WHERE activo = TRUE LIMIT 1");
                if (!activo.HasValue)
                    return new EvContratistaVerInicioDto
                    {
                        Periodos = periodos.Select(MapPeriodo).ToList(),
                        Proyectos = proyectos.ToList()
                    };
                targetPeriodo = activo.Value;
            }

            var evaluaciones = await ObtenerResumenesAsync(conn, targetPeriodo, proyectoId);

            return new EvContratistaVerInicioDto
            {
                Periodos = periodos.Select(MapPeriodo).ToList(),
                Proyectos = proyectos.ToList(),
                Evaluaciones = evaluaciones
            };
        }

        public async Task<EvContratistaDashboardDto> GetDashboardAsync(int? periodoId, int? proyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            // Período objetivo
            int targetPeriodo;
            if (periodoId.HasValue)
            {
                targetPeriodo = periodoId.Value;
            }
            else
            {
                var activo = await conn.QueryFirstOrDefaultAsync<int?>(
                    "SELECT id FROM ev_periodo WHERE activo = TRUE LIMIT 1");
                if (!activo.HasValue)
                    return new EvContratistaDashboardDto();
                targetPeriodo = activo.Value;
            }

            var contratistas = await ObtenerResumenesAsync(conn, targetPeriodo, proyectoId);

            // Promedios por área
            var promediosArea = await conn.QueryAsync<EvContratistaAreaPromedioDto>(
                @"SELECT
                    area_nombre AS AreaNombre,
                    ROUND(AVG(nota)::NUMERIC, 1) AS Promedio,
                    COUNT(*) AS TotalEvaluaciones
                  FROM ev_evaluacion_contratista
                  WHERE periodo_id = @PeriodoId
                    AND (@ProyectoId IS NULL OR proyecto_id = @ProyectoId)
                  GROUP BY area_nombre
                  ORDER BY area_nombre",
                new { PeriodoId = targetPeriodo, ProyectoId = proyectoId });

            // Tendencia histórica (últimos 6 períodos)
            var tendencia = await conn.QueryAsync<EvContratistaTendenciaRaw>(
                @"SELECT
                    ep.mes AS Mes, ep.anio AS Anio,
                    ec.contributor_id AS ContributorId,
                    c.contributor_name AS ContributorNombre,
                    ROUND(AVG(ec.nota)::NUMERIC, 1) AS NotaTotal
                  FROM ev_evaluacion_contratista ec
                  JOIN ev_periodo ep ON ep.id = ec.periodo_id
                  JOIN contributor c ON c.contributor_id = ec.contributor_id
                  WHERE ec.periodo_id IN (
                      SELECT id FROM ev_periodo ORDER BY anio DESC, mes DESC LIMIT 6
                  )
                    AND (@ProyectoId IS NULL OR ec.proyecto_id = @ProyectoId)
                  GROUP BY ep.mes, ep.anio, ec.contributor_id, c.contributor_name
                  ORDER BY ep.anio, ep.mes, c.contributor_name",
                new { ProyectoId = proyectoId });

            var tendenciaDto = tendencia.Select(t => new EvContratistaTendenciaDto
            {
                Mes = t.Mes,
                Anio = t.Anio,
                NombreMes = new DateTime(t.Anio, t.Mes, 1).ToString("MMM", new System.Globalization.CultureInfo("es-PE")),
                ContributorId = t.ContributorId,
                ContributorNombre = t.ContributorNombre,
                NotaTotal = t.NotaTotal
            }).ToList();

            var aprobados = contratistas.Count(c => c.NotaTotal.HasValue && c.NotaTotal.Value > 15);
            var regulares = contratistas.Count(c => c.NotaTotal.HasValue && c.NotaTotal.Value >= 12 && c.NotaTotal.Value <= 15);
            var desaprobados = contratistas.Count(c => c.NotaTotal.HasValue && c.NotaTotal.Value < 12);

            decimal? promedioGeneral = null;
            var conNota = contratistas.Where(c => c.NotaTotal.HasValue).ToList();
            if (conNota.Any())
                promedioGeneral = Math.Round(conNota.Average(c => c.NotaTotal!.Value), 1);

            return new EvContratistaDashboardDto
            {
                TotalContratistas = contratistas.Count,
                Aprobados = aprobados,
                Regulares = regulares,
                Desaprobados = desaprobados,
                PromedioGeneral = promedioGeneral,
                Contratistas = contratistas,
                PromediosPorArea = promediosArea.ToList(),
                Tendencia = tendenciaDto
            };
        }

        // ─── Helpers ───────────────────────────────────────────────────────────

        private static async Task<List<EvContratistaResumenDto>> ObtenerResumenesAsync(
            System.Data.IDbConnection conn, int periodoId, int? proyectoId)
        {
            var notas = await conn.QueryAsync<NotaAreaRaw>(
                @"SELECT
                    ec.contributor_id   AS ContributorId,
                    c.contributor_name  AS ContributorNombre,
                    c.contributor_ruc   AS ContributorRuc,
                    ec.proyecto_id      AS ProyectoId,
                    pr.project_description AS ProyectoNombre,
                    ec.area_nombre      AS AreaNombre,
                    ec.nota             AS Nota
                  FROM ev_evaluacion_contratista ec
                  JOIN contributor c ON c.contributor_id = ec.contributor_id
                  JOIN project pr    ON pr.project_id    = ec.proyecto_id
                  WHERE ec.periodo_id = @PeriodoId
                    AND (@ProyectoId IS NULL OR ec.proyecto_id = @ProyectoId)
                  ORDER BY c.contributor_name, pr.project_description",
                new { PeriodoId = periodoId, ProyectoId = proyectoId });

            return notas
                .GroupBy(n => (n.ContributorId, n.ProyectoId))
                .Select(g =>
                {
                    var first = g.First();
                    decimal? NotaDeArea(string area) =>
                        g.FirstOrDefault(x => x.AreaNombre.Equals(area, StringComparison.OrdinalIgnoreCase))?.Nota;

                    var notasValidas = g.Where(x => x.Nota.HasValue).Select(x => x.Nota!.Value).ToList();
                    decimal? total = notasValidas.Any() ? Math.Round(notasValidas.Average(), 1) : null;

                    string estado = total switch
                    {
                        null => "Sin evaluar",
                        > 15 => "Aprobado",
                        >= 12 => "Regular",
                        _ => "Desaprobado"
                    };

                    return new EvContratistaResumenDto
                    {
                        ContributorId = first.ContributorId,
                        ContributorNombre = first.ContributorNombre,
                        ContributorRuc = first.ContributorRuc,
                        ProyectoId = first.ProyectoId,
                        ProyectoNombre = first.ProyectoNombre,
                        NotaSsoma = NotaDeArea("SSOMA"),
                        NotaOT = NotaDeArea("Oficina Técnica"),
                        NotaProduccion = NotaDeArea("Producción"),
                        NotaResidencia = NotaDeArea("Residencia"),
                        NotaCalidad = NotaDeArea("Calidad"),
                        NotaTotal = total,
                        Estado = estado
                    };
                })
                .OrderByDescending(c => c.NotaTotal)
                .ToList();
        }

        // Mapea subarea/area del worker a la área de evaluación de contratistas
        private static (string? AreaNombre, string? PuestoEvaluador) ResolverArea(string subarea)
        {
            if (string.IsNullOrWhiteSpace(subarea)) return (null, null);
            var s = subarea.ToUpperInvariant();
            if (s.Contains("SSOMA")) return ("SSOMA", "Responsable SSOMA");
            if (s.Contains("OFICINA") || s.Contains("TÉCNICA") || s.Contains("TECNICA") || s.Contains("OT")) return ("Oficina Técnica", "Jefe de Oficina Técnica");
            if (s.Contains("PRODUCCI") || s.Contains("ING.PROD") || s.Contains("ING. PROD")) return ("Producción", "Residente / Ingeniero de Producción");
            if (s.Contains("CALIDAD")) return ("Calidad", "Responsable de Calidad");
            if (s.Contains("RESIDEN")) return ("Residencia", "Residente de Obra");
            return (null, null);
        }

        private static EvPeriodoDto MapPeriodo(EvPeriodoRaw r) => new()
        {
            Id = r.Id,
            Mes = r.Mes,
            Anio = r.Anio,
            FechaApertura = r.FechaApertura,
            FechaCierre = r.FechaCierre,
            Activo = r.Activo,
        };

        // ─── Raw helpers ───────────────────────────────────────────────────────
        private record EvPeriodoRaw(int Id, int Mes, int Anio, DateOnly FechaApertura, DateOnly FechaCierre, bool Activo);
        private record EvaluadorInfo(string? Subarea, string? Area);
        private record ContratistaRaw(int ContributorId, string ContributorNombre, string ContributorRuc, int ProyectoId, string ProyectoNombre, int DiasLaborados);
        private record NotaAreaRaw(int ContributorId, string ContributorNombre, string ContributorRuc, int ProyectoId, string ProyectoNombre, string AreaNombre, decimal? Nota);
        private record EvContratistaTendenciaRaw(int Mes, int Anio, int ContributorId, string ContributorNombre, decimal? NotaTotal);
    }
}
