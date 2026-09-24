using Abril_Backend.Shared.Constants;
using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Repositories
{
    public class EvEvaluacionStaffRepository : IEvEvaluacionStaffRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public EvEvaluacionStaffRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // Además del Residente (dueño natural del flujo), el Jefe SSOMA (puesto_id
        // fijo, categoría "JEFE" genérica igual que en EvJefeSsomaRepository) tiene
        // el mismo acceso: puede ver/evaluar el staff de SU PROPIO proyecto vigente,
        // igual que un residente vería el suyo.
        public async Task<bool> EsResidenteAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();
            return await conn.QueryFirstOrDefaultAsync<bool>(
                @"SELECT EXISTS (
                    SELECT 1
                    FROM app_user au
                    JOIN workers w ON LOWER(w.email_corporativo) = LOWER(au.email)
                    JOIN puesto pu ON pu.puesto_id = w.puesto_id
                    WHERE au.user_id = @UserId AND w.state AND w.contrata_casa = 'Casa' AND w.workers_estado_id = 1 /* WorkersEstadoIds.Activo */
                      AND (pu.categoria_id = @CategoriaResidente OR w.puesto_id = @PuestoJefeSsoma)
                  )",
                new { UserId = userId, CategoriaResidente = CategoriaIds.Residente, PuestoJefeSsoma = PuestoIds.JefeSsoma });
        }

        // El proyecto vigente de un worker no vive en workers (no hay workers.project_id):
        // se lee de worker_vinculaciones sin fecha_fin, el mismo criterio que usa
        // EvGestionSsomaRepository.ObtenerProyectosDeAsync.
        public async Task<int?> ObtenerProyectoDeResidenteAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();
            return await conn.QueryFirstOrDefaultAsync<int?>(
                @"SELECT wv.proyecto_id
                  FROM app_user au
                  JOIN workers w ON LOWER(w.email_corporativo) = LOWER(au.email)
                  JOIN worker_vinculaciones wv ON wv.worker_id = w.id AND wv.fecha_fin IS NULL
                  WHERE au.user_id = @UserId AND w.state AND w.contrata_casa = 'Casa' AND w.workers_estado_id = 1 /* WorkersEstadoIds.Activo */
                  LIMIT 1",
                new { UserId = userId });
        }

        public async Task<List<EvEvaluacionStaffPendienteDto>> GetPendientesAsync(int evaluadorUserId, int periodoId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            var proyectoId = await ObtenerProyectoDeResidenteAsync(evaluadorUserId);
            if (proyectoId == null) return [];

            var puestoIds = PuestoIds.StaffEvaluablePuestoIds.Keys.ToArray();

            var staff = await conn.QueryAsync<StaffRaw>(
                @"SELECT w.id AS WorkerId, p.full_name AS NombreCompleto, w.puesto_id AS PuestoId, pu.nombre AS PuestoNombre
                  FROM workers w
                  JOIN person p ON p.person_id = w.person_id
                  JOIN puesto pu ON pu.puesto_id = w.puesto_id
                  JOIN worker_vinculaciones wv ON wv.worker_id = w.id AND wv.fecha_fin IS NULL
                  WHERE w.state AND w.workers_estado_id = @WorkersEstadoActivo
                    AND w.obra_oficina_staff_id = @ObraOficinaStaff
                    AND wv.proyecto_id = @ProyectoId
                    AND w.puesto_id = ANY(@PuestoIds)",
                new
                {
                    WorkersEstadoActivo = WorkersEstadoIds.Activo,
                    ObraOficinaStaff = ObraOficinaStaffIds.Staff,
                    ProyectoId = proyectoId.Value,
                    PuestoIds = puestoIds,
                });

            var yaEvaluados = (await conn.QueryAsync<int>(
                "SELECT evaluado_worker_id FROM ev_evaluacion_staff WHERE periodo_id = @PeriodoId AND evaluador_user_id = @UserId",
                new { PeriodoId = periodoId, UserId = evaluadorUserId })).ToHashSet();

            return staff.Where(s => !yaEvaluados.Contains(s.WorkerId))
                .Select(s => new EvEvaluacionStaffPendienteDto
                {
                    WorkerId = s.WorkerId,
                    NombreCompleto = s.NombreCompleto,
                    PuestoId = s.PuestoId,
                    PuestoNombre = s.PuestoNombre,
                }).ToList();
        }

        public async Task<List<EvStaffPlantillaCriterioDto>> GetPlantillaPorPuestoAsync(int puestoId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.EvStaffPlantillas
                .Where(p => p.PuestoId == puestoId && p.Activo)
                .OrderBy(p => p.Orden)
                .Select(p => new EvStaffPlantillaCriterioDto
                {
                    Id = p.Id,
                    Criterio = p.Criterio,
                    Tipo = p.Tipo,
                    Orden = p.Orden,
                })
                .ToListAsync();
        }

        public async Task<int?> ValidarEvaluadoAsync(int evaluadoWorkerId, int projectId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            var puestoIds = PuestoIds.StaffEvaluablePuestoIds.Keys.ToArray();

            return await conn.QueryFirstOrDefaultAsync<int?>(
                @"SELECT w.puesto_id
                  FROM workers w
                  JOIN worker_vinculaciones wv ON wv.worker_id = w.id AND wv.fecha_fin IS NULL
                  WHERE w.id = @WorkerId AND w.state AND w.workers_estado_id = @WorkersEstadoActivo
                    AND w.obra_oficina_staff_id = @ObraOficinaStaff
                    AND wv.proyecto_id = @ProjectId
                    AND w.puesto_id = ANY(@PuestoIds)
                  LIMIT 1",
                new
                {
                    WorkerId = evaluadoWorkerId,
                    WorkersEstadoActivo = WorkersEstadoIds.Activo,
                    ObraOficinaStaff = ObraOficinaStaffIds.Staff,
                    ProjectId = projectId,
                    PuestoIds = puestoIds,
                });
        }

        public async Task<bool> YaEvaluoAsync(int periodoId, int evaluadorUserId, int evaluadoWorkerId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.EvEvaluacionesStaff.AnyAsync(e =>
                e.PeriodoId == periodoId && e.EvaluadorUserId == evaluadorUserId && e.EvaluadoWorkerId == evaluadoWorkerId);
        }

        public async Task CreateAsync(
            int periodoId, int evaluadorUserId, int evaluadoWorkerId, int projectId,
            string? comentario, List<(int? plantillaId, string criterio, int puntaje)> detalles, decimal nota)
        {
            using var ctx = _factory.CreateDbContext();

            var yaEvaluo = await ctx.EvEvaluacionesStaff.AnyAsync(e =>
                e.PeriodoId == periodoId && e.EvaluadorUserId == evaluadorUserId && e.EvaluadoWorkerId == evaluadoWorkerId);
            if (yaEvaluo)
                throw new AbrilException("Ya evaluaste a este trabajador en este período.", 409);

            var eval = new EvEvaluacionStaff
            {
                PeriodoId = periodoId,
                EvaluadorUserId = evaluadorUserId,
                EvaluadoWorkerId = evaluadoWorkerId,
                ProjectId = projectId,
                Nota = nota,
                Comentario = comentario,
                Detalles = detalles.Select(d => new EvEvaluacionStaffDetalle
                {
                    PlantillaId = d.plantillaId,
                    Criterio = d.criterio,
                    Puntaje = d.puntaje,
                }).ToList(),
            };
            ctx.EvEvaluacionesStaff.Add(eval);
            await ctx.SaveChangesAsync();
        }

        public async Task<List<EvEvaluacionStaffResultadoDto>> GetResultadosAsync(int periodoId, int? projectId, int? puestoId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            var filas = await conn.QueryAsync<ResultadoRaw>(
                @"SELECT w.id AS WorkerId, p.full_name AS NombreCompleto, w.puesto_id AS PuestoId,
                         pu.nombre AS PuestoNombre, e.project_id AS ProjectId,
                         AVG(e.nota) AS PromedioNota,
                         STRING_AGG(e.comentario, ' | ') FILTER (WHERE e.comentario IS NOT NULL AND e.comentario != '') AS Comentario
                  FROM ev_evaluacion_staff e
                  JOIN workers w ON w.id = e.evaluado_worker_id
                  JOIN person p ON p.person_id = w.person_id
                  JOIN puesto pu ON pu.puesto_id = w.puesto_id
                  WHERE e.periodo_id = @PeriodoId
                    AND (@ProjectId::int IS NULL OR e.project_id = @ProjectId)
                    AND (@PuestoId::int IS NULL OR w.puesto_id = @PuestoId)
                  GROUP BY w.id, p.full_name, w.puesto_id, pu.nombre, e.project_id
                  ORDER BY p.full_name",
                new { PeriodoId = periodoId, ProjectId = projectId, PuestoId = puestoId });

            return filas.Select(f => new EvEvaluacionStaffResultadoDto
            {
                WorkerId = f.WorkerId,
                NombreCompleto = f.NombreCompleto,
                PuestoId = f.PuestoId,
                PuestoNombre = f.PuestoNombre,
                ProjectId = f.ProjectId,
                PromedioNota = f.PromedioNota.HasValue ? Math.Round(f.PromedioNota.Value, 2) : null,
                Comentario = f.Comentario,
            }).ToList();
        }

        private record StaffRaw(int WorkerId, string NombreCompleto, int PuestoId, string PuestoNombre);
        private record ResultadoRaw(int WorkerId, string NombreCompleto, int PuestoId, string PuestoNombre, int ProjectId, decimal? PromedioNota, string? Comentario);
    }
}
