using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Repositories;

public class ResiduoAutorizacionDmeRepository : IResiduoAutorizacionDmeRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public ResiduoAutorizacionDmeRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<ResiduoAutorizacionDmeDto>> ListarAsync(int? projectId, string? estado)
    {
        using var ctx = _factory.CreateDbContext();
        var query = ctx.Set<SsResiduoAutorizacionDme>().AsNoTracking()
            .Include(a => a.EscombreraDestino).AsQueryable();
        if (projectId.HasValue) query = query.Where(a => a.ProjectId == projectId.Value);
        if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(a => a.Estado == estado);

        return await query.OrderByDescending(a => a.VigenciaDesde).Select(a => new ResiduoAutorizacionDmeDto
        {
            Id = a.Id,
            ProjectId = a.ProjectId,
            Municipalidad = a.Municipalidad,
            NumeroResolucion = a.NumeroResolucion,
            EscombreraDestinoId = a.EscombreraDestinoId,
            NombreEscombreraDestino = a.EscombreraDestino != null ? a.EscombreraDestino.RazonSocial : null,
            VigenciaDesde = a.VigenciaDesde,
            VigenciaHasta = a.VigenciaHasta,
            PlacasAutorizadas = a.PlacasAutorizadas,
            Estado = a.Estado,
            ArchivoUrl = a.ArchivoUrl,
        }).ToListAsync();
    }

    public async Task<SsResiduoAutorizacionDme?> ObtenerAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Set<SsResiduoAutorizacionDme>().AsNoTracking()
            .Include(a => a.EscombreraDestino)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<int> CrearAsync(ResiduoAutorizacionDmeUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = new SsResiduoAutorizacionDme
        {
            ProjectId = dto.ProjectId,
            Municipalidad = dto.Municipalidad,
            NumeroResolucion = dto.NumeroResolucion,
            EscombreraDestinoId = dto.EscombreraDestinoId,
            VigenciaDesde = dto.VigenciaDesde,
            VigenciaHasta = dto.VigenciaHasta,
            PlacasAutorizadas = dto.PlacasAutorizadas,
            Estado = dto.Estado,
        };
        ctx.Set<SsResiduoAutorizacionDme>().Add(entidad);
        await ctx.SaveChangesAsync();
        return entidad.Id;
    }

    public async Task<bool> ActualizarAsync(int id, ResiduoAutorizacionDmeUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoAutorizacionDme>().FindAsync(id);
        if (entidad == null) return false;

        entidad.ProjectId = dto.ProjectId;
        entidad.Municipalidad = dto.Municipalidad;
        entidad.NumeroResolucion = dto.NumeroResolucion;
        entidad.EscombreraDestinoId = dto.EscombreraDestinoId;
        entidad.VigenciaDesde = dto.VigenciaDesde;
        entidad.VigenciaHasta = dto.VigenciaHasta;
        entidad.PlacasAutorizadas = dto.PlacasAutorizadas;
        entidad.Estado = dto.Estado;
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetArchivoAsync(int id, string archivoUrl)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoAutorizacionDme>().FindAsync(id);
        if (entidad == null) return false;
        entidad.ArchivoUrl = archivoUrl;
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AnularAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoAutorizacionDme>().FindAsync(id);
        if (entidad == null) return false;
        entidad.Estado = "ANULADA";
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }
}
