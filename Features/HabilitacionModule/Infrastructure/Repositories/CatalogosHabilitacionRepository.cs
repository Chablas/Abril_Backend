using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class CatalogosHabilitacionRepository : ICatalogosHabilitacionRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CatalogosHabilitacionRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<SsItemTrabajador>> GetItemsTrabajadorAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsItemTrabajador
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToListAsync();
        }

        public async Task<List<SsItemEmpresa>> GetItemsEmpresaAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsItemEmpresa
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToListAsync();
        }

        public async Task<List<SsItemEquipo>> GetItemsEquipoAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsItemEquipo
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToListAsync();
        }

        public async Task<List<SsCriterioEvaluacion>> GetCriteriosEvaluacionAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsCriterioEvaluacion
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToListAsync();
        }

        public async Task<List<string>> GetAreasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.CatSubarea
                .Where(x => x.Activo)
                .Select(x => x.Area)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();
        }

        public async Task<List<CatSubarea>> GetSubareasAsync(string? area)
        {
            using var ctx = _factory.CreateDbContext();
            var query = ctx.CatSubarea.Where(x => x.Activo);
            if (!string.IsNullOrWhiteSpace(area))
                query = query.Where(x => x.Area == area);
            return await query
                .OrderBy(x => x.Subarea)
                .ToListAsync();
        }

        public async Task<List<CatCategoria>> GetCategoriasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.CatCategoria
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToListAsync();
        }

        public async Task<List<CatOcupacion>> GetOcupacionesAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.CatOcupacion
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToListAsync();
        }

        // ── Categorías CRUD ──────────────────────────────────────────
        public async Task<List<CatCategoria>> GetCategoriasTodasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.CatCategoria
                .OrderBy(x => x.Orden).ThenBy(x => x.Nombre)
                .ToListAsync();
        }

        public async Task<CatCategoria> CrearCategoriaAsync(string nombre)
        {
            using var ctx = _factory.CreateDbContext();
            var maxOrden = await ctx.CatCategoria.MaxAsync(x => (int?)x.Orden) ?? 0;
            var cat = new CatCategoria { Nombre = nombre, Orden = maxOrden + 1, Activo = true, CreatedAt = DateTime.UtcNow };
            ctx.CatCategoria.Add(cat);
            await ctx.SaveChangesAsync();
            return cat;
        }

        public async Task<CatCategoria> ActualizarCategoriaAsync(int id, string nombre)
        {
            using var ctx = _factory.CreateDbContext();
            var cat = await ctx.CatCategoria.FindAsync(id)
                ?? throw new AbrilException("Categoría no encontrada.", 404);
            cat.Nombre = nombre;
            await ctx.SaveChangesAsync();
            return cat;
        }

        public async Task ToggleCategoriaAsync(int id, bool activo)
        {
            using var ctx = _factory.CreateDbContext();
            var cat = await ctx.CatCategoria.FindAsync(id)
                ?? throw new AbrilException("Categoría no encontrada.", 404);
            cat.Activo = activo;
            await ctx.SaveChangesAsync();
        }

        // ── Ocupaciones CRUD ─────────────────────────────────────────
        public async Task<List<CatOcupacion>> GetOcupacionesTodasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.CatOcupacion
                .OrderBy(x => x.Orden).ThenBy(x => x.Nombre)
                .ToListAsync();
        }

        public async Task<CatOcupacion> CrearOcupacionAsync(string nombre)
        {
            using var ctx = _factory.CreateDbContext();
            var maxOrden = await ctx.CatOcupacion.MaxAsync(x => (int?)x.Orden) ?? 0;
            var ocu = new CatOcupacion { Nombre = nombre, Orden = maxOrden + 1, Activo = true, CreatedAt = DateTime.UtcNow };
            ctx.CatOcupacion.Add(ocu);
            await ctx.SaveChangesAsync();
            return ocu;
        }

        public async Task<CatOcupacion> ActualizarOcupacionAsync(int id, string nombre)
        {
            using var ctx = _factory.CreateDbContext();
            var ocu = await ctx.CatOcupacion.FindAsync(id)
                ?? throw new AbrilException("Ocupación no encontrada.", 404);
            ocu.Nombre = nombre;
            await ctx.SaveChangesAsync();
            return ocu;
        }

        public async Task ToggleOcupacionAsync(int id, bool activo)
        {
            using var ctx = _factory.CreateDbContext();
            var ocu = await ctx.CatOcupacion.FindAsync(id)
                ?? throw new AbrilException("Ocupación no encontrada.", 404);
            ocu.Activo = activo;
            await ctx.SaveChangesAsync();
        }
    }
}
