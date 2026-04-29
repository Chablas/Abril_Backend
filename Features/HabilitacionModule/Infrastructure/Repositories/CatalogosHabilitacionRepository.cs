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
    }
}
