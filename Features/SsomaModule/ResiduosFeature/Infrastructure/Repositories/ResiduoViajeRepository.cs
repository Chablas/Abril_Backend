using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Repositories;

public class ResiduoViajeRepository : IResiduoViajeRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public ResiduoViajeRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<ResiduoViajePagedDto> ListarAsync(ResiduoViajeListFiltroDto filtro)
    {
        using var ctx = _factory.CreateDbContext();
        var query = ctx.Set<SsResiduoViaje>().AsNoTracking()
            .Include(v => v.ResiduoTipo).Include(v => v.EoRs)
            .Where(v => v.Activo)
            .AsQueryable();

        if (filtro.ProjectId.HasValue) query = query.Where(v => v.ProjectId == filtro.ProjectId.Value);
        if (filtro.ResiduoTipoId.HasValue) query = query.Where(v => v.ResiduoTipoId == filtro.ResiduoTipoId.Value);
        if (filtro.EoRsId.HasValue) query = query.Where(v => v.EoRsId == filtro.EoRsId.Value);
        if (filtro.FechaDesde.HasValue) query = query.Where(v => v.Fecha >= filtro.FechaDesde.Value);
        if (filtro.FechaHasta.HasValue) query = query.Where(v => v.Fecha <= filtro.FechaHasta.Value);

        var total = await query.CountAsync();
        var pagina = filtro.Pagina < 1 ? 1 : filtro.Pagina;
        var tamano = filtro.TamanoPagina < 1 ? 20 : filtro.TamanoPagina;

        var items = await query
            .OrderByDescending(v => v.Fecha).ThenByDescending(v => v.Id)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(v => Proyectar(v))
            .ToListAsync();

        return new ResiduoViajePagedDto { Items = items, Total = total, Pagina = pagina, TamanoPagina = tamano };
    }

    private static ResiduoViajeDto Proyectar(SsResiduoViaje v) => new()
    {
        Id = v.Id,
        ProjectId = v.ProjectId,
        AutorizacionDmeId = v.AutorizacionDmeId,
        ResiduoTipoId = v.ResiduoTipoId,
        NombreResiduoTipo = v.ResiduoTipo.Nombre,
        EoRsId = v.EoRsId,
        NombreEoRs = v.EoRs.RazonSocial,
        Contratista = v.Contratista,
        Origen = v.Origen,
        Fecha = v.Fecha,
        CantidadM3 = v.CantidadM3,
        CantidadTon = v.CantidadTon,
        TipoManejo = v.TipoManejo,
        GestorReceptor = v.GestorReceptor,
        DestinoFinal = v.DestinoFinal,
        NumeroRegistro = v.NumeroRegistro,
        NumeroCertificado = v.NumeroCertificado,
        ArchivoUrl = v.ArchivoUrl,
        Activo = v.Activo,
    };

    public async Task<SsResiduoViaje?> ObtenerAsync(long id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Set<SsResiduoViaje>().AsNoTracking()
            .Include(v => v.ResiduoTipo).Include(v => v.EoRs)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<long> CrearAsync(SsResiduoViaje entidad)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.Set<SsResiduoViaje>().Add(entidad);
        await ctx.SaveChangesAsync();
        return entidad.Id;
    }

    public async Task<bool> ActualizarAsync(SsResiduoViaje entidad)
    {
        using var ctx = _factory.CreateDbContext();
        var existente = await ctx.Set<SsResiduoViaje>().FindAsync(entidad.Id);
        if (existente == null) return false;

        existente.ProjectId = entidad.ProjectId;
        existente.AutorizacionDmeId = entidad.AutorizacionDmeId;
        existente.ResiduoTipoId = entidad.ResiduoTipoId;
        existente.EoRsId = entidad.EoRsId;
        existente.Contratista = entidad.Contratista;
        existente.Origen = entidad.Origen;
        existente.Fecha = entidad.Fecha;
        existente.CantidadM3 = entidad.CantidadM3;
        existente.CantidadTon = entidad.CantidadTon;
        existente.TipoManejo = entidad.TipoManejo;
        existente.GestorReceptor = entidad.GestorReceptor;
        existente.DestinoFinal = entidad.DestinoFinal;
        existente.NumeroRegistro = entidad.NumeroRegistro;
        existente.NumeroCertificado = entidad.NumeroCertificado;
        existente.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DesactivarAsync(long id)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoViaje>().FindAsync(id);
        if (entidad == null) return false;
        entidad.Activo = false;
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetArchivoAsync(long id, string archivoUrl)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.Set<SsResiduoViaje>().FindAsync(id);
        if (entidad == null) return false;
        entidad.ArchivoUrl = archivoUrl;
        entidad.ActualizadoEn = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<List<ResiduoViajeAgregadoMensualDto>> ObtenerAgregadoMensualAsync(int contributorId, int anio)
    {
        using var ctx = _factory.CreateDbContext();

        var viajes = await ctx.Set<SsResiduoViaje>().AsNoTracking()
            .Where(v => v.Activo
                && v.Fecha.Year == anio
                && v.Proyecto.ContributorId == contributorId)
            .Select(v => new
            {
                v.ResiduoTipoId,
                v.Fecha.Month,
                Cantidad = v.CantidadTon ?? 0m,
                v.TipoManejo,
            })
            .ToListAsync();

        var resultado = new List<ResiduoViajeAgregadoMensualDto>();
        foreach (var grupo in viajes.GroupBy(v => v.ResiduoTipoId))
        {
            var fila = new ResiduoViajeAgregadoMensualDto { ResiduoTipoId = grupo.Key };
            foreach (var v in grupo)
            {
                fila.Meses[v.Month - 1] += v.Cantidad;
                switch (v.TipoManejo)
                {
                    case "ALMACENADO": fila.Almacenado += v.Cantidad; break;
                    case "TRATADO": fila.Tratado += v.Cantidad; break;
                    case "ACONDICIONADO": fila.Acondicionado += v.Cantidad; break;
                    case "VALORIZADO": fila.Valorizado += v.Cantidad; break;
                    case "COMERCIALIZADO": fila.Comercializado += v.Cantidad; break;
                    case "DISPOSICION_FINAL": fila.DisposicionFinal += v.Cantidad; break;
                }
            }
            resultado.Add(fila);
        }
        return resultado;
    }
}
