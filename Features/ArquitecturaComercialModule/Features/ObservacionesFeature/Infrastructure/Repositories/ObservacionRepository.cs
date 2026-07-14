using Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Application.Dtos;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Infrastructure.Repositories;

public class ObservacionRepository : IObservacionRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ObservacionRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<ObservacionListResponseDTO> GetObservaciones(int? proyectoId, string? estado, string? partida, DateTime? desde, DateTime? hasta, string? search, int pagina, int porPagina)
    {
        using var ctx = _factory.CreateDbContext();
        var query = ctx.AcObservaciones.Include(o => o.Proyecto).Include(o => o.Fotos).AsQueryable();

        if (proyectoId.HasValue) query = query.Where(o => o.ProyectoId == proyectoId.Value);
        if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(o => o.Estado == estado);
        if (!string.IsNullOrWhiteSpace(partida)) query = query.Where(o => o.PartidaReportada == partida);
        if (desde.HasValue) query = query.Where(o => o.Fecha >= desde.Value);
        if (hasta.HasValue) query = query.Where(o => o.Fecha <= hasta.Value);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(o => o.Codigo.Contains(search) || o.Descripcion.Contains(search) || (o.PersonaReporta != null && o.PersonaReporta.Contains(search)));

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.Fecha)
            .Skip((pagina - 1) * porPagina)
            .Take(porPagina)
            .Select(o => new ObservacionListItemDTO
            {
                Id = o.Id,
                ProyectoId = o.ProyectoId,
                ProyectoNombre = o.Proyecto != null ? o.Proyecto.ProjectDescription : null,
                Codigo = o.Codigo,
                Fecha = o.Fecha,
                PersonaReporta = o.PersonaReporta,
                EmpresaReporta = o.EmpresaReporta,
                Lugar = o.Lugar,
                Descripcion = o.Descripcion,
                PlazoLevantamiento = o.PlazoLevantamiento,
                PartidaReportada = o.PartidaReportada,
                Estado = o.Estado,
                TipoObservacion = o.TipoObservacion,
                AreaResponsable = o.AreaResponsable,
                Ejecutor = o.Ejecutor,
                Origen = o.Origen,
                Fotos = o.Fotos.Select(f => new ObservacionFotoDTO { Id = f.Id, Tipo = f.Tipo, Url = f.Url, Orden = f.Orden }).ToList()
            })
            .ToListAsync();

        return new ObservacionListResponseDTO { Total = total, Pagina = pagina, PorPagina = porPagina, Items = items };
    }

    public async Task<ObservacionListItemDTO?> GetObservacionById(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var o = await ctx.AcObservaciones.Include(x => x.Proyecto).Include(x => x.Fotos).FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return null;

        return new ObservacionListItemDTO
        {
            Id = o.Id,
            ProyectoId = o.ProyectoId,
            ProyectoNombre = o.Proyecto?.ProjectDescription,
            Codigo = o.Codigo,
            Fecha = o.Fecha,
            PersonaReporta = o.PersonaReporta,
            EmpresaReporta = o.EmpresaReporta,
            Lugar = o.Lugar,
            Descripcion = o.Descripcion,
            PlazoLevantamiento = o.PlazoLevantamiento,
            PartidaReportada = o.PartidaReportada,
            Estado = o.Estado,
            TipoObservacion = o.TipoObservacion,
            AreaResponsable = o.AreaResponsable,
            Ejecutor = o.Ejecutor,
            Origen = o.Origen,
            Fotos = o.Fotos.Select(f => new ObservacionFotoDTO { Id = f.Id, Tipo = f.Tipo, Url = f.Url, Orden = f.Orden }).ToList()
        };
    }

    public async Task<ObservacionFiltrosDTO> GetFiltros()
    {
        using var ctx = _factory.CreateDbContext();

        var proyectos = await ctx.AcObservaciones
            .Include(o => o.Proyecto)
            .Where(o => o.Proyecto != null)
            .Select(o => new ProyectoFiltroDTO { Id = o.ProyectoId, Nombre = o.Proyecto!.ProjectDescription })
            .Distinct()
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        var partidas = await ctx.AcObservaciones
            .Where(o => o.PartidaReportada != null)
            .Select(o => o.PartidaReportada!)
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync();

        var estados = await ctx.AcObservaciones
            .Select(o => o.Estado)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();

        return new ObservacionFiltrosDTO { Proyectos = proyectos, Partidas = partidas, Estados = estados };
    }

    public async Task<ObservacionDashboardDTO> GetDashboard(DateTime? desde, DateTime? hasta, int? proyectoId)
    {
        using var ctx = _factory.CreateDbContext();
        var query = ctx.AcObservaciones.Include(o => o.Proyecto).AsQueryable();

        if (desde.HasValue) query = query.Where(o => o.Fecha >= desde.Value);
        if (hasta.HasValue) query = query.Where(o => o.Fecha <= hasta.Value);
        if (proyectoId.HasValue) query = query.Where(o => o.ProyectoId == proyectoId.Value);

        var registros = await query.ToListAsync();

        var supervisores = registros
            .GroupBy(o => new { Persona = o.PersonaReporta ?? "Sin asignar", Proyecto = o.Proyecto?.ProjectDescription })
            .Select(g => new ObservacionDashboardSupervisorDTO
            {
                PersonaReporta = g.Key.Persona,
                ProyectoNombre = g.Key.Proyecto,
                TotalReportadas = g.Count(),
                TotalCompletadas = g.Count(o => o.Estado == "Completado"),
                TotalPendientes = g.Count(o => o.Estado == "Pendiente"),
                TotalEnProceso = g.Count(o => o.Estado == "En Proceso"),
                PctAvance = g.Count() == 0 ? 0 : Math.Round(g.Count(o => o.Estado == "Completado") * 100m / g.Count(), 1),
                PorPartida = g.GroupBy(o => o.PartidaReportada ?? "Otros")
                    .Select(pg => new ObservacionPorPartidaDTO
                    {
                        Partida = pg.Key,
                        Completado = pg.Count(o => o.Estado == "Completado"),
                        Pendiente = pg.Count(o => o.Estado != "Completado")
                    }).ToList()
            })
            .OrderByDescending(s => s.TotalReportadas)
            .ToList();

        return new ObservacionDashboardDTO { Supervisores = supervisores };
    }

    public async Task<int> GetProximoCorrelativo(string prefijoProyecto, int anio)
    {
        using var ctx = _factory.CreateDbContext();
        var sufijo = $"-{anio}";
        var existentes = await ctx.AcObservaciones
            .Where(o => o.Codigo.StartsWith(prefijoProyecto + "-") && o.Codigo.EndsWith(sufijo))
            .Select(o => o.Codigo)
            .ToListAsync();

        var max = 0;
        foreach (var c in existentes)
        {
            var partes = c.Split('-');
            if (partes.Length == 3 && int.TryParse(partes[1], out var n) && n > max) max = n;
        }
        return max + 1;
    }

    public async Task<string> GetProyectoAbbreviation(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();
        var proyecto = await ctx.Set<Abril_Backend.Shared.Models.Project>().FirstOrDefaultAsync(p => p.ProjectId == proyectoId);
        return proyecto?.Abbreviation ?? proyecto?.Codigo ?? "OBS";
    }

    public async Task<AcObservacion> CreateObservacion(CreateObservacionDTO body, string codigo)
    {
        using var ctx = _factory.CreateDbContext();
        var entity = new AcObservacion
        {
            ProyectoId = body.ProyectoId,
            Codigo = codigo,
            Fecha = body.Fecha,
            PersonaReporta = body.PersonaReporta,
            EmpresaReporta = body.EmpresaReporta,
            Lugar = body.Lugar,
            Descripcion = body.Descripcion,
            PlazoLevantamiento = body.PlazoLevantamiento,
            PartidaReportada = body.PartidaReportada,
            TipoObservacion = body.TipoObservacion,
            AreaResponsable = body.AreaResponsable,
            Ejecutor = body.Ejecutor,
            CreadoPor = body.CreadoPor,
            Estado = "Pendiente",
            Origen = "Nuevo",
            CreatedAt = DateTime.UtcNow
        };

        ctx.AcObservaciones.Add(entity);
        await ctx.SaveChangesAsync();
        return entity;
    }

    public async Task<AcObservacionFoto> AgregarFoto(int observacionId, string tipo, string url, int orden)
    {
        using var ctx = _factory.CreateDbContext();
        var foto = new AcObservacionFoto { ObservacionId = observacionId, Tipo = tipo, Url = url, Orden = orden, CreatedAt = DateTime.UtcNow };
        ctx.AcObservacionFotos.Add(foto);
        await ctx.SaveChangesAsync();
        return foto;
    }

    public async Task<ObservacionListItemDTO?> LevantarObservacion(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var o = await ctx.AcObservaciones.FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return null;

        o.Estado = "Completado";
        o.FechaLevantamiento = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        return await GetObservacionById(id);
    }
}
