using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Dtos;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Repositories;

public class MaterialRepository : IMaterialRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public MaterialRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<AlmacenFiltrosDTO> GetFiltros()
    {
        using var ctx = _factory.CreateDbContext();

        var proyectos = await ctx.Project
            .Where(p => p.State && p.Active)
            .Select(p => new ProyectoAlmacenFiltroDTO { Id = p.ProjectId, Nombre = p.ProjectDescription })
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        var materiales = await ctx.AlmacenMateriales
            .Where(m => m.Activo)
            .Select(m => new AlmacenMaterialDTO { Id = m.Id, Codigo = m.Codigo, Nombre = m.Nombre, UnidadMedida = m.UnidadMedida, Activo = m.Activo })
            .OrderBy(m => m.Nombre)
            .ToListAsync();

        return new AlmacenFiltrosDTO { Proyectos = proyectos, Materiales = materiales };
    }

    public async Task<bool> CodigoExiste(string codigo)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.AlmacenMateriales.AnyAsync(m => m.Codigo == codigo);
    }

    public async Task<AlmacenMaterialDTO> CreateMaterial(CreateAlmacenMaterialDTO body)
    {
        using var ctx = _factory.CreateDbContext();
        var entity = new AlmacenMaterial
        {
            Codigo = body.Codigo,
            Nombre = body.Nombre,
            UnidadMedida = body.UnidadMedida,
            PuntoReorden = body.PuntoReorden,
            StockSeguridad = body.StockSeguridad,
            Activo = true
        };
        ctx.AlmacenMateriales.Add(entity);
        await ctx.SaveChangesAsync();

        return new AlmacenMaterialDTO { Id = entity.Id, Codigo = entity.Codigo, Nombre = entity.Nombre, UnidadMedida = entity.UnidadMedida, Activo = entity.Activo };
    }

    public async Task<AlmacenMovimientoListResponseDTO> GetMovimientos(AlmacenMovimientosQueryParams query)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.AlmacenMovimientos.Include(m => m.Proyecto).Include(m => m.Material).AsQueryable();

        if (query.ProyectoId.HasValue) q = q.Where(m => m.ProyectoId == query.ProyectoId.Value);
        if (query.MaterialId.HasValue) q = q.Where(m => m.MaterialId == query.MaterialId.Value);
        if (!string.IsNullOrWhiteSpace(query.Tipo)) q = q.Where(m => m.Tipo == query.Tipo);
        if (query.Desde.HasValue) q = q.Where(m => m.Fecha >= query.Desde.Value);
        if (query.Hasta.HasValue) q = q.Where(m => m.Fecha <= query.Hasta.Value);

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(m => m.Fecha)
            .ThenByDescending(m => m.Id)
            .Skip((query.Pagina - 1) * query.PorPagina)
            .Take(query.PorPagina)
            .Select(m => new AlmacenMovimientoListItemDTO
            {
                Id = m.Id,
                ProyectoId = m.ProyectoId,
                ProyectoNombre = m.Proyecto != null ? m.Proyecto.ProjectDescription : null,
                MaterialId = m.MaterialId,
                MaterialCodigo = m.Material != null ? m.Material.Codigo : null,
                MaterialNombre = m.Material != null ? m.Material.Nombre : null,
                UnidadMedida = m.Material != null ? m.Material.UnidadMedida : null,
                Fecha = m.Fecha,
                Tipo = m.Tipo,
                Cantidad = m.Cantidad,
                Origen = m.Origen,
                Comentario = m.Comentario,
                CreadoPor = m.CreadoPor
            })
            .ToListAsync();

        return new AlmacenMovimientoListResponseDTO { Total = total, Pagina = query.Pagina, PorPagina = query.PorPagina, Items = items };
    }

    public async Task<AlmacenMovimientoListItemDTO> CreateMovimiento(CreateAlmacenMovimientoDTO body, string? creadoPor)
    {
        using var ctx = _factory.CreateDbContext();
        var entity = new AlmacenMovimiento
        {
            ProyectoId = body.ProyectoId,
            MaterialId = body.MaterialId,
            Fecha = body.Fecha,
            Tipo = body.Tipo,
            Cantidad = body.Cantidad,
            Origen = body.Origen,
            Comentario = body.Comentario,
            CreadoPor = creadoPor
        };
        ctx.AlmacenMovimientos.Add(entity);
        await ctx.SaveChangesAsync();

        var proyectoNombre = await ctx.Project.Where(p => p.ProjectId == body.ProyectoId).Select(p => p.ProjectDescription).FirstOrDefaultAsync();
        var material = await ctx.AlmacenMateriales.FirstOrDefaultAsync(m => m.Id == body.MaterialId);

        return new AlmacenMovimientoListItemDTO
        {
            Id = entity.Id,
            ProyectoId = entity.ProyectoId,
            ProyectoNombre = proyectoNombre,
            MaterialId = entity.MaterialId,
            MaterialCodigo = material?.Codigo,
            MaterialNombre = material?.Nombre,
            UnidadMedida = material?.UnidadMedida,
            Fecha = entity.Fecha,
            Tipo = entity.Tipo,
            Cantidad = entity.Cantidad,
            Origen = entity.Origen,
            Comentario = entity.Comentario,
            CreadoPor = entity.CreadoPor
        };
    }

    public async Task<AlmacenStockDTO> GetStock(int? proyectoId)
    {
        using var ctx = _factory.CreateDbContext();

        var q = ctx.AlmacenMovimientos.AsQueryable();
        if (proyectoId.HasValue) q = q.Where(m => m.ProyectoId == proyectoId.Value);

        var agregados = await q
            .GroupBy(m => m.MaterialId)
            .Select(g => new
            {
                MaterialId = g.Key,
                TotalIngresos = g.Where(m => m.Tipo == TipoMovimientoAlmacen.Ingreso).Sum(m => m.Cantidad),
                TotalSalidas = g.Where(m => m.Tipo == TipoMovimientoAlmacen.Salida).Sum(m => m.Cantidad)
            })
            .ToListAsync();

        var materiales = await ctx.AlmacenMateriales.Where(m => m.Activo).ToListAsync();

        var items = materiales
            .Select(m =>
            {
                var agg = agregados.FirstOrDefault(a => a.MaterialId == m.Id);
                var ingresos = agg?.TotalIngresos ?? 0m;
                var salidas = agg?.TotalSalidas ?? 0m;
                return new AlmacenStockItemDTO
                {
                    MaterialId = m.Id,
                    MaterialCodigo = m.Codigo,
                    MaterialNombre = m.Nombre,
                    UnidadMedida = m.UnidadMedida,
                    TotalIngresos = ingresos,
                    TotalSalidas = salidas,
                    SaldoActual = ingresos - salidas
                };
            })
            .Where(i => i.TotalIngresos > 0 || i.TotalSalidas > 0)
            .OrderByDescending(i => i.SaldoActual)
            .ToList();

        return new AlmacenStockDTO { ProyectoId = proyectoId, Materiales = items };
    }

    private const int LimiteSeguridadDias = 10;

    public async Task<AlmacenDashboardDTO> GetDashboard(int? proyectoId, int diasVentana)
    {
        using var ctx = _factory.CreateDbContext();

        var query = ctx.AlmacenMovimientos.Include(m => m.Material).Include(m => m.Proyecto).AsQueryable();
        if (proyectoId.HasValue) query = query.Where(m => m.ProyectoId == proyectoId.Value);

        var movimientos = await query.ToListAsync();

        // 1) Flujo de materiales: ingresos vs salidas globales, top 8 por volumen total.
        var flujo = movimientos
            .GroupBy(m => m.Material != null ? m.Material.Nombre : "—")
            .Select(g => new AlmacenDashboardFlujoItemDTO
            {
                MaterialNombre = g.Key,
                TotalIngresos = g.Where(m => m.Tipo == TipoMovimientoAlmacen.Ingreso).Sum(m => m.Cantidad),
                TotalSalidas = g.Where(m => m.Tipo == TipoMovimientoAlmacen.Salida).Sum(m => m.Cantidad)
            })
            .OrderByDescending(f => f.TotalIngresos + f.TotalSalidas)
            .Take(8)
            .ToList();

        // 2) Participación por proyecto (consumo = salidas), agrupando el resto en "Otros".
        var consumoPorProyecto = movimientos
            .Where(m => m.Tipo == TipoMovimientoAlmacen.Salida)
            .GroupBy(m => m.Proyecto != null ? m.Proyecto.ProjectDescription : "—")
            .Select(g => new { Proyecto = g.Key, Total = g.Sum(m => m.Cantidad) })
            .OrderByDescending(g => g.Total)
            .ToList();

        var totalConsumo = consumoPorProyecto.Sum(c => c.Total);
        var participacion = new List<AlmacenDashboardParticipacionItemDTO>();
        const int topProyectos = 6;

        foreach (var c in consumoPorProyecto.Take(topProyectos))
        {
            participacion.Add(new AlmacenDashboardParticipacionItemDTO
            {
                ProyectoNombre = c.Proyecto,
                TotalConsumo = c.Total,
                Porcentaje = totalConsumo == 0 ? 0 : Math.Round(c.Total * 100m / totalConsumo, 1)
            });
        }

        var otros = consumoPorProyecto.Skip(topProyectos).Sum(c => c.Total);
        if (otros > 0)
        {
            participacion.Add(new AlmacenDashboardParticipacionItemDTO
            {
                ProyectoNombre = "Otros",
                TotalConsumo = otros,
                Porcentaje = totalConsumo == 0 ? 0 : Math.Round(otros * 100m / totalConsumo, 1)
            });
        }

        // 3) Materiales críticos: solo los que tienen umbrales configurados.
        var materiales = await ctx.AlmacenMateriales
            .Where(m => m.Activo && m.PuntoReorden != null && m.StockSeguridad != null)
            .ToListAsync();

        var stockPorMaterial = movimientos
            .GroupBy(m => m.MaterialId)
            .ToDictionary(
                g => g.Key,
                g => g.Where(m => m.Tipo == TipoMovimientoAlmacen.Ingreso).Sum(m => m.Cantidad)
                    - g.Where(m => m.Tipo == TipoMovimientoAlmacen.Salida).Sum(m => m.Cantidad));

        var criticos = new List<AlmacenMaterialCriticoDTO>();
        foreach (var m in materiales)
        {
            var stockActual = stockPorMaterial.TryGetValue(m.Id, out var s) ? s : 0m;
            var puntoReorden = m.PuntoReorden!.Value;
            var stockSeguridad = m.StockSeguridad!.Value;

            string estado;
            string accion;
            if (stockActual <= stockSeguridad)
            {
                estado = EstadoStockCritico.Critico;
                accion = $"OC URGENTE x {Math.Max(puntoReorden - stockActual, 0):0.##} {m.UnidadMedida}";
            }
            else if (stockActual <= puntoReorden)
            {
                estado = EstadoStockCritico.AlertaBaja;
                accion = $"Programar OC x {Math.Max(puntoReorden - stockActual, 0):0.##} {m.UnidadMedida}";
            }
            else if (stockActual <= puntoReorden * 1.3m)
            {
                estado = EstadoStockCritico.BajoMinimos;
                accion = "Confirmar OC en tránsito";
            }
            else
            {
                estado = EstadoStockCritico.Optimo;
                accion = "Reposición menor";
            }

            criticos.Add(new AlmacenMaterialCriticoDTO
            {
                MaterialId = m.Id,
                MaterialNombre = m.Nombre,
                UnidadMedida = m.UnidadMedida,
                StockActual = stockActual,
                PuntoReorden = puntoReorden,
                StockSeguridad = stockSeguridad,
                Estado = estado,
                AccionRecomendada = accion
            });
        }

        criticos = criticos.OrderBy(c => c.StockActual - c.StockSeguridad).ToList();

        // 4) Cobertura: días de stock actual / consumo diario promedio de los últimos N días.
        var desdeVentana = DateTime.UtcNow.Date.AddDays(-diasVentana);
        var salidasVentana = await ctx.AlmacenMovimientos
            .Where(m => m.Tipo == TipoMovimientoAlmacen.Salida && m.Fecha >= desdeVentana && (!proyectoId.HasValue || m.ProyectoId == proyectoId.Value))
            .GroupBy(m => m.MaterialId)
            .Select(g => new { MaterialId = g.Key, Total = g.Sum(m => m.Cantidad) })
            .ToListAsync();

        var cobertura = criticos.Select(c =>
        {
            var consumoVentana = salidasVentana.FirstOrDefault(s => s.MaterialId == c.MaterialId)?.Total ?? 0m;
            var consumoDiario = consumoVentana / diasVentana;
            decimal? dias = consumoDiario > 0 ? Math.Round(c.StockActual / consumoDiario, 1) : null;
            return new AlmacenCoberturaItemDTO { MaterialNombre = c.MaterialNombre, DiasCobertura = dias };
        }).ToList();

        return new AlmacenDashboardDTO
        {
            FlujoMateriales = flujo,
            ParticipacionProyectos = participacion,
            MaterialesCriticos = criticos,
            Cobertura = cobertura,
            LimiteSeguridadDias = LimiteSeguridadDias
        };
    }
}
