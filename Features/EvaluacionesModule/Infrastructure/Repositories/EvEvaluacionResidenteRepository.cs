using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
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

        private static async Task<List<EvEvaluacionResidenteResponseDto>> MapToResponseDtos(
            AppDbContext ctx, List<EvEvaluacionResidente> evals)
        {
            var userIds = evals
                .SelectMany(e => new[] { e.EvaluadorUserId, e.EvaluadoUserId })
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
                EvaluadorUserId = e.EvaluadorUserId,
                EvaluadorNombre = persons.GetValueOrDefault(e.EvaluadorUserId, ""),
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
