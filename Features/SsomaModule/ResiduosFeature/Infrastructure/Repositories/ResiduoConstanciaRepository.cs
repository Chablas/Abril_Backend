using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Repositories;

public class ResiduoConstanciaRepository : IResiduoConstanciaRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public ResiduoConstanciaRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<ResiduoConstanciaDto>> ListarAsync(int? projectId, int? periodoAnio, int? periodoMes)
    {
        using var ctx = _factory.CreateDbContext();
        var query = ctx.Set<SsResiduoConstancia>().AsNoTracking().Include(c => c.EoRs).AsQueryable();
        if (projectId.HasValue) query = query.Where(c => c.ProjectId == projectId.Value);
        if (periodoAnio.HasValue) query = query.Where(c => c.PeriodoAnio == periodoAnio.Value);
        if (periodoMes.HasValue) query = query.Where(c => c.PeriodoMes == periodoMes.Value);

        return await query
            .OrderByDescending(c => c.PeriodoAnio).ThenByDescending(c => c.PeriodoMes)
            .Select(c => new ResiduoConstanciaDto
            {
                Id = c.Id,
                ProjectId = c.ProjectId,
                EoRsId = c.EoRsId,
                NombreEoRs = c.EoRs != null ? c.EoRs.RazonSocial : null,
                Contratista = c.Contratista,
                Destino = c.Destino,
                PeriodoAnio = c.PeriodoAnio,
                PeriodoMes = c.PeriodoMes,
                NumeroCertificado = c.NumeroCertificado,
                ArchivoUrl = c.ArchivoUrl,
            }).ToListAsync();
    }

    public async Task<SsResiduoConstancia?> ObtenerAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Set<SsResiduoConstancia>().AsNoTracking().Include(c => c.EoRs)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<int> CrearAsync(ResiduoConstanciaUpsertDto dto, string archivoUrl, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = new SsResiduoConstancia
        {
            ProjectId = dto.ProjectId,
            EoRsId = dto.EoRsId,
            Contratista = dto.Contratista,
            Destino = dto.Destino,
            PeriodoAnio = dto.PeriodoAnio,
            PeriodoMes = dto.PeriodoMes,
            NumeroCertificado = dto.NumeroCertificado,
            ArchivoUrl = archivoUrl,
            CreadoPor = userId,
        };
        ctx.Set<SsResiduoConstancia>().Add(entidad);
        await ctx.SaveChangesAsync();
        return entidad.Id;
    }

    public async Task<bool> ActualizarAsync(int id, ResiduoConstanciaUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoConstancia>().FindAsync(id);
        if (entidad == null) return false;

        entidad.ProjectId = dto.ProjectId;
        entidad.EoRsId = dto.EoRsId;
        entidad.Contratista = dto.Contratista;
        entidad.Destino = dto.Destino;
        entidad.PeriodoAnio = dto.PeriodoAnio;
        entidad.PeriodoMes = dto.PeriodoMes;
        entidad.NumeroCertificado = dto.NumeroCertificado;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoConstancia>().FindAsync(id);
        if (entidad == null) return false;
        ctx.Set<SsResiduoConstancia>().Remove(entidad);
        await ctx.SaveChangesAsync();
        return true;
    }
}
