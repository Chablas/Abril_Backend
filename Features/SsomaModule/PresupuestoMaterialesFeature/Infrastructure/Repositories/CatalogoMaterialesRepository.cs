using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

public class CatalogoMaterialesRepository : ICatalogoMaterialesRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public CatalogoMaterialesRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<SsMaterialTipo>> GetTiposAsync()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsMaterialTipo.AsNoTracking().ToListAsync();
    }

    public async Task<List<SsMaterialFamilia>> GetFamiliasAsync()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsMaterialFamilia.AsNoTracking().ToListAsync();
    }

    public async Task<List<SsMaterialItem>> GetItemsAsync()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsMaterialItem.AsNoTracking().ToListAsync();
    }

    public async Task<List<SsMaterialAlias>> GetAliasesAsync()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsMaterialAlias.AsNoTracking().ToListAsync();
    }

    public async Task<SsMaterialTipo> GetOrCreateTipoAsync(string nombre)
    {
        using var ctx = _factory.CreateDbContext();
        var existente = await ctx.SsMaterialTipo.FirstOrDefaultAsync(t => t.Nombre == nombre);
        if (existente != null) return existente;

        var nuevo = new SsMaterialTipo { Nombre = nombre, Activo = true };
        ctx.SsMaterialTipo.Add(nuevo);
        await ctx.SaveChangesAsync();
        return nuevo;
    }

    public async Task<(SsMaterialFamilia Familia, bool Creada)> GetOrCreateFamiliaAsync(
        string nombre, string nombreNormalizado, int tipoId, string variableBase, bool perteneceSsoma)
    {
        using var ctx = _factory.CreateDbContext();
        var existente = await ctx.SsMaterialFamilia
            .FirstOrDefaultAsync(f => f.NombreNormalizado == nombreNormalizado);
        if (existente != null) return (existente, false);

        var nueva = new SsMaterialFamilia
        {
            Nombre = nombre,
            NombreNormalizado = nombreNormalizado,
            TipoId = tipoId,
            VariableBase = variableBase,
            PerteneceSsoma = perteneceSsoma,
            Activo = true,
            CreadoEn = DateTimeOffset.UtcNow
        };
        ctx.SsMaterialFamilia.Add(nueva);
        await ctx.SaveChangesAsync();
        return (nueva, true);
    }

    public async Task<(SsMaterialItem Item, bool Creado)> GetOrCreateItemAsync(
        string nombre, string nombreNormalizado, int familiaId, string? talla, string? dimensionNorm, bool noUsar)
    {
        using var ctx = _factory.CreateDbContext();
        var existente = await ctx.SsMaterialItem
            .FirstOrDefaultAsync(i => i.NombreNormalizado == nombreNormalizado && i.FamiliaId == familiaId);
        if (existente != null) return (existente, false);

        var nuevo = new SsMaterialItem
        {
            Nombre = nombre,
            NombreNormalizado = nombreNormalizado,
            FamiliaId = familiaId,
            Talla = talla,
            DimensionNorm = dimensionNorm,
            NoUsar = noUsar,
            Activo = !noUsar,
            CreadoEn = DateTimeOffset.UtcNow
        };
        ctx.SsMaterialItem.Add(nuevo);
        await ctx.SaveChangesAsync();
        return (nuevo, true);
    }

    public async Task<bool> CreateAliasIfNotExistsAsync(
        string textoCrudo, string textoCrudoNorm, int itemId, string origen, decimal? confianza)
    {
        using var ctx = _factory.CreateDbContext();
        var existe = await ctx.SsMaterialAlias.AnyAsync(a => a.TextoCrudoNorm == textoCrudoNorm);
        if (existe) return false;

        ctx.SsMaterialAlias.Add(new SsMaterialAlias
        {
            TextoCrudo = textoCrudo,
            TextoCrudoNorm = textoCrudoNorm,
            ItemId = itemId,
            Origen = origen,
            Confianza = confianza,
            CreadoEn = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();
        return true;
    }
}
