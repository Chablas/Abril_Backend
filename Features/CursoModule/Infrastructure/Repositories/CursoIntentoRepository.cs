using Abril_Backend.Features.CursoModule.Application.Interfaces;
using Abril_Backend.Features.CursoModule.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.CursoModule.Infrastructure.Repositories
{
    public class CursoIntentoRepository : ICursoIntentoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CursoIntentoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<CursoIntento> CrearIntentoAsync(CursoIntento intento, CursoIntentoEvidencia evidencia)
        {
            using var ctx = _factory.CreateDbContext();
            ctx.CursoIntentos.Add(intento);
            await ctx.SaveChangesAsync();

            evidencia.CursoIntentoId = intento.Id;
            ctx.CursoIntentoEvidencias.Add(evidencia);
            await ctx.SaveChangesAsync();

            return intento;
        }

        public async Task<CursoIntento?> GetByIdAsync(int intentoId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.CursoIntentos.FirstOrDefaultAsync(i => i.Id == intentoId);
        }

        public async Task<List<CursoIntento>> GetPorUsuarioAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.CursoIntentos
                .Where(i => i.UserId == userId)
                .OrderByDescending(i => i.FechaInicio)
                .ToListAsync();
        }

        public async Task<List<CursoIntento>> GetHistorialAsync(int? cursoId, DateTime? desde, DateTime? hasta)
        {
            using var ctx = _factory.CreateDbContext();
            var query = ctx.CursoIntentos.AsQueryable();
            if (cursoId.HasValue) query = query.Where(i => i.CursoId == cursoId.Value);
            if (desde.HasValue) query = query.Where(i => i.FechaInicio >= desde.Value);
            if (hasta.HasValue) query = query.Where(i => i.FechaInicio <= hasta.Value);

            return await query.OrderByDescending(i => i.FechaInicio).ToListAsync();
        }

        public async Task<Dictionary<int, string>> GetNombresTrabajadoresAsync(int[] userIds)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Person
                .Where(p => p.UserId.HasValue && userIds.Contains(p.UserId.Value))
                .ToDictionaryAsync(p => p.UserId!.Value, p => p.FullName ?? "(sin nombre)");
        }

        public async Task<CursoIntentoRespuesta> GuardarRespuestaAsync(CursoIntentoRespuesta respuesta)
        {
            using var ctx = _factory.CreateDbContext();
            ctx.CursoIntentoRespuestas.Add(respuesta);
            await ctx.SaveChangesAsync();
            return respuesta;
        }

        public async Task<List<CursoIntentoRespuesta>> GetRespuestasAsync(int intentoId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.CursoIntentoRespuestas
                .Where(r => r.CursoIntentoId == intentoId)
                .OrderBy(r => r.Id)
                .ToListAsync();
        }

        public async Task<CursoIntentoEvidencia?> GetEvidenciaAsync(int intentoId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.CursoIntentoEvidencias.FirstOrDefaultAsync(e => e.CursoIntentoId == intentoId);
        }

        public async Task FinalizarAsync(CursoIntento intento, CursoIntentoEvidencia evidencia)
        {
            using var ctx = _factory.CreateDbContext();

            // El intento y la evidencia llegan de lecturas hechas en otro DbContext (u otro momento);
            // se reatachan explícitamente para no arrastrar timestamps con Kind=Unspecified hacia
            // columnas timestamptz (mismo cuidado que EvPeriodoRepository.UpdateAsync).
            if (intento.FechaInicio.Kind == DateTimeKind.Unspecified)
                intento.FechaInicio = DateTime.SpecifyKind(intento.FechaInicio, DateTimeKind.Utc);
            if (evidencia.CreatedAt.Kind == DateTimeKind.Unspecified)
                evidencia.CreatedAt = DateTime.SpecifyKind(evidencia.CreatedAt, DateTimeKind.Utc);

            ctx.CursoIntentos.Update(intento);
            ctx.CursoIntentoEvidencias.Update(evidencia);
            await ctx.SaveChangesAsync();
        }
    }
}
