using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.Rac.Dtos;
using Abril_Backend.Features.Ssoma.Rac.Entities;
using Abril_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.Rac.Services;

public class RacService : IRacService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IRacSharePointService _spService;
    private readonly IRacNotificationService _notifService;
    private readonly ILogger<RacService> _logger;

    public RacService(
        IDbContextFactory<AppDbContext> factory,
        IRacSharePointService spService,
        IRacNotificationService notifService,
        ILogger<RacService> logger)
    {
        _factory      = factory;
        _spService    = spService;
        _notifService = notifService;
        _logger       = logger;
    }

    public async Task<List<RacCategoriaDto>> GetCategoriasAsync()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsomaRacCategorias
            .Where(c => c.Activo)
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Nombre)
            .Select(c => new RacCategoriaDto
            {
                Id     = c.Id,
                Nombre = c.Nombre,
                Tipo   = c.Tipo,
                Orden  = c.Orden
            })
            .ToListAsync();
    }

    public Task<RacPagedResult<RacListItemDto>> GetListAsync(RacListQuery q)           => throw new NotImplementedException();
    public Task<RacDetalleDto?> GetDetalleAsync(int id)                                => throw new NotImplementedException();
    public async Task<RacCreadoDto> CrearAsync(RacCreateRequest req, int userId)
    {
        // ── Validaciones ──────────────────────────────────────────────────────
        if (!req.EsAnonimoReportante && req.ReportanteId is null)
            throw new AbrilException("El id del reportante es requerido cuando no es anónimo.", 400);

        if (req.AplicaPenalidad && req.InfraccionId is null)
            throw new AbrilException("El campo InfraccionId es requerido cuando aplica penalidad.", 400);

        using var ctx = _factory.CreateDbContext();

        if (req.AplicaPenalidad && req.EmpresaReportadaId.HasValue)
        {
            var esAbril = await ctx.Contributor
                .Where(c => c.ContributorId == req.EmpresaReportadaId.Value)
                .Select(c => c.EsAbril)
                .FirstOrDefaultAsync();
            if (esAbril)
                throw new AbrilException("La empresa Abril Ingeniería no puede ser objeto de penalidad.", 422);
        }

        // ── Snapshot reportante ───────────────────────────────────────────────
        string? reportanteNombre = req.ReportanteNombre;
        string? reportanteCargo  = req.ReportanteCargo;
        if (!req.EsAnonimoReportante && req.ReportanteId.HasValue)
        {
            var persona = await ctx.Person
                .Where(p => p.UserId == req.ReportanteId.Value)
                .Select(p => new { p.FullName, p.PersonId })
                .FirstOrDefaultAsync();
            if (persona is not null)
            {
                if (!string.IsNullOrEmpty(persona.FullName))
                    reportanteNombre = persona.FullName;

                var worker = await ctx.Worker
                    .Where(w => w.PersonId == persona.PersonId)
                    .Select(w => new { w.Ocupacion })
                    .FirstOrDefaultAsync();
                if (!string.IsNullOrEmpty(worker?.Ocupacion))
                    reportanteCargo = worker.Ocupacion;
            }
        }

        // ── UIT e infracción para penalidad (snapshot) ────────────────────────
        decimal uitValor = 0m;
        SsomaRacInfraccion? infraccion = null;
        if (req.AplicaPenalidad)
        {
            uitValor = await ctx.SsomaUitAnios
                .Where(u => u.Activo)
                .Select(u => u.Valor)
                .FirstOrDefaultAsync();

            if (req.InfraccionId.HasValue)
                infraccion = await ctx.SsomaRacInfracciones
                    .Where(i => i.Id == req.InfraccionId.Value)
                    .FirstOrDefaultAsync();
        }

        // ── Transacción ───────────────────────────────────────────────────────
        await using var tx = await ctx.Database.BeginTransactionAsync();
        RacCreadoDto resultado;
        int racId;
        string racCodigo;

        try
        {
            var project = await ctx.Project
                .Where(p => p.ProjectId == req.ProyectoId)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException("Proyecto no encontrado.", 404);

            var abbrev = project.Abbreviation ?? req.ProyectoId.ToString();
            var year   = DateTime.UtcNow.Year;

            var nuevoContadorRac = project.ContadorRac + 1;
            var codigoRac        = $"RAC-{year}-{abbrev}-{nuevoContadorRac:D3}";

            var rac = new SsomaRac
            {
                Codigo              = codigoRac,
                ProyectoId          = req.ProyectoId,
                Tipo                = req.Tipo,
                CategoriaId         = req.CategoriaId,
                Severidad           = req.Severidad,
                EsAnonimoReportante = req.EsAnonimoReportante,
                ReportanteId        = req.ReportanteId,
                ReportanteNombre    = reportanteNombre,
                ReportanteCargo     = reportanteCargo,
                EmpresaReportanteId = req.EmpresaReportanteId,
                EsAnonimoObservado  = req.EsAnonimoObservado,
                ObservadoWorkerId   = req.ObservadoWorkerId,
                EmpresaReportadaId  = req.EmpresaReportadaId,
                ProyectoPiso        = req.ProyectoPiso,
                LugarDescripcion    = req.LugarDescripcion,
                Latitud             = req.Latitud,
                Longitud            = req.Longitud,
                Descripcion         = req.Descripcion,
                PlanAccion          = req.PlanAccion,
                FechaReporte        = req.FechaReporte,
                PlazoLevantamiento  = req.PlazoLevantamiento,
                AplicaPenalidad     = req.AplicaPenalidad,
                CreatedBy           = userId,
                CreatedAt           = DateTime.UtcNow
            };

            ctx.SsomaRacs.Add(rac);
            project.ContadorRac = nuevoContadorRac;

            SsomaRacPenalidad? penalidad = null;
            if (req.AplicaPenalidad)
            {
                var nuevoContadorPen = project.ContadorPenalidad + 1;
                var codigoPen        = $"PEN-{year}-{abbrev}-{nuevoContadorPen:D3}";

                decimal monto = infraccion is not null
                    ? infraccion.MontoFijo.HasValue
                        ? infraccion.MontoFijo.Value
                        : infraccion.FactorUit.HasValue
                            ? Math.Round(infraccion.FactorUit.Value * uitValor, 2)
                            : 0m
                    : 0m;

                penalidad = new SsomaRacPenalidad
                {
                    Codigo              = codigoPen,
                    Rac                 = rac,
                    EmpresaId           = req.EmpresaReportadaId,
                    ProyectoId          = req.ProyectoId,
                    InfraccionId        = req.InfraccionId,
                    MontoCalculado      = monto,
                    UitReferencia       = uitValor,
                    DescripcionOcurrido = req.DescripcionOcurrido,
                    CreatedBy           = userId,
                    CreatedAt           = DateTime.UtcNow
                };

                ctx.SsomaRacPenalidades.Add(penalidad);
                project.ContadorPenalidad = nuevoContadorPen;
            }

            await ctx.SaveChangesAsync();
            await tx.CommitAsync();

            racId     = rac.Id;
            racCodigo = rac.Codigo;
            resultado = new RacCreadoDto
            {
                Id              = rac.Id,
                Codigo          = rac.Codigo,
                PenalidadId     = penalidad?.Id,
                PenalidadCodigo = penalidad?.Codigo
            };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        // ── PDF + SharePoint + Email (best-effort) ────────────────────────────
        try
        {
            var detalle = await GetDetalleAsync(racId);
            if (detalle != null)
            {
                var pdfBytes = RacPdfService.GenerarPdf(detalle);
                using var pdfStream = new MemoryStream(pdfBytes);
                var pdfPath = await _spService.SubirPdfAsync(pdfStream, $"{racCodigo}.pdf", racId);

                using var ctx2 = _factory.CreateDbContext();
                var racRow = await ctx2.SsomaRacs.FindAsync(racId);
                if (racRow != null)
                {
                    racRow.PdfUrl = pdfPath;
                    await ctx2.SaveChangesAsync();
                }

                _ = Task.Run(async () =>
                {
                    try { await _notifService.NotificarRacCreadoAsync(detalle); }
                    catch { /* silencioso */ }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error post-creación RAC {Codigo}: PDF/SP/email no procesado", racCodigo);
        }

        return resultado;
    }
    public Task<RacDetalleDto> CerrarAsync(int id, RacCerrarRequest req, int userId)   => throw new NotImplementedException();
    public Task<RacFotoUploadResult> SubirFotoAsync(int racId, IFormFile file, string tipo, int userId) => throw new NotImplementedException();
    public byte[] GenerarPdf(RacDetalleDto rac) => RacPdfService.GenerarPdf(rac);

    public async Task<byte[]> GetPdfAsync(int id)
    {
        var rac = await GetDetalleAsync(id)
            ?? throw new AbrilException("RAC no encontrado.", 404);
        return RacPdfService.GenerarPdf(rac);
    }
    public Task<RacDashboardDto> GetDashboardAsync()                                   => throw new NotImplementedException();
    public async Task<List<RacInfraccionDto>> GetInfraccionesAsync()
    {
        using var ctx = _factory.CreateDbContext();

        var uit = await ctx.SsomaUitAnios
            .Where(u => u.Activo)
            .Select(u => u.Valor)
            .FirstOrDefaultAsync();

        var infracciones = await ctx.SsomaRacInfracciones
            .Where(i => i.Activo)
            .OrderBy(i => i.Nombre)
            .ToListAsync();

        return infracciones.Select(i => new RacInfraccionDto
        {
            Id             = i.Id,
            Nombre         = i.Nombre,
            FactorUit      = i.FactorUit,
            MontoFijo      = i.MontoFijo,
            Descripcion    = i.Descripcion,
            UitReferencia  = uit,
            MontoCalculado = i.MontoFijo.HasValue
                ? i.MontoFijo.Value
                : i.FactorUit.HasValue
                    ? Math.Round(i.FactorUit.Value * uit, 2)
                    : 0m
        }).ToList();
    }
}
