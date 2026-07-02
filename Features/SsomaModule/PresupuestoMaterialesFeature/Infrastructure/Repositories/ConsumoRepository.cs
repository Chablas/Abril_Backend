using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

public class ConsumoRepository : IConsumoRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ConsumoRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<bool> ExisteHashAsync(string hash)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsConsumoCarga.AnyAsync(c => c.HashArchivo == hash && c.Estado == "ACTIVA");
    }

    public async Task<bool> ExisteSolapamientoFechasAsync(int projectId, DateOnly fechaMin, DateOnly fechaMax, int? excluirCargaId = null)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsConsumoCarga
            .Where(c => c.ProjectId == projectId && c.Estado == "ACTIVA"
                && (excluirCargaId == null || c.Id != excluirCargaId)
                && c.FechaMin <= fechaMax && c.FechaMax >= fechaMin)
            .AnyAsync();
    }

    public async Task<SsConsumoCarga> CrearCargaAsync(SsConsumoCarga carga)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.SsConsumoCarga.Add(carga);
        await ctx.SaveChangesAsync();
        return carga;
    }

    public async Task InsertarLineasBulkAsync(IEnumerable<SsConsumoLinea> lineas)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.SsConsumoLinea.AddRange(lineas);
        await ctx.SaveChangesAsync();
    }

    public async Task<List<SsConsumoLinea>> ObtenerLineasSinEstandarizarAsync(int cargaId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsConsumoLinea
            .Where(l => l.CargaId == cargaId && !l.Estandarizado)
            .ToListAsync();
    }

    public async Task ActualizarLineaEstandarizadaAsync(long lineaId, int itemId, bool perteneceSsoma, string metodo, decimal score, string? estadoRevision)
    {
        using var ctx = _factory.CreateDbContext();
        var linea = await ctx.SsConsumoLinea.FindAsync(lineaId);
        if (linea == null) return;
        linea.ItemId = itemId;
        linea.Estandarizado = true;
        linea.PerteneceSsoma = perteneceSsoma;
        linea.MetodoMatch = metodo;
        linea.ScoreMatch = score;
        linea.EstadoRevision = estadoRevision;
        await ctx.SaveChangesAsync();
    }

    public async Task ActualizarContadoresCargaAsync(int cargaId, int estandarizadas, int pendientes)
    {
        using var ctx = _factory.CreateDbContext();
        var carga = await ctx.SsConsumoCarga.FindAsync(cargaId);
        if (carga == null) return;
        carga.LineasEstandarizadas = estandarizadas;
        carga.LineasPendientes = pendientes;
        await ctx.SaveChangesAsync();
    }

    public async Task<List<ConsumoCargaResumenDto>> ObtenerCargasPorProyectoAsync(int projectId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsConsumoCarga
            .Where(c => c.ProjectId == projectId)
            .OrderByDescending(c => c.CreadoEn)
            .Select(c => new ConsumoCargaResumenDto
            {
                Id = c.Id,
                ProjectId = c.ProjectId,
                NombreArchivo = c.NombreArchivo,
                FechaMin = c.FechaMin,
                FechaMax = c.FechaMax,
                TotalLineas = c.TotalLineas,
                LineasEstandarizadas = c.LineasEstandarizadas,
                LineasPendientes = c.LineasPendientes,
                Estado = c.Estado,
                CreadoEn = c.CreadoEn
            })
            .ToListAsync();
    }

    public async Task<List<MaterialPendienteDto>> ObtenerPendientesRevisionAsync(int projectId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsConsumoLinea
            .Where(l => l.ProjectId == projectId && l.EstadoRevision == "PENDIENTE")
            .Include(l => l.Item).ThenInclude(i => i!.Familia)
            .OrderByDescending(l => l.PrecioTotal)
            .Select(l => new MaterialPendienteDto
            {
                LineaId = l.Id,
                RecursoCrudo = l.RecursoCrudo,
                Cantidad = l.Cantidad,
                PrecioUnitario = l.PrecioUnitario,
                PrecioTotal = l.PrecioTotal,
                FechaGuia = l.FechaGuia,
                NombreItemSugerido = l.Item != null ? l.Item.Nombre : null,
                NombreFamiliaSugerida = l.Item != null ? l.Item.Familia.Nombre : null,
                ScoreMatch = l.ScoreMatch,
                MetodoMatch = l.MetodoMatch,
                EstadoRevision = l.EstadoRevision,
                ItemIdSugerido = l.ItemId
            })
            .ToListAsync();
    }

    public async Task<SsConsumoLinea?> ObtenerLineaPorIdAsync(long lineaId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsConsumoLinea.FindAsync(lineaId);
    }

    public async Task ActualizarRevisionAsync(long lineaId, string decision, int? itemIdConfirmado)
    {
        using var ctx = _factory.CreateDbContext();
        var linea = await ctx.SsConsumoLinea.FindAsync(lineaId);
        if (linea == null) return;
        linea.EstadoRevision = decision;
        if (itemIdConfirmado.HasValue)
            linea.ItemId = itemIdConfirmado.Value;
        await ctx.SaveChangesAsync();
    }
}
