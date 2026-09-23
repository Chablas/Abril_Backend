using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Repositories;

public class ResiduoTipoRepository : IResiduoTipoRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public ResiduoTipoRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<ResiduoTipoDto>> ListarAsync(bool? activo)
    {
        using var ctx = _factory.CreateDbContext();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = ctx.Set<SsResiduoTipo>().AsNoTracking().Include(t => t.Factores).AsQueryable();
        if (activo.HasValue) query = query.Where(t => t.Activo == activo.Value);

        return await query
            .OrderBy(t => t.Nombre)
            .Select(t => new ResiduoTipoDto
            {
                Id = t.Id,
                CodigoSigersol = t.CodigoSigersol,
                Nombre = t.Nombre,
                EsPeligroso = t.EsPeligroso,
                Activo = t.Activo,
                FactorVigente = t.Factores
                    .Where(f => f.VigenciaDesde <= hoy && (f.VigenciaHasta == null || f.VigenciaHasta >= hoy))
                    .OrderByDescending(f => f.VigenciaDesde)
                    .Select(f => (decimal?)f.FactorM3aTon)
                    .FirstOrDefault(),
            })
            .ToListAsync();
    }

    public async Task<SsResiduoTipo?> ObtenerAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Set<SsResiduoTipo>().AsNoTracking().Include(t => t.Factores)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<int> CrearAsync(ResiduoTipoUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = new SsResiduoTipo
        {
            CodigoSigersol = dto.CodigoSigersol,
            Nombre = dto.Nombre,
            EsPeligroso = dto.EsPeligroso,
            Activo = dto.Activo,
        };
        ctx.Set<SsResiduoTipo>().Add(entidad);
        await ctx.SaveChangesAsync();
        return entidad.Id;
    }

    public async Task<bool> ActualizarAsync(int id, ResiduoTipoUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoTipo>().FindAsync(id);
        if (entidad == null) return false;

        entidad.CodigoSigersol = dto.CodigoSigersol;
        entidad.Nombre = dto.Nombre;
        entidad.EsPeligroso = dto.EsPeligroso;
        entidad.Activo = dto.Activo;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DesactivarAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoTipo>().FindAsync(id);
        if (entidad == null) return false;
        entidad.Activo = false;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<List<ResiduoTipoFactorDto>> ListarFactoresAsync(int residuoTipoId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Set<SsResiduoTipoFactor>().AsNoTracking()
            .Where(f => f.ResiduoTipoId == residuoTipoId)
            .OrderByDescending(f => f.VigenciaDesde)
            .Select(f => new ResiduoTipoFactorDto
            {
                Id = f.Id,
                ResiduoTipoId = f.ResiduoTipoId,
                FactorM3aTon = f.FactorM3aTon,
                VigenciaDesde = f.VigenciaDesde,
                VigenciaHasta = f.VigenciaHasta,
            }).ToListAsync();
    }

    public async Task<int> CrearFactorAsync(int residuoTipoId, ResiduoTipoFactorUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = new SsResiduoTipoFactor
        {
            ResiduoTipoId = residuoTipoId,
            FactorM3aTon = dto.FactorM3aTon,
            VigenciaDesde = dto.VigenciaDesde,
            VigenciaHasta = dto.VigenciaHasta,
        };
        ctx.Set<SsResiduoTipoFactor>().Add(entidad);
        await ctx.SaveChangesAsync();
        return entidad.Id;
    }

    public async Task<bool> ActualizarFactorAsync(int factorId, ResiduoTipoFactorUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoTipoFactor>().FindAsync(factorId);
        if (entidad == null) return false;
        entidad.FactorM3aTon = dto.FactorM3aTon;
        entidad.VigenciaDesde = dto.VigenciaDesde;
        entidad.VigenciaHasta = dto.VigenciaHasta;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> EliminarFactorAsync(int factorId)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoTipoFactor>().FindAsync(factorId);
        if (entidad == null) return false;
        ctx.Set<SsResiduoTipoFactor>().Remove(entidad);
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<decimal?> ObtenerFactorVigenteAsync(int residuoTipoId, DateOnly fecha)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Set<SsResiduoTipoFactor>().AsNoTracking()
            .Where(f => f.ResiduoTipoId == residuoTipoId
                && f.VigenciaDesde <= fecha
                && (f.VigenciaHasta == null || f.VigenciaHasta >= fecha))
            .OrderByDescending(f => f.VigenciaDesde)
            .Select(f => (decimal?)f.FactorM3aTon)
            .FirstOrDefaultAsync();
    }
}
