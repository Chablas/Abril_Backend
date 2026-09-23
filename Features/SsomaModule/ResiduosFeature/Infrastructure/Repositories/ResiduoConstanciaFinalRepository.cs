using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Repositories;

public class ResiduoConstanciaFinalRepository : IResiduoConstanciaFinalRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public ResiduoConstanciaFinalRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<ResiduoConstanciaFinalDto>> ListarAsync(int? projectId)
    {
        using var ctx = _factory.CreateDbContext();
        var query = ctx.Set<SsResiduoConstanciaFinal>().AsNoTracking().AsQueryable();
        if (projectId.HasValue) query = query.Where(c => c.ProjectId == projectId.Value);

        return await query.OrderByDescending(c => c.FechaEmision)
            .Select(c => new ResiduoConstanciaFinalDto
            {
                Id = c.Id,
                ProjectId = c.ProjectId,
                FechaEmision = c.FechaEmision,
                ArchivoUrl = c.ArchivoUrl,
                Observaciones = c.Observaciones,
            }).ToListAsync();
    }

    public async Task<SsResiduoConstanciaFinal?> ObtenerAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Set<SsResiduoConstanciaFinal>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<int> CrearAsync(ResiduoConstanciaFinalUpsertDto dto, string archivoUrl, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = new SsResiduoConstanciaFinal
        {
            ProjectId = dto.ProjectId,
            FechaEmision = dto.FechaEmision,
            Observaciones = dto.Observaciones,
            ArchivoUrl = archivoUrl,
            CreadoPor = userId,
        };
        ctx.Set<SsResiduoConstanciaFinal>().Add(entidad);
        await ctx.SaveChangesAsync();
        return entidad.Id;
    }

    public async Task<bool> ActualizarAsync(int id, ResiduoConstanciaFinalUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoConstanciaFinal>().FindAsync(id);
        if (entidad == null) return false;
        entidad.ProjectId = dto.ProjectId;
        entidad.FechaEmision = dto.FechaEmision;
        entidad.Observaciones = dto.Observaciones;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoConstanciaFinal>().FindAsync(id);
        if (entidad == null) return false;
        ctx.Set<SsResiduoConstanciaFinal>().Remove(entidad);
        await ctx.SaveChangesAsync();
        return true;
    }
}
