using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Infrastructure.Repositories;

public class AccidenteIncidenteRepository : IAccidenteIncidenteRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public AccidenteIncidenteRepository(IDbContextFactory<AppDbContext> factory)
        => _factory = factory;

    public async Task<List<AccidenteIncidenteListItemDto>> GetListAsync(
        int? proyectoId, string? tipo, string? estado,
        DateTime? fechaDesde, DateTime? fechaHasta, int page, int pageSize)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsomaAccidenteIncidente
            .Include(a => a.Proyecto)
            .Include(a => a.Documentos)
            .AsQueryable();

        if (proyectoId.HasValue) q = q.Where(a => a.ProyectoId == proyectoId.Value);
        if (!string.IsNullOrEmpty(tipo)) q = q.Where(a => a.Tipo == tipo);
        if (!string.IsNullOrEmpty(estado)) q = q.Where(a => a.Estado == estado);
        if (fechaDesde.HasValue) q = q.Where(a => a.Fecha >= DateTime.SpecifyKind(fechaDesde.Value.Date, DateTimeKind.Utc));
        if (fechaHasta.HasValue) q = q.Where(a => a.Fecha <= DateTime.SpecifyKind(fechaHasta.Value.Date, DateTimeKind.Utc));

        return await q
            .OrderByDescending(a => a.Fecha)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AccidenteIncidenteListItemDto
            {
                Id = a.Id,
                ProyectoNombre = a.Proyecto != null ? a.Proyecto.ProjectDescription : "",
                Fecha = a.Fecha,
                Descripcion = a.Descripcion,
                Tipo = a.Tipo,
                Estado = a.Estado,
                TotalDocumentos = a.Documentos.Count,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<int> GetListCountAsync(
        int? proyectoId, string? tipo, string? estado,
        DateTime? fechaDesde, DateTime? fechaHasta)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsomaAccidenteIncidente.AsQueryable();
        if (proyectoId.HasValue) q = q.Where(a => a.ProyectoId == proyectoId.Value);
        if (!string.IsNullOrEmpty(tipo)) q = q.Where(a => a.Tipo == tipo);
        if (!string.IsNullOrEmpty(estado)) q = q.Where(a => a.Estado == estado);
        if (fechaDesde.HasValue) q = q.Where(a => a.Fecha >= DateTime.SpecifyKind(fechaDesde.Value.Date, DateTimeKind.Utc));
        if (fechaHasta.HasValue) q = q.Where(a => a.Fecha <= DateTime.SpecifyKind(fechaHasta.Value.Date, DateTimeKind.Utc));
        return await q.CountAsync();
    }

    public async Task<AccidenteIncidenteDetalleDto?> GetDetalleAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var a = await ctx.SsomaAccidenteIncidente
            .Include(x => x.Proyecto)
            .Include(x => x.Documentos)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (a == null) return null;

        return new AccidenteIncidenteDetalleDto
        {
            Id = a.Id,
            ProyectoId = a.ProyectoId,
            ProyectoNombre = a.Proyecto?.ProjectDescription ?? "",
            Fecha = a.Fecha,
            Descripcion = a.Descripcion,
            Tipo = a.Tipo,
            Estado = a.Estado,
            ResponsableId = a.ResponsableId,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt,
            Documentos = a.Documentos.OrderByDescending(d => d.CreatedAt).Select(d => new DocumentoAdjuntoDto
            {
                Id = d.Id,
                NombreArchivo = d.NombreArchivo,
                TipoArchivo = d.TipoArchivo,
                TamanioBytes = d.TamanioBytes,
                UrlSharepoint = d.UrlSharepoint,
                CreatedAt = d.CreatedAt
            }).ToList()
        };
    }

    public async Task<int> CrearAsync(CrearAccidenteIncidenteRequest request, int? usuarioId)
    {
        using var ctx = _factory.CreateDbContext();
        var entity = new SsomaAccidenteIncidente
        {
            ProyectoId = request.ProyectoId,
            Fecha = DateTime.SpecifyKind(request.Fecha.Date, DateTimeKind.Utc),
            Descripcion = request.Descripcion,
            Tipo = request.Tipo,
            Estado = request.Estado,
            ResponsableId = request.ResponsableId,
            UsuarioId = usuarioId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.SsomaAccidenteIncidente.Add(entity);
        await ctx.SaveChangesAsync();
        return entity.Id;
    }

    public async Task ActualizarAsync(int id, ActualizarAccidenteIncidenteRequest request)
    {
        using var ctx = _factory.CreateDbContext();
        var entity = await ctx.SsomaAccidenteIncidente.FindAsync(id)
            ?? throw new AbrilException("Accidente/Incidente no encontrado.", 404);
        entity.ProyectoId = request.ProyectoId;
        entity.Fecha = DateTime.SpecifyKind(request.Fecha.Date, DateTimeKind.Utc);
        entity.Descripcion = request.Descripcion;
        entity.Tipo = request.Tipo;
        entity.Estado = request.Estado;
        entity.ResponsableId = request.ResponsableId;
        entity.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task EliminarAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var entity = await ctx.SsomaAccidenteIncidente.FindAsync(id)
            ?? throw new AbrilException("Accidente/Incidente no encontrado.", 404);
        ctx.SsomaAccidenteIncidente.Remove(entity);
        await ctx.SaveChangesAsync();
    }

    public async Task<int> SubirDocumentoAsync(int accidenteId, SubirDocumentoRequest request, string url, int? usuarioId)
    {
        using var ctx = _factory.CreateDbContext();
        var doc = new SsomaAccidenteDocumento
        {
            AccidenteId = accidenteId,
            NombreArchivo = request.NombreArchivo,
            TipoArchivo = request.TipoArchivo,
            TamanioBytes = request.TamanioBytes,
            UrlSharepoint = url,
            UsuarioId = usuarioId,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SsomaAccidenteDocumento.Add(doc);
        await ctx.SaveChangesAsync();
        return doc.Id;
    }

    public async Task<DocumentoAdjuntoDto?> GetDocumentoAsync(int accidenteId, int docId)
    {
        using var ctx = _factory.CreateDbContext();
        var doc = await ctx.SsomaAccidenteDocumento
            .FirstOrDefaultAsync(d => d.Id == docId && d.AccidenteId == accidenteId);
        if (doc == null) return null;
        return new DocumentoAdjuntoDto
        {
            Id = doc.Id,
            NombreArchivo = doc.NombreArchivo,
            TipoArchivo = doc.TipoArchivo,
            TamanioBytes = doc.TamanioBytes,
            UrlSharepoint = doc.UrlSharepoint,
            CreatedAt = doc.CreatedAt
        };
    }
}
