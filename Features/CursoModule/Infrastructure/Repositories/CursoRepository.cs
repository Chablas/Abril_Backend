using Abril_Backend.Features.CursoModule.Application.Interfaces;
using Abril_Backend.Features.CursoModule.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.CursoModule.Infrastructure.Repositories
{
    public class CursoRepository : ICursoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CursoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<Curso>> GetActivosPorRolAsync(int[] roleIds)
        {
            using var ctx = _factory.CreateDbContext();

            // Mismo patrón de LearningModule: un curso sin RolDestino es público para todos los
            // roles; si lo tiene, solo es visible si coincide con alguno de los roles del usuario
            // (los roles viajan en el JWT como texto, ver ClaimTypes.Role en JWTService).
            var roleValues = roleIds.Select(r => r.ToString()).ToArray();

            return await ctx.Cursos
                .Where(c => c.Activo && (c.RolDestino == null || c.RolDestino == "" || roleValues.Contains(c.RolDestino)))
                .OrderBy(c => c.Titulo)
                .ToListAsync();
        }

        public async Task<Curso?> GetByIdAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Cursos.FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<CursoSlide>> GetSlidesOrdenadasAsync(int cursoId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.CursoSlides
                .Where(s => s.CursoId == cursoId)
                .OrderBy(s => s.Orden)
                .ToListAsync();
        }

        public async Task<CursoSlide?> GetSlideByIdAsync(int slideId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.CursoSlides.FirstOrDefaultAsync(s => s.Id == slideId);
        }
    }
}
