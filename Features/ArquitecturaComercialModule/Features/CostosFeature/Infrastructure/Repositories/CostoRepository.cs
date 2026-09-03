using Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Application.Dtos;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Infrastructure.Repositories;

public class CostoRepository : ICostoRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public CostoRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    private static (int anio, int mes) MesSiguiente(int anio, int mes) => mes == 12 ? (anio + 1, 1) : (anio, mes + 1);

    public async Task<CostoFiltrosDTO> GetFiltros()
    {
        using var ctx = _factory.CreateDbContext();

        var proyectos = await ctx.Project
            .Where(p => p.TieneArquitecturaComercial && p.State && p.Active)
            .Select(p => new ProyectoCostoFiltroDTO { Id = p.ProjectId, Nombre = p.ProjectDescription })
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        return new CostoFiltrosDTO { Proyectos = proyectos, Partidas = PartidaCosto.Valores.ToList() };
    }

    public async Task<CostoMatrizDTO?> GetMatriz(int proyectoId, int anio, int mes)
    {
        using var ctx = _factory.CreateDbContext();

        var proyectoNombre = await ctx.Project.Where(p => p.ProjectId == proyectoId).Select(p => p.ProjectDescription).FirstOrDefaultAsync();
        if (proyectoNombre == null) return null;

        var registros = await ctx.AcCostoRegistros
            .Where(r => r.ProyectoId == proyectoId && r.Anio == anio && r.Mes == mes)
            .ToListAsync();

        var (anioProy, mesProy) = MesSiguiente(anio, mes);
        var proyecciones = await ctx.AcCostoProyecciones
            .Where(p => p.ProyectoId == proyectoId && p.Anio == anioProy && p.Mes == mesProy)
            .ToListAsync();

        var numeroSemanas = registros.Count == 0 ? 4 : Math.Max(4, registros.Max(r => r.Semana));

        var filas = PartidaCosto.Valores.Select(partida =>
        {
            var montos = registros.Where(r => r.Partida == partida).ToDictionary(r => r.Semana, r => r.Monto);
            return new CostoPartidaFilaDTO
            {
                Partida = partida,
                MontosPorSemana = montos,
                TotalMes = montos.Values.Sum()
            };
        }).ToList();

        var proyeccionesDto = PartidaCosto.Valores.Select(partida => new CostoPartidaProyeccionDTO
        {
            Partida = partida,
            Monto = proyecciones.FirstOrDefault(p => p.Partida == partida)?.Monto ?? 0m
        }).ToList();

        return new CostoMatrizDTO
        {
            ProyectoId = proyectoId,
            ProyectoNombre = proyectoNombre,
            Anio = anio,
            Mes = mes,
            NumeroSemanas = numeroSemanas,
            Partidas = filas,
            SubtotalMes = filas.Sum(f => f.TotalMes),
            AnioProyeccion = anioProy,
            MesProyeccion = mesProy,
            Proyecciones = proyeccionesDto,
            SubtotalProyeccion = proyeccionesDto.Sum(p => p.Monto)
        };
    }

    public async Task UpsertRegistro(UpsertCostoRegistroDTO body, string? creadoPor)
    {
        using var ctx = _factory.CreateDbContext();

        var entity = await ctx.AcCostoRegistros.FirstOrDefaultAsync(r =>
            r.ProyectoId == body.ProyectoId && r.Anio == body.Anio && r.Mes == body.Mes &&
            r.Semana == body.Semana && r.Partida == body.Partida);

        if (entity == null)
        {
            ctx.AcCostoRegistros.Add(new AcCostoRegistro
            {
                ProyectoId = body.ProyectoId,
                Anio = body.Anio,
                Mes = body.Mes,
                Semana = body.Semana,
                Partida = body.Partida,
                Monto = body.Monto,
                CreadoPor = creadoPor
            });
        }
        else
        {
            entity.Monto = body.Monto;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await ctx.SaveChangesAsync();
    }

    public async Task UpsertProyeccion(UpsertCostoProyeccionDTO body, string? creadoPor)
    {
        using var ctx = _factory.CreateDbContext();

        var entity = await ctx.AcCostoProyecciones.FirstOrDefaultAsync(p =>
            p.ProyectoId == body.ProyectoId && p.Anio == body.Anio && p.Mes == body.Mes && p.Partida == body.Partida);

        if (entity == null)
        {
            ctx.AcCostoProyecciones.Add(new AcCostoProyeccion
            {
                ProyectoId = body.ProyectoId,
                Anio = body.Anio,
                Mes = body.Mes,
                Partida = body.Partida,
                Monto = body.Monto,
                CreadoPor = creadoPor
            });
        }
        else
        {
            entity.Monto = body.Monto;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await ctx.SaveChangesAsync();
    }

    public async Task<CostoDashboardDTO> GetDashboard(int anio, int mes)
    {
        using var ctx = _factory.CreateDbContext();

        var proyectos = await ctx.Project
            .Where(p => p.TieneArquitecturaComercial && p.State && p.Active)
            .Select(p => new { p.ProjectId, p.ProjectDescription })
            .ToListAsync();

        var registros = await ctx.AcCostoRegistros
            .Where(r => r.Anio == anio && r.Mes == mes)
            .GroupBy(r => r.ProyectoId)
            .Select(g => new { ProyectoId = g.Key, Total = g.Sum(r => r.Monto) })
            .ToListAsync();

        var items = proyectos
            .Select(p => new CostoDashboardItemDTO
            {
                ProyectoId = p.ProjectId,
                ProyectoNombre = p.ProjectDescription,
                TotalMes = registros.FirstOrDefault(r => r.ProyectoId == p.ProjectId)?.Total ?? 0m
            })
            .Where(i => i.TotalMes > 0m)
            .OrderByDescending(i => i.TotalMes)
            .ToList();

        return new CostoDashboardDTO { Anio = anio, Mes = mes, Proyectos = items };
    }

    public async Task<CostoEvolucionDTO> GetEvolucion(int anioDesde, int mesDesde, int cantidadMeses)
    {
        using var ctx = _factory.CreateDbContext();

        var puntos = new List<CostoEvolucionPuntoDTO>();
        var (anio, mes) = (anioDesde, mesDesde);

        for (var i = 0; i < cantidadMeses; i++)
        {
            var totalRegistrado = await ctx.AcCostoRegistros
                .Where(r => r.Anio == anio && r.Mes == mes)
                .SumAsync(r => (decimal?)r.Monto) ?? 0m;

            decimal total;
            bool esProyeccion;

            if (totalRegistrado > 0m)
            {
                total = totalRegistrado;
                esProyeccion = false;
            }
            else
            {
                total = await ctx.AcCostoProyecciones
                    .Where(p => p.Anio == anio && p.Mes == mes)
                    .SumAsync(p => (decimal?)p.Monto) ?? 0m;
                esProyeccion = true;
            }

            var meta = await ctx.AcCostoMetaMensuales
                .Where(m => m.Anio == anio && m.Mes == mes)
                .Select(m => (decimal?)m.Monto)
                .FirstOrDefaultAsync();

            puntos.Add(new CostoEvolucionPuntoDTO { Anio = anio, Mes = mes, GastoEjecutadoOProyectado = total, EsProyeccion = esProyeccion, PresupuestoMeta = meta });

            (anio, mes) = MesSiguiente(anio, mes);
        }

        return new CostoEvolucionDTO { Puntos = puntos };
    }

    public async Task UpsertMeta(UpsertCostoMetaDTO body, string? creadoPor)
    {
        using var ctx = _factory.CreateDbContext();

        var entity = await ctx.AcCostoMetaMensuales.FirstOrDefaultAsync(m => m.Anio == body.Anio && m.Mes == body.Mes);
        if (entity == null)
        {
            ctx.AcCostoMetaMensuales.Add(new AcCostoMetaMensual { Anio = body.Anio, Mes = body.Mes, Monto = body.Monto, CreadoPor = creadoPor });
        }
        else
        {
            entity.Monto = body.Monto;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await ctx.SaveChangesAsync();
    }
}
