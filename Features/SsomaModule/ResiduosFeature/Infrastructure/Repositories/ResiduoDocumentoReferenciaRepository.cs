using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Repositories;

public class ResiduoDocumentoReferenciaRepository : IResiduoDocumentoReferenciaRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public ResiduoDocumentoReferenciaRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<ResiduoDocumentoReferenciaDto>> ListarAsync(string? tipo, bool? activo)
    {
        using var ctx = _factory.CreateDbContext();
        var query = ctx.Set<SsResiduoDocumentoReferencia>().AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(tipo)) query = query.Where(d => d.Tipo == tipo);
        if (activo.HasValue) query = query.Where(d => d.Activo == activo.Value);

        return await query.OrderBy(d => d.Tipo).ThenBy(d => d.Nombre).Select(d => new ResiduoDocumentoReferenciaDto
        {
            Id = d.Id,
            Tipo = d.Tipo,
            Nombre = d.Nombre,
            Descripcion = d.Descripcion,
            ArchivoUrl = d.ArchivoUrl,
            Version = d.Version,
            Activo = d.Activo,
        }).ToListAsync();
    }

    public async Task<SsResiduoDocumentoReferencia?> ObtenerAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Set<SsResiduoDocumentoReferencia>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<int> CrearAsync(ResiduoDocumentoReferenciaUpsertDto dto, string archivoUrl, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = new SsResiduoDocumentoReferencia
        {
            Tipo = dto.Tipo,
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            Version = dto.Version,
            Activo = dto.Activo,
            ArchivoUrl = archivoUrl,
            CreadoPor = userId,
        };
        ctx.Set<SsResiduoDocumentoReferencia>().Add(entidad);
        await ctx.SaveChangesAsync();
        return entidad.Id;
    }

    public async Task<bool> ActualizarAsync(int id, ResiduoDocumentoReferenciaUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoDocumentoReferencia>().FindAsync(id);
        if (entidad == null) return false;

        entidad.Tipo = dto.Tipo;
        entidad.Nombre = dto.Nombre;
        entidad.Descripcion = dto.Descripcion;
        entidad.Version = dto.Version;
        entidad.Activo = dto.Activo;
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetArchivoAsync(int id, string archivoUrl)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoDocumentoReferencia>().FindAsync(id);
        if (entidad == null) return false;
        entidad.ArchivoUrl = archivoUrl;
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DesactivarAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoDocumentoReferencia>().FindAsync(id);
        if (entidad == null) return false;
        entidad.Activo = false;
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }
}
