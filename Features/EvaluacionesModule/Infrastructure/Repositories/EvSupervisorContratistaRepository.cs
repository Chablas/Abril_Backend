using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Repositories
{
    public class EvSupervisorContratistaRepository : IEvSupervisorContratistaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public EvSupervisorContratistaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<EvSupervisorContratistaInicioDto> GetInicioAsync(int evaluadorUserId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            var periodo = await conn.QueryFirstOrDefaultAsync<EvPeriodoRaw>(
                "SELECT id, mes, anio, fecha_apertura, fecha_cierre, activo FROM ev_periodo WHERE activo = TRUE LIMIT 1");

            var plantilla = await conn.QueryAsync<EvSupervisorContratistaCriterioDto>(
                @"SELECT id AS Id, criterio AS Criterio, orden AS Orden
                  FROM ev_supervisor_contratista_plantilla
                  WHERE activo = TRUE ORDER BY orden");

            if (periodo == null)
                return new EvSupervisorContratistaInicioDto { Plantilla = plantilla.ToList() };

            var yaMarcoNoAplica = await conn.QueryFirstOrDefaultAsync<bool>(
                @"SELECT EXISTS (
                    SELECT 1 FROM ev_evaluacion_supervisor_contratista
                    WHERE periodo_id = @PeriodoId AND evaluador_user_id = @UserId AND no_aplica = TRUE
                  )",
                new { PeriodoId = periodo.Id, UserId = evaluadorUserId });

            // Proyectos donde el evaluador (Prevencionista/Coordinador SSOMA) está
            // actualmente destacado, según su vinculación vigente.
            var proyectoIds = (await conn.QueryAsync<int>(
                @"SELECT DISTINCT wv.proyecto_id
                  FROM workers w
                  JOIN person p ON p.person_id = w.person_id
                  JOIN worker_vinculaciones wv ON wv.worker_id = w.id AND wv.fecha_fin IS NULL
                  WHERE p.user_id = @UserId",
                new { UserId = evaluadorUserId })).ToList();

            if (proyectoIds.Count == 0)
                return new EvSupervisorContratistaInicioDto
                {
                    Periodo = MapPeriodo(periodo),
                    Plantilla = plantilla.ToList(),
                    YaMarcoNoAplica = yaMarcoNoAplica
                };

            // Supervisores de campo (rol de sistema 74) de contratistas activos en esos proyectos.
            var supervisores = await conn.QueryAsync<SupervisorRaw>(
                @"SELECT DISTINCT
                    scu.id                AS SupervisorSsContratistaUsuarioId,
                    COALESCE(p.full_name, au.email) AS SupervisorNombre,
                    c.contributor_id      AS ContributorId,
                    c.contributor_name    AS ContributorNombre,
                    scup.proyecto_id      AS ProyectoId,
                    pr.project_description AS ProyectoNombre
                  FROM ss_contratista_usuario scu
                  JOIN user_role ur ON ur.user_id = scu.user_id AND ur.role_id = 74 AND ur.active = TRUE AND ur.state = TRUE
                  JOIN app_user au ON au.user_id = scu.user_id
                  JOIN contributor c ON c.contributor_id = scu.contractor_id
                  JOIN ss_contratista_usuario_proyecto scup ON scup.contratista_usuario_id = scu.id
                  JOIN project pr ON pr.project_id = scup.proyecto_id
                  LEFT JOIN workers w ON w.id = scu.worker_id
                  LEFT JOIN person p ON p.person_id = w.person_id
                  WHERE scu.activo = TRUE AND scup.proyecto_id = ANY(@ProyectoIds)",
                new { ProyectoIds = proyectoIds.ToArray() });

            var yaEvaluadas = await conn.QueryAsync<YaEvaluadaRaw>(
                @"SELECT supervisor_ss_contratista_usuario_id AS SupervisorId, nota AS Nota
                  FROM ev_evaluacion_supervisor_contratista
                  WHERE periodo_id = @PeriodoId AND evaluador_user_id = @UserId",
                new { PeriodoId = periodo.Id, UserId = evaluadorUserId });
            var evaluadasMap = yaEvaluadas.ToDictionary(x => x.SupervisorId);

            var aEvaluar = supervisores.Select(s =>
            {
                var yaEvalue = evaluadasMap.TryGetValue(s.SupervisorSsContratistaUsuarioId, out var previa);
                return new EvSupervisorContratistaAEvaluarDto
                {
                    SupervisorSsContratistaUsuarioId = s.SupervisorSsContratistaUsuarioId,
                    SupervisorNombre = s.SupervisorNombre,
                    ContributorId = s.ContributorId,
                    ContributorNombre = s.ContributorNombre,
                    ProyectoId = s.ProyectoId,
                    ProyectoNombre = s.ProyectoNombre,
                    YaEvalue = yaEvalue,
                    NotaPrevia = yaEvalue ? previa.Nota : null
                };
            }).ToList();

            return new EvSupervisorContratistaInicioDto
            {
                Periodo = MapPeriodo(periodo),
                Plantilla = plantilla.ToList(),
                SupervisoresAEvaluar = aEvaluar,
                YaMarcoNoAplica = yaMarcoNoAplica
            };
        }

        public async Task<EvEvaluacionSupervisorContratista> CreateAsync(
            EvEvaluacionSupervisorContratista eval, List<EvEvaluacionSupervisorContratistaDetalle> detalles)
        {
            using var ctx = _factory.CreateDbContext();

            int maxPorCriterio = 4;
            var puntajesValidos = detalles.Where(d => !d.EsNa && d.Puntaje.HasValue).Select(d => d.Puntaje!.Value).ToList();
            int totalMax = puntajesValidos.Count * maxPorCriterio;
            decimal sumPuntajes = puntajesValidos.Sum();
            eval.Nota = totalMax > 0 ? Math.Round((sumPuntajes / totalMax) * 20m, 2) : 0;
            eval.Detalles = detalles;

            ctx.EvEvaluacionesSupervisorContratista.Add(eval);
            await ctx.SaveChangesAsync();
            return eval;
        }

        public async Task<bool> ExisteAsync(int periodoId, int supervisorSsContratistaUsuarioId, int evaluadorUserId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.EvEvaluacionesSupervisorContratista.AnyAsync(e =>
                e.PeriodoId == periodoId &&
                e.SupervisorSsContratistaUsuarioId == supervisorSsContratistaUsuarioId &&
                e.EvaluadorUserId == evaluadorUserId);
        }

        public async Task<bool> ExisteNoAplicaAsync(int periodoId, int evaluadorUserId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.EvEvaluacionesSupervisorContratista.AnyAsync(e =>
                e.PeriodoId == periodoId && e.EvaluadorUserId == evaluadorUserId && e.NoAplica);
        }

        public async Task RegistrarNoAplicaAsync(
            int periodoId, int evaluadorUserId, string motivo,
            int? proyectoId = null, int? supervisorSsContratistaUsuarioId = null)
        {
            using var ctx = _factory.CreateDbContext();
            ctx.EvEvaluacionesSupervisorContratista.Add(new EvEvaluacionSupervisorContratista
            {
                PeriodoId = periodoId,
                EvaluadorUserId = evaluadorUserId,
                ProyectoId = proyectoId ?? 0,
                SupervisorSsContratistaUsuarioId = supervisorSsContratistaUsuarioId ?? 0,
                NoAplica = true,
                NoAplicaMotivo = motivo
            });
            await ctx.SaveChangesAsync();
        }

        public async Task<EvSupervisorContratistaVerInicioDto> GetVerInicioAsync(int? periodoId, int? proyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            var periodos = await conn.QueryAsync<EvPeriodoRaw>(
                "SELECT id, mes, anio, fecha_apertura, fecha_cierre, activo FROM ev_periodo ORDER BY anio DESC, mes DESC LIMIT 24");

            var proyectos = await conn.QueryAsync<EvSupervisorContratistaProyectoFiltroDto>(
                @"SELECT DISTINCT pr.project_id AS ProyectoId, pr.project_description AS ProyectoNombre
                  FROM ev_evaluacion_supervisor_contratista ec
                  JOIN project pr ON pr.project_id = ec.proyecto_id
                  WHERE ec.no_aplica = FALSE
                  ORDER BY pr.project_description");

            var evaluaciones = await ObtenerResumenesAsync(conn, periodoId, proyectoId);

            return new EvSupervisorContratistaVerInicioDto
            {
                Periodos = periodos.Select(MapPeriodo).ToList(),
                Proyectos = proyectos.ToList(),
                Evaluaciones = evaluaciones
            };
        }

        public async Task<EvSupervisorContratistaDashboardDto> GetDashboardAsync(int? periodoId, int? proyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            var evaluaciones = await ObtenerResumenesAsync(conn, periodoId, proyectoId);
            var conNota = evaluaciones.Where(e => e.Nota.HasValue).ToList();

            return new EvSupervisorContratistaDashboardDto
            {
                TotalEvaluaciones = evaluaciones.Count,
                PromedioGeneral = conNota.Count > 0 ? Math.Round(conNota.Average(e => e.Nota!.Value), 2) : null,
                Evaluaciones = evaluaciones
            };
        }

        private static async Task<List<EvSupervisorContratistaResumenDto>> ObtenerResumenesAsync(
            System.Data.IDbConnection conn, int? periodoId, int? proyectoId)
        {
            var rows = await conn.QueryAsync<EvSupervisorContratistaResumenDto>(
                @"SELECT
                    ec.id                  AS EvaluacionId,
                    ec.supervisor_ss_contratista_usuario_id AS SupervisorSsContratistaUsuarioId,
                    ec.supervisor_nombre   AS SupervisorNombre,
                    ec.contributor_id      AS ContributorId,
                    c.contributor_name     AS ContributorNombre,
                    ec.proyecto_id         AS ProyectoId,
                    pr.project_description AS ProyectoNombre,
                    COALESCE(p.full_name, au.email) AS EvaluadorNombre,
                    ec.nota                AS Nota,
                    ec.comentario           AS Comentario,
                    ec.created_at           AS CreatedAt
                  FROM ev_evaluacion_supervisor_contratista ec
                  JOIN contributor c ON c.contributor_id = ec.contributor_id
                  JOIN project pr    ON pr.project_id    = ec.proyecto_id
                  JOIN app_user au   ON au.user_id       = ec.evaluador_user_id
                  LEFT JOIN workers w  ON w.id = (
                      SELECT w2.id FROM workers w2
                      JOIN person p2 ON p2.person_id = w2.person_id
                      WHERE p2.user_id = ec.evaluador_user_id LIMIT 1)
                  LEFT JOIN person p ON p.person_id = w.person_id
                  WHERE ec.no_aplica = FALSE
                    AND (@PeriodoId IS NULL OR ec.periodo_id = @PeriodoId)
                    AND (@ProyectoId IS NULL OR ec.proyecto_id = @ProyectoId)
                  ORDER BY ec.created_at DESC",
                new { PeriodoId = periodoId, ProyectoId = proyectoId });

            return rows.ToList();
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

        private record EvPeriodoRaw(int Id, int Mes, int Anio, DateOnly FechaApertura, DateOnly FechaCierre, bool Activo);
        private record SupervisorRaw(int SupervisorSsContratistaUsuarioId, string SupervisorNombre, int ContributorId, string ContributorNombre, int ProyectoId, string ProyectoNombre);
        private record YaEvaluadaRaw(int SupervisorId, decimal? Nota);
    }
}
