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

        public async Task<List<Curso>> GetTodosAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Cursos.OrderBy(c => c.Titulo).ToListAsync();
        }

        public async Task<Curso> CreateCursoAsync(Curso curso)
        {
            using var ctx = _factory.CreateDbContext();
            ctx.Cursos.Add(curso);
            await ctx.SaveChangesAsync();
            return curso;
        }

        public async Task UpdateCursoAsync(int id, Curso datos)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.Cursos.FirstOrDefaultAsync(c => c.Id == id)
                ?? throw new InvalidOperationException("Curso no encontrado.");

            entity.Titulo = datos.Titulo;
            entity.Descripcion = datos.Descripcion;
            entity.CategoriaNombre = datos.CategoriaNombre;
            entity.RolDestino = datos.RolDestino;
            entity.NotaMinimaAprobacion = datos.NotaMinimaAprobacion;
            entity.Activo = datos.Activo;
            entity.ColorTema = datos.ColorTema;
            entity.UpdatedAt = DateTime.UtcNow;

            await ctx.SaveChangesAsync();
        }

        public async Task<CursoSlide> CreateSlideAsync(CursoSlide slide)
        {
            using var ctx = _factory.CreateDbContext();
            ctx.CursoSlides.Add(slide);
            await ctx.SaveChangesAsync();
            return slide;
        }

        public async Task UpdateSlideAsync(int slideId, CursoSlide datos)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.CursoSlides.FirstOrDefaultAsync(s => s.Id == slideId)
                ?? throw new InvalidOperationException("Slide no encontrada.");

            entity.Orden = datos.Orden;
            entity.TipoCodigo = datos.TipoCodigo;
            entity.EsEvaluable = datos.EsEvaluable;
            entity.Puntaje = datos.Puntaje;
            entity.ModoCorreccion = datos.ModoCorreccion;
            entity.ConfiguracionJson = datos.ConfiguracionJson;
            entity.UpdatedAt = DateTime.UtcNow;

            await ctx.SaveChangesAsync();
        }

        public async Task<CursoSlide> DuplicarSlideAsync(int slideId, int? cursoDestinoId)
        {
            using var ctx = _factory.CreateDbContext();
            var original = await ctx.CursoSlides.FirstOrDefaultAsync(s => s.Id == slideId)
                ?? throw new InvalidOperationException("Slide no encontrada.");

            var cursoId = cursoDestinoId ?? original.CursoId;
            var maxOrden = await ctx.CursoSlides.Where(s => s.CursoId == cursoId).Select(s => (int?)s.Orden).MaxAsync() ?? 0;

            var copia = new CursoSlide
            {
                CursoId = cursoId,
                Orden = maxOrden + 1,
                TipoCodigo = original.TipoCodigo,
                EsEvaluable = original.EsEvaluable,
                Puntaje = original.Puntaje,
                ModoCorreccion = original.ModoCorreccion,
                ConfiguracionJson = original.ConfiguracionJson,
            };

            ctx.CursoSlides.Add(copia);
            await ctx.SaveChangesAsync();
            return copia;
        }

        public async Task DeleteSlideAsync(int slideId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.CursoSlides.FirstOrDefaultAsync(s => s.Id == slideId);
            if (entity == null) return;
            ctx.CursoSlides.Remove(entity);
            await ctx.SaveChangesAsync();
        }
    }
}
