using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Repositories;

public class ResiduoDeclaracionRepository : IResiduoDeclaracionRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public ResiduoDeclaracionRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<ResiduoDeclaracionDto>> ListarAsync(int? contributorId, int? periodoAnio)
    {
        using var ctx = _factory.CreateDbContext();
        var query = ctx.Set<SsResiduoDeclaracion>().AsNoTracking()
            .Include(d => d.Contributor)
            .Include(d => d.Detalles).ThenInclude(det => det.ResiduoTipo)
            .Include(d => d.EoRsIntervinientes).ThenInclude(e => e.EoRs)
            .AsQueryable();

        if (contributorId.HasValue) query = query.Where(d => d.ContributorId == contributorId.Value);
        if (periodoAnio.HasValue) query = query.Where(d => d.PeriodoAnio == periodoAnio.Value);

        var lista = await query.OrderByDescending(d => d.PeriodoAnio).ToListAsync();
        return lista.Select(Proyectar).ToList();
    }

    private static ResiduoDeclaracionDto Proyectar(SsResiduoDeclaracion d) => new()
    {
        Id = d.Id,
        ContributorId = d.ContributorId,
        NombreContributor = d.Contributor?.ContributorName,
        PeriodoAnio = d.PeriodoAnio,
        Estado = d.Estado,
        FechaPresentacion = d.FechaPresentacion,
        ArchivoConstanciaUrl = d.ArchivoConstanciaUrl,
        Detalles = d.Detalles.Select(det => new ResiduoDeclaracionDetalleDto
        {
            Id = det.Id,
            ResiduoTipoId = det.ResiduoTipoId,
            NombreResiduoTipo = det.ResiduoTipo?.Nombre,
            CantidadAcumuladaAnterior = det.CantidadAcumuladaAnterior,
            Ene = det.Ene,
            Feb = det.Feb,
            Mar = det.Mar,
            Abr = det.Abr,
            May = det.May,
            Jun = det.Jun,
            Jul = det.Jul,
            Ago = det.Ago,
            Sep = det.Sep,
            Oct = det.Oct,
            Nov = det.Nov,
            Dic = det.Dic,
            Almacenado = det.Almacenado,
            Tratado = det.Tratado,
            Acondicionado = det.Acondicionado,
            Valorizado = det.Valorizado,
            Comercializado = det.Comercializado,
            DisposicionFinal = det.DisposicionFinal,
        }).ToList(),
        EoRsIntervinientes = d.EoRsIntervinientes.Select(e => new ResiduoDeclaracionEoRsDto
        {
            Id = e.Id,
            EoRsId = e.EoRsId,
            NombreEoRs = e.EoRs?.RazonSocial,
            Etapa = e.Etapa,
            NumeroServiciosAnio = e.NumeroServiciosAnio,
            TotalResiduoTon = e.TotalResiduoTon,
        }).ToList(),
    };

    public async Task<SsResiduoDeclaracion?> ObtenerAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Set<SsResiduoDeclaracion>().AsNoTracking()
            .Include(d => d.Contributor)
            .Include(d => d.Detalles).ThenInclude(det => det.ResiduoTipo)
            .Include(d => d.EoRsIntervinientes).ThenInclude(e => e.EoRs)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<SsResiduoDeclaracion?> ObtenerPorContributorPeriodoAsync(int contributorId, int periodoAnio)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Set<SsResiduoDeclaracion>()
            .Include(d => d.Detalles)
            .FirstOrDefaultAsync(d => d.ContributorId == contributorId && d.PeriodoAnio == periodoAnio);
    }

    public async Task<int> CrearAsync(ResiduoDeclaracionUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = new SsResiduoDeclaracion
        {
            ContributorId = dto.ContributorId,
            PeriodoAnio = dto.PeriodoAnio,
            Estado = "BORRADOR",
        };
        ctx.Set<SsResiduoDeclaracion>().Add(entidad);
        await ctx.SaveChangesAsync();
        return entidad.Id;
    }

    public async Task<bool> MarcarPresentadaAsync(int id, ResiduoDeclaracionMarcarPresentadaDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoDeclaracion>().FindAsync(id);
        if (entidad == null) return false;
        entidad.Estado = "PRESENTADA";
        entidad.FechaPresentacion = dto.FechaPresentacion;
        entidad.ArchivoConstanciaUrl = dto.ArchivoConstanciaUrl ?? entidad.ArchivoConstanciaUrl;
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> VolverABorradorAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoDeclaracion>().FindAsync(id);
        if (entidad == null) return false;
        entidad.Estado = "BORRADOR";
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoDeclaracion>().FindAsync(id);
        if (entidad == null) return false;
        ctx.Set<SsResiduoDeclaracion>().Remove(entidad);
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<int> UpsertDetalleAsync(int declaracionId, ResiduoDeclaracionDetalleUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoDeclaracionDetalle>()
            .FirstOrDefaultAsync(d => d.DeclaracionId == declaracionId && d.ResiduoTipoId == dto.ResiduoTipoId);

        if (entidad == null)
        {
            entidad = new SsResiduoDeclaracionDetalle { DeclaracionId = declaracionId, ResiduoTipoId = dto.ResiduoTipoId };
            ctx.Set<SsResiduoDeclaracionDetalle>().Add(entidad);
        }

        entidad.CantidadAcumuladaAnterior = dto.CantidadAcumuladaAnterior;
        entidad.Ene = dto.Ene;
        entidad.Feb = dto.Feb;
        entidad.Mar = dto.Mar;
        entidad.Abr = dto.Abr;
        entidad.May = dto.May;
        entidad.Jun = dto.Jun;
        entidad.Jul = dto.Jul;
        entidad.Ago = dto.Ago;
        entidad.Sep = dto.Sep;
        entidad.Oct = dto.Oct;
        entidad.Nov = dto.Nov;
        entidad.Dic = dto.Dic;
        entidad.Almacenado = dto.Almacenado;
        entidad.Tratado = dto.Tratado;
        entidad.Acondicionado = dto.Acondicionado;
        entidad.Valorizado = dto.Valorizado;
        entidad.Comercializado = dto.Comercializado;
        entidad.DisposicionFinal = dto.DisposicionFinal;
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;

        await ctx.SaveChangesAsync();
        return entidad.Id;
    }

    public async Task<bool> EliminarDetalleAsync(int detalleId)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoDeclaracionDetalle>().FindAsync(detalleId);
        if (entidad == null) return false;
        ctx.Set<SsResiduoDeclaracionDetalle>().Remove(entidad);
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<int> CrearEoRsIntervinienteAsync(int declaracionId, ResiduoDeclaracionEoRsUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = new SsResiduoDeclaracionEoRs
        {
            DeclaracionId = declaracionId,
            EoRsId = dto.EoRsId,
            Etapa = dto.Etapa,
            NumeroServiciosAnio = dto.NumeroServiciosAnio,
            TotalResiduoTon = dto.TotalResiduoTon,
        };
        ctx.Set<SsResiduoDeclaracionEoRs>().Add(entidad);
        await ctx.SaveChangesAsync();
        return entidad.Id;
    }

    public async Task<bool> ActualizarEoRsIntervinienteAsync(int itemId, ResiduoDeclaracionEoRsUpsertDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoDeclaracionEoRs>().FindAsync(itemId);
        if (entidad == null) return false;
        entidad.EoRsId = dto.EoRsId;
        entidad.Etapa = dto.Etapa;
        entidad.NumeroServiciosAnio = dto.NumeroServiciosAnio;
        entidad.TotalResiduoTon = dto.TotalResiduoTon;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> EliminarEoRsIntervinienteAsync(int itemId)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoDeclaracionEoRs>().FindAsync(itemId);
        if (entidad == null) return false;
        ctx.Set<SsResiduoDeclaracionEoRs>().Remove(entidad);
        await ctx.SaveChangesAsync();
        return true;
    }
}
