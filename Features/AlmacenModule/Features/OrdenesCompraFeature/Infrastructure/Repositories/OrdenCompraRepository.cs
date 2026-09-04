using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Application.Dtos;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Infrastructure.Repositories;

public class OrdenCompraRepository : IOrdenCompraRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public OrdenCompraRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<AlmacenOrdenCompraListResponseDTO> GetOrdenesCompra(AlmacenOrdenCompraQueryParams query)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.AlmacenOrdenesCompra.Include(o => o.Proyecto).AsQueryable();

        if (query.ProyectoId.HasValue) q = q.Where(o => o.ProyectoId == query.ProyectoId.Value);
        if (!string.IsNullOrWhiteSpace(query.Tipo)) q = q.Where(o => o.Tipo == query.Tipo);
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(o => o.Numero.Contains(query.Search) || o.Proveedor.Contains(query.Search));

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(o => o.Fecha)
            .ThenByDescending(o => o.Id)
            .Skip((query.Pagina - 1) * query.PorPagina)
            .Take(query.PorPagina)
            .Select(o => new AlmacenOrdenCompraListItemDTO
            {
                Id = o.Id,
                ProyectoId = o.ProyectoId,
                ProyectoNombre = o.Proyecto != null ? o.Proyecto.ProjectDescription : null,
                Numero = o.Numero,
                Tipo = o.Tipo,
                Proveedor = o.Proveedor,
                ContratistaId = o.ContratistaId,
                Monto = o.Monto,
                Moneda = o.Moneda,
                Fecha = o.Fecha,
                ArchivoUrl = o.ArchivoUrl,
                ArchivoNombre = o.ArchivoNombre,
                SubidoPor = o.SubidoPor,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync();

        return new AlmacenOrdenCompraListResponseDTO { Total = total, Pagina = query.Pagina, PorPagina = query.PorPagina, Items = items };
    }

    public async Task<AlmacenOrdenCompra> CreateOrdenCompra(CreateAlmacenOrdenCompraDTO body, string archivoUrl, string archivoNombre, string? subidoPor)
    {
        using var ctx = _factory.CreateDbContext();
        var entity = new AlmacenOrdenCompra
        {
            ProyectoId = body.ProyectoId,
            Numero = body.Numero,
            Tipo = body.Tipo,
            Proveedor = body.Proveedor,
            ContratistaId = body.ContratistaId,
            Monto = body.Monto,
            Moneda = body.Moneda,
            Fecha = body.Fecha,
            ArchivoUrl = archivoUrl,
            ArchivoNombre = archivoNombre,
            SubidoPor = subidoPor
        };
        ctx.AlmacenOrdenesCompra.Add(entity);
        await ctx.SaveChangesAsync();
        return entity;
    }
}
