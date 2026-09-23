using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Repositories;

public class ResiduoEoRsRepository : IResiduoEoRsRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public ResiduoEoRsRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<ResiduoEoRsDto>> ListarAsync(bool? activo, string? tipoOperador)
    {
        using var ctx = _factory.CreateDbContext();
        var query = ctx.Set<SsResiduoEoRs>().AsNoTracking().AsQueryable();
        if (activo.HasValue) query = query.Where(e => e.Activo == activo.Value);
        if (!string.IsNullOrWhiteSpace(tipoOperador)) query = query.Where(e => e.TipoOperador == tipoOperador);

        return await query.OrderBy(e => e.RazonSocial).Select(e => new ResiduoEoRsDto
        {
            Id = e.Id,
            Ruc = e.Ruc,
            RazonSocial = e.RazonSocial,
            TipoOperador = e.TipoOperador,
            NumeroRegistroMinam = e.NumeroRegistroMinam,
            VigenciaRegistro = e.VigenciaRegistro,
            Direccion = e.Direccion,
            AmbitoGestion = e.AmbitoGestion,
            Activo = e.Activo,
        }).ToListAsync();
    }

    public async Task<SsResiduoEoRs?> ObtenerAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Set<SsResiduoEoRs>().AsNoTracking().Include(e => e.Documentos)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<int> CrearAsync(ResiduoEoRsUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = new SsResiduoEoRs
        {
            Ruc = dto.Ruc,
            RazonSocial = dto.RazonSocial,
            TipoOperador = dto.TipoOperador,
            NumeroRegistroMinam = dto.NumeroRegistroMinam,
            VigenciaRegistro = dto.VigenciaRegistro,
            Direccion = dto.Direccion,
            AmbitoGestion = dto.AmbitoGestion,
            Activo = dto.Activo,
        };
        ctx.Set<SsResiduoEoRs>().Add(entidad);
        await ctx.SaveChangesAsync();
        return entidad.Id;
    }

    public async Task<bool> ActualizarAsync(int id, ResiduoEoRsUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoEoRs>().FindAsync(id);
        if (entidad == null) return false;

        entidad.Ruc = dto.Ruc;
        entidad.RazonSocial = dto.RazonSocial;
        entidad.TipoOperador = dto.TipoOperador;
        entidad.NumeroRegistroMinam = dto.NumeroRegistroMinam;
        entidad.VigenciaRegistro = dto.VigenciaRegistro;
        entidad.Direccion = dto.Direccion;
        entidad.AmbitoGestion = dto.AmbitoGestion;
        entidad.Activo = dto.Activo;
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DesactivarAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoEoRs>().FindAsync(id);
        if (entidad == null) return false;
        entidad.Activo = false;
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<List<ResiduoEoRsDocumentoDto>> ListarDocumentosAsync(int eoRsId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Set<SsResiduoEoRsDocumento>().AsNoTracking()
            .Where(d => d.EoRsId == eoRsId)
            .OrderByDescending(d => d.CreadoEn)
            .Select(d => new ResiduoEoRsDocumentoDto
            {
                Id = d.Id,
                EoRsId = d.EoRsId,
                TipoDocumento = d.TipoDocumento,
                Numero = d.Numero,
                VigenciaDesde = d.VigenciaDesde,
                VigenciaHasta = d.VigenciaHasta,
                ArchivoUrl = d.ArchivoUrl,
                Cumple = d.Cumple,
            }).ToListAsync();
    }

    public async Task<int> CrearDocumentoAsync(int eoRsId, ResiduoEoRsDocumentoUpsertDto dto, string? archivoUrl)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = new SsResiduoEoRsDocumento
        {
            EoRsId = eoRsId,
            TipoDocumento = dto.TipoDocumento,
            Numero = dto.Numero,
            VigenciaDesde = dto.VigenciaDesde,
            VigenciaHasta = dto.VigenciaHasta,
            ArchivoUrl = archivoUrl,
            Cumple = dto.Cumple,
        };
        ctx.Set<SsResiduoEoRsDocumento>().Add(entidad);
        await ctx.SaveChangesAsync();
        return entidad.Id;
    }

    public async Task<bool> ActualizarDocumentoAsync(int documentoId, ResiduoEoRsDocumentoUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoEoRsDocumento>().FindAsync(documentoId);
        if (entidad == null) return false;

        entidad.TipoDocumento = dto.TipoDocumento;
        entidad.Numero = dto.Numero;
        entidad.VigenciaDesde = dto.VigenciaDesde;
        entidad.VigenciaHasta = dto.VigenciaHasta;
        entidad.Cumple = dto.Cumple;
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetArchivoDocumentoAsync(int documentoId, string archivoUrl)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoEoRsDocumento>().FindAsync(documentoId);
        if (entidad == null) return false;
        entidad.ArchivoUrl = archivoUrl;
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> EliminarDocumentoAsync(int documentoId)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoEoRsDocumento>().FindAsync(documentoId);
        if (entidad == null) return false;
        ctx.Set<SsResiduoEoRsDocumento>().Remove(entidad);
        await ctx.SaveChangesAsync();
        return true;
    }
}
