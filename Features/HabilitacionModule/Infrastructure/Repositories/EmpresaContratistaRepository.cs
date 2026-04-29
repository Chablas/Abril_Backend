using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class EmpresaContratistaRepository : IEmpresaContratistaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public EmpresaContratistaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<SsEmpresaContratista?> GetByIdAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsEmpresaContratista.FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<(List<SsEmpresaContratista> Items, int Total)> GetPagedAsync(
            string? search, string? tipo, bool? activo, int page, int pageSize)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.SsEmpresaContratista.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(e =>
                    e.RazonSocial.ToLower().Contains(s) ||
                    (e.NombreComercial != null && e.NombreComercial.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(tipo))
                query = query.Where(e => e.Tipo == tipo);

            if (activo.HasValue)
                query = query.Where(e => e.Activo == activo.Value);

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(e => e.RazonSocial)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<SsEmpresaContratista> CreateAsync(SsEmpresaContratista empresa)
        {
            using var ctx = _factory.CreateDbContext();
            ctx.SsEmpresaContratista.Add(empresa);
            await ctx.SaveChangesAsync();
            return empresa;
        }

        public async Task<SsEmpresaContratista> UpdateAsync(SsEmpresaContratista empresa)
        {
            using var ctx = _factory.CreateDbContext();
            ctx.SsEmpresaContratista.Update(empresa);
            await ctx.SaveChangesAsync();
            return empresa;
        }

        public async Task<List<SsEmpresaProyecto>> GetProyectosAsync(int empresaId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsEmpresaProyecto
                .Include(ep => ep.Proyecto)
                .Where(ep => ep.EmpresaId == empresaId)
                .ToListAsync();
        }

        public async Task AddProyectoAsync(SsEmpresaProyecto ep)
        {
            using var ctx = _factory.CreateDbContext();
            ctx.SsEmpresaProyecto.Add(ep);
            await ctx.SaveChangesAsync();
        }

        public async Task RemoveProyectoAsync(int empresaId, int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            var ep = await ctx.SsEmpresaProyecto
                .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.ProyectoId == proyectoId);
            if (ep is null) return;
            ctx.SsEmpresaProyecto.Remove(ep);
            await ctx.SaveChangesAsync();
        }
    }
}
