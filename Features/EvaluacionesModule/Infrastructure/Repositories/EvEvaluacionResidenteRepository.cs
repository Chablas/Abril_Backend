using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Repositories
{
    public class EvEvaluacionResidenteRepository : IEvEvaluacionResidenteRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public EvEvaluacionResidenteRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<EvEvaluacionResidente> CreateAsync(EvEvaluacionResidente eval, List<EvEvaluacionResidenteDetalle> detalles)
        {
            using var ctx = _factory.CreateDbContext();
            var puntajesValidos = detalles
                .Where(d => !d.EsNa && d.Puntaje.HasValue)
                .Select(d => d.Puntaje!.Value)
                .ToList();
            eval.Nota = puntajesValidos.Count != 0 ? Math.Round((decimal)puntajesValidos.Average() * 4, 2) : 0;
            eval.Detalles = detalles;
            ctx.EvEvaluacionesResidente.Add(eval);
            await ctx.SaveChangesAsync();
            return eval;
        }

        public async Task<bool> ExisteAsync(int periodoId, int evaluadorUserId, int evaluadoUserId, string areaNombre)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.EvEvaluacionesResidente.AnyAsync(e =>
                e.PeriodoId == periodoId &&
                e.EvaluadorUserId == evaluadorUserId &&
                e.EvaluadoUserId == evaluadoUserId &&
                e.AreaNombre == areaNombre);
        }

        public async Task<List<EvEvaluacionResidenteResponseDto>> GetByPeriodoAsync(int periodoId)
        {
            using var ctx = _factory.CreateDbContext();
            var evals = await ctx.EvEvaluacionesResidente
                .Where(e => e.PeriodoId == periodoId)
                .Include(e => e.Detalles)
                .Include(e => e.Periodo)
                .Include(e => e.Project)
                .ToListAsync();

            return await MapToResponseDtos(ctx, evals);
        }

        public async Task<List<EvEvaluacionResidenteResponseDto>> GetByEvaluadorAsync(int evaluadorUserId, int periodoId)
        {
            using var ctx = _factory.CreateDbContext();
            var evals = await ctx.EvEvaluacionesResidente
                .Where(e => e.EvaluadorUserId == evaluadorUserId && e.PeriodoId == periodoId)
                .Include(e => e.Detalles)
                .Include(e => e.Periodo)
                .Include(e => e.Project)
                .ToListAsync();

            return await MapToResponseDtos(ctx, evals);
        }

        public async Task<List<EvEvaluacionResidenteResponseDto>> GetByEvaluadoAsync(int evaluadoUserId, int periodoId)
        {
            using var ctx = _factory.CreateDbContext();
            var evals = await ctx.EvEvaluacionesResidente
                .Where(e => e.EvaluadoUserId == evaluadoUserId && e.PeriodoId == periodoId)
                .Include(e => e.Detalles)
                .Include(e => e.Periodo)
                .Include(e => e.Project)
                .ToListAsync();

            return await MapToResponseDtos(ctx, evals);
        }

        public async Task<EvEvaluacionResidenteResponseDto?> GetDetalleAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            var eval = await ctx.EvEvaluacionesResidente
                .Where(e => e.Id == id)
                .Include(e => e.Detalles)
                .Include(e => e.Periodo)
                .Include(e => e.Project)
                .FirstOrDefaultAsync();

            if (eval == null) return null;

            var dtos = await MapToResponseDtos(ctx, [eval]);
            return dtos.FirstOrDefault();
        }

        public async Task<List<ResidenteEvaluableDto>> GetResidentesEvaluablesAsync(int evaluadorUserId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            // Paso 1: obtener obra_oficina del evaluador
            var obraOficina = await conn.QueryFirstOrDefaultAsync<string>(
                @"SELECT w.obra_oficina
                  FROM workers w
                  JOIN person p ON p.person_id = w.person_id
                  WHERE p.user_id = @EvaluadorUserId
                  LIMIT 1",
                new { EvaluadorUserId = evaluadorUserId });

            var puedeVerTodos = string.Equals(obraOficina, "Oficina Central", StringComparison.OrdinalIgnoreCase);

            // Paso 2: residentes según scope
            const string selectBase = @"
                SELECT DISTINCT
                    p.full_name            AS NombreCompleto,
                    p.user_id              AS UserId,
                    pr.project_id          AS ProjectId,
                    pr.project_description AS ProjectNombre,
                    w.area                 AS Area,
                    w.subarea              AS Subarea
                FROM workers w
                JOIN person p   ON p.person_id    = w.person_id
                JOIN app_user u ON u.user_id       = p.user_id
                JOIN project pr ON pr.contributor_id = w.contributor_id
                WHERE w.ocupacion = 'Residencia'
                  AND w.estado   != 'Retirado'
                  AND u.active    = true";

            var sql = puedeVerTodos
                ? selectBase + "\nORDER BY p.full_name"
                : selectBase + @"
                  AND pr.project_id = (
                      SELECT pr2.project_id
                      FROM workers w2
                      JOIN person p2  ON p2.person_id    = w2.person_id
                      JOIN project pr2 ON pr2.contributor_id = w2.contributor_id
                      WHERE p2.user_id = @EvaluadorUserId
                      LIMIT 1
                  )
                ORDER BY p.full_name";

            var result = (await conn.QueryAsync<ResidenteEvaluableDto>(sql, new { EvaluadorUserId = evaluadorUserId })).ToList();
            result.ForEach(r => r.PuedeVerTodos = puedeVerTodos);
            return result;
        }

        public async Task<string?> GetMiSubareaAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();
            return await conn.QueryFirstOrDefaultAsync<string>(
                @"SELECT w.subarea
                  FROM workers w
                  JOIN person p ON p.person_id = w.person_id
                  WHERE p.user_id = @UserId
                  LIMIT 1",
                new { UserId = userId });
        }

        private static async Task<List<EvEvaluacionResidenteResponseDto>> MapToResponseDtos(
            AppDbContext ctx, List<EvEvaluacionResidente> evals)
        {
            var userIds = evals
                .SelectMany(e => new int?[] { e.EvaluadorUserId, e.EvaluadoUserId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var persons = await ctx.Person
                .Where(p => p.UserId.HasValue && userIds.Contains(p.UserId.Value))
                .ToDictionaryAsync(p => p.UserId!.Value, p => p.FullName ?? "");

            return evals.Select(e => new EvEvaluacionResidenteResponseDto
            {
                Id = e.Id,
                PeriodoId = e.PeriodoId,
                NombreMes = e.Periodo != null
                    ? new DateTime(e.Periodo.Anio, e.Periodo.Mes, 1)
                        .ToString("MMMM", new System.Globalization.CultureInfo("es-PE"))
                    : "",
                EvaluadorUserId = e.EvaluadorUserId ?? 0,
                EvaluadorNombre = e.EvaluadorUserId.HasValue ? persons.GetValueOrDefault(e.EvaluadorUserId.Value, "") : "",
                EvaluadoUserId = e.EvaluadoUserId,
                EvaluadoNombre = persons.GetValueOrDefault(e.EvaluadoUserId, ""),
                ProjectId = e.ProjectId,
                ProjectNombre = e.Project?.ProjectDescription,
                AreaNombre = e.AreaNombre,
                Nota = e.Nota,
                Comentario = e.Comentario,
                NoAplica = e.NoAplica,
                NoAplicaMotivo = e.NoAplicaMotivo,
                CreatedAt = e.CreatedAt,
                Detalles = e.Detalles.Select(d => new EvDetalleResponseDto
                {
                    Id = d.Id,
                    PlantillaId = d.PlantillaId,
                    Criterio = d.Criterio,
                    Puntaje = d.Puntaje,
                    EsNa = d.EsNa
                }).ToList()
            }).ToList();
        }
    }
}
