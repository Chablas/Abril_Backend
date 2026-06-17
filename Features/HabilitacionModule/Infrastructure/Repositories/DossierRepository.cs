using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Dossier;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories;

public class DossierRepository : IDossierRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    private static readonly string[] TiposDoc =
        ["Accidente", "EPP", "Estadisticas", "Capacitaciones", "PETAR", "ATS", "Charlas"];

    public DossierRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<DossierSemanaDto>> GetSemanasAsync(int? contributorId, int? proyectoId, int? anio)
    {
        using var ctx = _factory.CreateDbContext();
        var query = ctx.SsDossierSemana.Include(s => s.Documentos).AsQueryable();
        if (contributorId.HasValue) query = query.Where(s => s.ContributorId == contributorId.Value);
        if (proyectoId.HasValue) query = query.Where(s => s.ProyectoId == proyectoId.Value);
        if (anio.HasValue) query = query.Where(s => s.Anio == anio.Value);

        return await query
            .OrderByDescending(s => s.Anio)
            .ThenByDescending(s => s.NumeroSemana)
            .Select(s => new DossierSemanaDto(
                s.Id, s.ContributorId, s.ProyectoId,
                s.Anio, s.NumeroSemana, s.FechaInicio, s.FechaFin,
                s.Estado, s.ObsRevisor, s.CreatedAt,
                s.Documentos.Count,
                s.Documentos.Count(d => d.Estado == "Subido"),
                s.Documentos.Count(d => d.Estado == "NA")))
            .ToListAsync();
    }

    public async Task<DossierSemanaDetalleDto?> GetDetalleAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var s = await ctx.SsDossierSemana
            .Include(s => s.Documentos)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (s == null) return null;

        var docs = s.Documentos
            .OrderBy(d => Array.IndexOf(TiposDoc, d.TipoDoc))
            .Select(d => new DossierDocumentoDto(
                d.Id, d.DossierId, d.TipoDoc,
                d.NombreArchivo, d.ArchivoPath,
                d.Estado, d.CreatedAt, d.UpdatedAt))
            .ToList();

        return new DossierSemanaDetalleDto(
            s.Id, s.ContributorId, s.ProyectoId,
            s.Anio, s.NumeroSemana, s.FechaInicio, s.FechaFin,
            s.Estado, s.ObsRevisor, s.CreatedAt, docs);
    }

    public async Task<(int Id, DateTime FechaInicio, DateTime FechaFin)> EnsureSemanaAsync(EnsureSemanaRequest req)
    {
        var fechaInicio = DateTime.SpecifyKind(
            ISOWeek.ToDateTime(req.Anio, req.NumeroSemana, DayOfWeek.Monday),
            DateTimeKind.Utc);
        var fechaFin = fechaInicio.AddDays(6);
        var ahora = DateTime.UtcNow;

        using var ctx = _factory.CreateDbContext();

        await ctx.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ss_dossier_semana
                (contributor_id, proyecto_id, anio, numero_semana, fecha_inicio, fecha_fin, estado, created_at, updated_at)
              VALUES ({0}, {1}, {2}, {3}, {4}, {5}, 'Borrador', {6}, {7})
              ON CONFLICT (contributor_id, proyecto_id, anio, numero_semana) DO NOTHING",
            req.ContributorId, req.ProyectoId, req.Anio, req.NumeroSemana,
            fechaInicio, fechaFin, ahora, ahora);

        var semana = await ctx.SsDossierSemana
            .FirstAsync(s => s.ContributorId == req.ContributorId
                          && s.ProyectoId == req.ProyectoId
                          && s.Anio == req.Anio
                          && s.NumeroSemana == req.NumeroSemana);

        foreach (var tipo in TiposDoc)
        {
            await ctx.Database.ExecuteSqlRawAsync(
                @"INSERT INTO ss_dossier_documento (dossier_id, tipo_doc, estado, created_at, updated_at)
                  VALUES ({0}, {1}, 'Pendiente', {2}, {3})
                  ON CONFLICT (dossier_id, tipo_doc) DO NOTHING",
                semana.Id, tipo, ahora, ahora);
        }

        return (semana.Id, semana.FechaInicio, semana.FechaFin);
    }

    public async Task<(int ContributorId, int ProyectoId, int NumeroSemana, DateTime FechaInicio)?> GetDossierContextoAsync(int dossierId)
    {
        using var ctx = _factory.CreateDbContext();
        var s = await ctx.SsDossierSemana
            .Where(s => s.Id == dossierId)
            .Select(s => new { s.ContributorId, s.ProyectoId, s.NumeroSemana, s.FechaInicio })
            .FirstOrDefaultAsync();
        if (s == null) return null;
        return (s.ContributorId, s.ProyectoId, s.NumeroSemana, s.FechaInicio);
    }

    public async Task SubirDocumentoAsync(int dossierId, string tipoDoc, string nombreArchivo, string archivoPath)
    {
        using var ctx = _factory.CreateDbContext();
        var doc = await ctx.SsDossierDocumento
            .FirstOrDefaultAsync(d => d.DossierId == dossierId && d.TipoDoc == tipoDoc)
            ?? throw new AbrilException("Documento no encontrado.", 404);

        doc.NombreArchivo = nombreArchivo;
        doc.ArchivoPath = archivoPath;
        doc.Estado = "Subido";
        doc.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task MarcarNaAsync(int docId)
    {
        using var ctx = _factory.CreateDbContext();
        var doc = await ctx.SsDossierDocumento.FindAsync(docId)
            ?? throw new AbrilException("Documento no encontrado.", 404);
        doc.Estado = doc.Estado == "NA" ? "Pendiente" : "NA";
        doc.NombreArchivo = null;
        doc.ArchivoPath = null;
        doc.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task EnviarAsync(int dossierId)
    {
        using var ctx = _factory.CreateDbContext();
        var semana = await ctx.SsDossierSemana.FindAsync(dossierId)
            ?? throw new AbrilException("Dossier no encontrado.", 404);
        semana.Estado = "Enviado";
        semana.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task RevisarAsync(int dossierId, RevisarDossierRequest req)
    {
        using var ctx = _factory.CreateDbContext();
        var semana = await ctx.SsDossierSemana.FindAsync(dossierId)
            ?? throw new AbrilException("Dossier no encontrado.", 404);
        semana.Estado = req.Estado;
        semana.ObsRevisor = req.ObsRevisor;
        semana.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task<string?> GetArchivoPathAsync(int docId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsDossierDocumento
            .Where(d => d.Id == docId)
            .Select(d => d.ArchivoPath)
            .FirstOrDefaultAsync();
    }
}
