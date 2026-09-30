using Abril_Backend.Shared.Constants;
using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services.Residentes.Services;
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

        // Evalúan al staff el Residente de cada obra (dueño natural del flujo) y el Jefe SSOMA.
        // El residente es quien figura en Configuración → Proyectos con el rol RESIDENTE
        // (ResidenteQueries.ObrasConSuResidente) y evalúa al staff de esas obras: ni su puesto ni
        // su vinculación deciden. El Jefe SSOMA (puesto_id fijo, categoría "JEFE" genérica igual
        // que en EvJefeSsomaRepository) tiene el mismo acceso sobre el staff de SU PROPIO proyecto
        // vigente, igual que un residente sobre el de su obra.
        public async Task<bool> EsResidenteAsync(int userId)
            => await EsResidenteDeObraAsync(userId) || await EsJefeSsomaAsync(userId);

        public async Task<bool> EsResidenteDeObraAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.ObrasConSuResidente().AnyAsync(o => o.UserId == userId);
        }

        // Obras cuyo staff evalúa el usuario: las del residente, y si no es residente de ninguna,
        // el proyecto vigente del Jefe SSOMA. El proyecto vigente de un worker no vive en workers
        // (no hay workers.project_id): se lee de worker_vinculaciones sin fecha_fin, el mismo
        // criterio que usa EvGestionSsomaRepository.ObtenerProyectosDeAsync.
        public async Task<List<int>> ObtenerProyectosDelEvaluadorAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var obras = await ctx.ObrasConSuResidente()
                .Where(o => o.UserId == userId)
                .Select(o => o.ProjectId)
                .Distinct()
                .ToListAsync();
            if (obras.Count > 0) return obras;

            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();
            return (await conn.QueryAsync<int>(
                @"SELECT DISTINCT wv.proyecto_id
                  FROM app_user au
                  JOIN workers w ON LOWER(w.email_corporativo) = LOWER(au.email)
                  JOIN worker_vinculaciones wv ON wv.worker_id = w.id AND wv.fecha_fin IS NULL
                  WHERE au.user_id = @UserId AND w.state AND w.contrata_casa = 'Casa' AND w.workers_estado_id = 1 /* WorkersEstadoIds.Activo */
                    AND w.puesto_id = @PuestoJefeSsoma",
                new { UserId = userId, PuestoJefeSsoma = PuestoIds.JefeSsoma })).ToList();
        }

        // Por correo y por puesto único, el mismo criterio que EvGestionSsomaRepository.EsJefeSsomaAsync.
        private async Task<bool> EsJefeSsomaAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();
            return await conn.QueryFirstOrDefaultAsync<bool>(
                @"SELECT EXISTS (
                    SELECT 1
                    FROM app_user au
                    JOIN workers w ON LOWER(w.email_corporativo) = LOWER(au.email)
                    WHERE au.user_id = @UserId AND w.state AND w.contrata_casa = 'Casa' AND w.workers_estado_id = 1 /* WorkersEstadoIds.Activo */
                      AND w.puesto_id = @PuestoJefeSsoma
                  )",
                new { UserId = userId, PuestoJefeSsoma = PuestoIds.JefeSsoma });
        }

        public async Task<List<EvEvaluacionStaffPendienteDto>> GetPendientesAsync(int evaluadorUserId, int periodoId)
        {
            var proyectoIds = await ObtenerProyectosDelEvaluadorAsync(evaluadorUserId);
            if (proyectoIds.Count == 0) return [];

            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            var puestoIds = PuestoIds.StaffEvaluablePuestoIds.Keys.ToArray();

            var staff = await conn.QueryAsync<StaffRaw>(
                @"SELECT DISTINCT w.id AS WorkerId, p.full_name AS NombreCompleto, w.puesto_id AS PuestoId, pu.nombre AS PuestoNombre
                  FROM workers w
                  JOIN person p ON p.person_id = w.person_id
                  JOIN puesto pu ON pu.puesto_id = w.puesto_id
                  JOIN worker_vinculaciones wv ON wv.worker_id = w.id AND wv.fecha_fin IS NULL
                  WHERE w.state AND w.workers_estado_id = @WorkersEstadoActivo
                    AND w.obra_oficina_staff_id = @ObraOficinaStaff
                    AND wv.proyecto_id = ANY(@ProyectoIds)
                    AND w.puesto_id = ANY(@PuestoIds)",
                new
                {
                    WorkersEstadoActivo = WorkersEstadoIds.Activo,
                    ObraOficinaStaff = ObraOficinaStaffIds.Staff,
                    ProyectoIds = proyectoIds.ToArray(),
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

        public async Task<int?> ValidarEvaluadoAsync(int evaluadoWorkerId, List<int> projectIds)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            var puestoIds = PuestoIds.StaffEvaluablePuestoIds.Keys.ToArray();

            return await conn.QueryFirstOrDefaultAsync<int?>(
                @"SELECT wv.proyecto_id
                  FROM workers w
                  JOIN worker_vinculaciones wv ON wv.worker_id = w.id AND wv.fecha_fin IS NULL
                  WHERE w.id = @WorkerId AND w.state AND w.workers_estado_id = @WorkersEstadoActivo
                    AND w.obra_oficina_staff_id = @ObraOficinaStaff
                    AND wv.proyecto_id = ANY(@ProjectIds)
                    AND w.puesto_id = ANY(@PuestoIds)
                  ORDER BY wv.fecha_inicio DESC
                  LIMIT 1",
                new
                {
                    WorkerId = evaluadoWorkerId,
                    WorkersEstadoActivo = WorkersEstadoIds.Activo,
                    ObraOficinaStaff = ObraOficinaStaffIds.Staff,
                    ProjectIds = projectIds.ToArray(),
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
