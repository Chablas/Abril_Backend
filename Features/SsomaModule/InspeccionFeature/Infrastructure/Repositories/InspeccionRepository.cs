using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.InspeccionFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.InspeccionFeature.Infrastructure.Repositories;

public class InspeccionRepository : IInspeccionRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public InspeccionRepository(IDbContextFactory<AppDbContext> factory)
        => _factory = factory;

    public async Task<List<InspeccionTipoDto>> GetTiposAsync()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsomaInspeccionTipo
            .Where(t => t.Activo)
            .OrderBy(t => t.Ambito).ThenBy(t => t.Nombre)
            .Select(t => new InspeccionTipoDto
            {
                Id = t.Id,
                Nombre = t.Nombre,
                Ambito = t.Ambito
            })
            .ToListAsync();
    }

    public async Task<List<InspeccionChecklistItemDto>> GetChecklistItemsAsync(int tipoId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsomaInspeccionChecklistItem
            .Where(i => i.TipoId == tipoId && i.Activo)
            .OrderBy(i => i.Orden)
            .Select(i => new InspeccionChecklistItemDto
            {
                Id = i.Id,
                TipoId = i.TipoId,
                Pregunta = i.Pregunta,
                Categoria = i.Categoria,
                Orden = i.Orden
            })
            .ToListAsync();
    }

    public async Task<int> CrearInspeccionAsync(CrearInspeccionRequest request,
        string? firmaInspectorUrl, string? firmaRepresentanteUrl,
        Dictionary<int, List<string>> fotosHallazgoUrls)
    {
        using var ctx = _factory.CreateDbContext();

        var totalCumple = request.Respuestas.Count(r => r.Resultado == "Cumple");
        var totalNoCumple = request.Respuestas.Count(r => r.Resultado == "NoCumple");
        var totalNa = request.Respuestas.Count(r => r.Resultado == "NA");
        var evaluados = totalCumple + totalNoCumple;
        decimal? tasa = evaluados > 0
            ? Math.Round((decimal)totalCumple / evaluados * 100, 2)
            : null;

        TimeOnly? horaInicio = null, horaFin = null;
        if (!string.IsNullOrEmpty(request.HoraInicio) && TimeOnly.TryParse(request.HoraInicio, out var hi))
            horaInicio = hi;
        if (!string.IsNullOrEmpty(request.HoraFin) && TimeOnly.TryParse(request.HoraFin, out var hf))
            horaFin = hf;

        var inspeccion = new SsomaInspeccion
        {
            ProyectoId = request.ProyectoId,
            TipoId = request.TipoId,
            EmpresaId = request.EmpresaId,
            EsPlanificada = request.EsPlanificada,
            Fecha = request.Fecha.Date,
            HoraInicio = horaInicio,
            HoraFin = horaFin,
            Area = request.Area,
            ResponsableArea = request.ResponsableArea,
            InspectorNombre = request.InspectorNombre,
            InspectorCargo = request.InspectorCargo,
            InspectorEmpresa = request.InspectorEmpresa,
            FirmaInspectorUrl = firmaInspectorUrl,
            RepresentanteNombre = request.RepresentanteNombre,
            RepresentanteCargo = request.RepresentanteCargo,
            FirmaRepresentanteUrl = firmaRepresentanteUrl,
            DescripcionCausas = request.DescripcionCausas,
            Conclusiones = request.Conclusiones,
            TotalItems = request.Respuestas.Count,
            TotalCumple = totalCumple,
            TotalNoCumple = totalNoCumple,
            TotalNa = totalNa,
            TasaCumplimiento = tasa,
            Estado = request.Hallazgos.Any() ? "En Proceso" : "Cerrada",
            CreatedAt = DateTime.UtcNow
        };

        ctx.SsomaInspeccion.Add(inspeccion);
        await ctx.SaveChangesAsync();

        foreach (var r in request.Respuestas)
        {
            ctx.SsomaInspeccionRespuesta.Add(new SsomaInspeccionRespuesta
            {
                InspeccionId = inspeccion.Id,
                ItemId = r.ItemId,
                Resultado = r.Resultado,
                Observacion = r.Observacion
            });
        }

        for (int i = 0; i < request.Hallazgos.Count; i++)
        {
            var h = request.Hallazgos[i];
            var hallazgo = new SsomaInspeccionHallazgo
            {
                InspeccionId = inspeccion.Id,
                Descripcion = h.Descripcion,
                Tipo = h.Tipo,
                Area = h.Area,
                ResponsableNombre = h.ResponsableNombre,
                ResponsableCargo = h.ResponsableCargo,
                FechaLimite = h.FechaLimite,
                AccionCorrectiva = h.AccionCorrectiva,
                Latitud = h.Latitud,
                Longitud = h.Longitud,
                Estado = "Abierto",
                CreatedAt = DateTime.UtcNow
            };
            ctx.SsomaInspeccionHallazgo.Add(hallazgo);
            await ctx.SaveChangesAsync();

            if (fotosHallazgoUrls.TryGetValue(i, out var urls))
            {
                for (int j = 0; j < urls.Count; j++)
                {
                    ctx.SsomaInspeccionHallazgoFoto.Add(new SsomaInspeccionHallazgoFoto
                    {
                        HallazgoId = hallazgo.Id,
                        Url = urls[j],
                        Orden = j,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await ctx.SaveChangesAsync();
        return inspeccion.Id;
    }

    public async Task CerrarHallazgoAsync(int hallazgoId, CerrarHallazgoRequest request, string? evidenciaUrl)
    {
        using var ctx = _factory.CreateDbContext();
        var hallazgo = await ctx.SsomaInspeccionHallazgo.FindAsync(hallazgoId)
            ?? throw new AbrilException("Hallazgo no encontrado.", 404);
        hallazgo.Estado = "Cerrado";
        hallazgo.AccionCorrectiva = request.AccionCorrectiva;
        hallazgo.EvidenciaCierreUrl = evidenciaUrl;
        hallazgo.FechaCierre = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task<InspeccionDetalleDto?> GetDetalleAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var insp = await ctx.SsomaInspeccion
            .Include(i => i.Proyecto)
            .Include(i => i.Tipo)
            .Include(i => i.Empresa)
            .Include(i => i.Respuestas).ThenInclude(r => r.Item)
            .Include(i => i.Hallazgos).ThenInclude(h => h.Fotos)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (insp == null) return null;

        return new InspeccionDetalleDto
        {
            Id = insp.Id,
            ProyectoId = insp.ProyectoId,
            ProyectoNombre = insp.Proyecto?.ProjectDescription ?? "",
            TipoId = insp.TipoId,
            TipoNombre = insp.Tipo?.Nombre ?? "",
            TipoAmbito = insp.Tipo?.Ambito ?? "",
            EmpresaId = insp.EmpresaId,
            EmpresaNombre = insp.Empresa?.ContributorNombreComercial,
            EsPlanificada = insp.EsPlanificada,
            Fecha = insp.Fecha,
            HoraInicio = insp.HoraInicio?.ToString("HH:mm"),
            HoraFin = insp.HoraFin?.ToString("HH:mm"),
            Area = insp.Area,
            ResponsableArea = insp.ResponsableArea,
            InspectorNombre = insp.InspectorNombre,
            InspectorCargo = insp.InspectorCargo,
            InspectorEmpresa = insp.InspectorEmpresa,
            FirmaInspectorUrl = insp.FirmaInspectorUrl,
            RepresentanteNombre = insp.RepresentanteNombre,
            RepresentanteCargo = insp.RepresentanteCargo,
            FirmaRepresentanteUrl = insp.FirmaRepresentanteUrl,
            DescripcionCausas = insp.DescripcionCausas,
            Conclusiones = insp.Conclusiones,
            TotalItems = insp.TotalItems,
            TotalCumple = insp.TotalCumple,
            TotalNoCumple = insp.TotalNoCumple,
            TotalNa = insp.TotalNa,
            TasaCumplimiento = insp.TasaCumplimiento,
            Estado = insp.Estado,
            CreatedAt = insp.CreatedAt,
            Respuestas = insp.Respuestas
                .OrderBy(r => r.Item?.Orden)
                .Select(r => new InspeccionRespuestaDto
                {
                    ItemId = r.ItemId,
                    Pregunta = r.Item?.Pregunta ?? "",
                    Categoria = r.Item?.Categoria,
                    Orden = r.Item?.Orden ?? 0,
                    Resultado = r.Resultado,
                    Observacion = r.Observacion
                }).ToList(),
            Hallazgos = insp.Hallazgos
                .OrderByDescending(h => h.Tipo)
                .Select(h => new InspeccionHallazgoDto
                {
                    Id = h.Id,
                    Descripcion = h.Descripcion,
                    Tipo = h.Tipo,
                    Area = h.Area,
                    ResponsableNombre = h.ResponsableNombre,
                    ResponsableCargo = h.ResponsableCargo,
                    FechaLimite = h.FechaLimite,
                    Estado = h.Estado,
                    AccionCorrectiva = h.AccionCorrectiva,
                    EvidenciaCierreUrl = h.EvidenciaCierreUrl,
                    FechaCierre = h.FechaCierre,
                    Latitud = h.Latitud,
                    Longitud = h.Longitud,
                    Fotos = h.Fotos.OrderBy(f => f.Orden)
                        .Select(f => new InspeccionHallazgoFotoDto
                        {
                            Id = f.Id,
                            Url = f.Url,
                            Descripcion = f.Descripcion,
                            Orden = f.Orden
                        }).ToList()
                }).ToList()
        };
    }

    public async Task<List<InspeccionListItemDto>> GetListAsync(int? proyectoId, int? tipoId,
        string? estado, DateTime? fechaDesde, DateTime? fechaHasta, int page, int pageSize)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsomaInspeccion
            .Include(i => i.Proyecto)
            .Include(i => i.Tipo)
            .Include(i => i.Empresa)
            .Include(i => i.Hallazgos)
            .AsQueryable();

        if (proyectoId.HasValue) q = q.Where(i => i.ProyectoId == proyectoId.Value);
        if (tipoId.HasValue) q = q.Where(i => i.TipoId == tipoId.Value);
        if (!string.IsNullOrEmpty(estado)) q = q.Where(i => i.Estado == estado);
        if (fechaDesde.HasValue) q = q.Where(i => i.Fecha >= fechaDesde.Value.Date);
        if (fechaHasta.HasValue) q = q.Where(i => i.Fecha <= fechaHasta.Value.Date);

        return await q
            .OrderByDescending(i => i.Fecha)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InspeccionListItemDto
            {
                Id = i.Id,
                ProyectoNombre = i.Proyecto != null ? i.Proyecto.ProjectDescription : "",
                TipoNombre = i.Tipo != null ? i.Tipo.Nombre : "",
                TipoAmbito = i.Tipo != null ? i.Tipo.Ambito : "",
                EmpresaNombre = i.Empresa != null ? i.Empresa.ContributorNombreComercial : null,
                EsPlanificada = i.EsPlanificada,
                Fecha = i.Fecha,
                Area = i.Area,
                InspectorNombre = i.InspectorNombre,
                TotalHallazgos = i.Hallazgos.Count,
                HallazgosCriticos = i.Hallazgos.Count(h => h.Tipo == "Critico"),
                HallazgosAbiertos = i.Hallazgos.Count(h => h.Estado == "Abierto"),
                TasaCumplimiento = i.TasaCumplimiento,
                Estado = i.Estado,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<int> GetListCountAsync(int? proyectoId, int? tipoId,
        string? estado, DateTime? fechaDesde, DateTime? fechaHasta)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsomaInspeccion.AsQueryable();
        if (proyectoId.HasValue) q = q.Where(i => i.ProyectoId == proyectoId.Value);
        if (tipoId.HasValue) q = q.Where(i => i.TipoId == tipoId.Value);
        if (!string.IsNullOrEmpty(estado)) q = q.Where(i => i.Estado == estado);
        if (fechaDesde.HasValue) q = q.Where(i => i.Fecha >= fechaDesde.Value.Date);
        if (fechaHasta.HasValue) q = q.Where(i => i.Fecha <= fechaHasta.Value.Date);
        return await q.CountAsync();
    }

    public async Task<InspeccionDashboardDto> GetDashboardAsync(int? proyectoId, int? anio)
    {
        using var ctx = _factory.CreateDbContext();
        var anioFiltro = anio ?? DateTime.Now.Year;
        var mesActual = DateTime.Now.Month;

        var q = ctx.SsomaInspeccion.Include(i => i.Tipo).Include(i => i.Hallazgos).AsQueryable();
        if (proyectoId.HasValue) q = q.Where(i => i.ProyectoId == proyectoId.Value);

        var all = await q.ToListAsync();
        var delAnio = all.Where(i => i.Fecha.Year == anioFiltro).ToList();
        var delMes = delAnio.Where(i => i.Fecha.Month == mesActual).ToList();
        var todosHallazgos = all.SelectMany(i => i.Hallazgos).ToList();

        var tendencia = Enumerable.Range(1, 12).Select(m =>
        {
            var items = delAnio.Where(i => i.Fecha.Month == m).ToList();
            return new InspeccionTendenciaMensualDto
            {
                Anio = anioFiltro,
                Mes = m,
                MesNombre = new DateTime(anioFiltro, m, 1).ToString("MMM",
                    new System.Globalization.CultureInfo("es-PE")),
                Total = items.Count,
                TasaPromedio = items.Any(i => i.TasaCumplimiento.HasValue)
                    ? Math.Round(items.Where(i => i.TasaCumplimiento.HasValue)
                        .Average(i => i.TasaCumplimiento!.Value), 1)
                    : null
            };
        }).ToList();

        var porTipo = delAnio
            .GroupBy(i => new { i.TipoId, Nombre = i.Tipo?.Nombre ?? "", Ambito = i.Tipo?.Ambito ?? "" })
            .Select(g => new InspeccionPorTipoDto
            {
                TipoNombre = g.Key.Nombre,
                Ambito = g.Key.Ambito,
                Total = g.Count(),
                TasaPromedio = g.Any(i => i.TasaCumplimiento.HasValue)
                    ? Math.Round(g.Where(i => i.TasaCumplimiento.HasValue)
                        .Average(i => i.TasaCumplimiento!.Value), 1)
                    : null
            })
            .OrderByDescending(t => t.Total)
            .Take(10)
            .ToList();

        var hallazgosPorArea = todosHallazgos
            .Where(h => !string.IsNullOrEmpty(h.Area))
            .GroupBy(h => h.Area!)
            .Select(g => new InspeccionHallazgoPorAreaDto
            {
                Area = g.Key,
                Total = g.Count(),
                Criticos = g.Count(h => h.Tipo == "Critico"),
                Abiertos = g.Count(h => h.Estado == "Abierto")
            })
            .OrderByDescending(a => a.Criticos)
            .Take(10)
            .ToList();

        var recurrentes = todosHallazgos
            .GroupBy(h => h.Descripcion.ToLower().Trim())
            .Where(g => g.Count() > 1)
            .Select(g => new InspeccionHallazgoRecurrenteDto
            {
                Descripcion = g.First().Descripcion,
                Ocurrencias = g.Count(),
                UltimoTipo = g.OrderByDescending(h => h.CreatedAt).First().Tipo
            })
            .OrderByDescending(r => r.Ocurrencias)
            .Take(5)
            .ToList();

        return new InspeccionDashboardDto
        {
            TotalInspecciones = all.Count,
            TotalEsteMes = delMes.Count,
            HallazgosAbiertos = todosHallazgos.Count(h => h.Estado == "Abierto"),
            HallazgosCriticosAbiertos = todosHallazgos.Count(h => h.Tipo == "Critico" && h.Estado == "Abierto"),
            TasaCumplimientoPromedio = all.Any(i => i.TasaCumplimiento.HasValue)
                ? Math.Round(all.Where(i => i.TasaCumplimiento.HasValue)
                    .Average(i => i.TasaCumplimiento!.Value), 1)
                : null,
            TasaCumplimientoEsteMes = delMes.Any(i => i.TasaCumplimiento.HasValue)
                ? Math.Round(delMes.Where(i => i.TasaCumplimiento.HasValue)
                    .Average(i => i.TasaCumplimiento!.Value), 1)
                : null,
            TendenciaMensual = tendencia,
            PorTipo = porTipo,
            HallazgosPorArea = hallazgosPorArea,
            HallazgosRecurrentes = recurrentes
        };
    }
}
