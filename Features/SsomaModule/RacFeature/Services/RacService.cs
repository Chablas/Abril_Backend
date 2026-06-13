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

    public async Task<RacPagedResult<RacListItemDto>> GetListAsync(RacListQuery q)
    {
        using var ctx = _factory.CreateDbContext();

        var query = ctx.SsomaRacs.AsQueryable();

        if (q.ProyectoId.HasValue)
            query = query.Where(r => r.ProyectoId == q.ProyectoId.Value);
        if (!string.IsNullOrEmpty(q.Estado))
            query = query.Where(r => r.Estado == q.Estado);
        if (!string.IsNullOrEmpty(q.Severidad))
            query = query.Where(r => r.Severidad == q.Severidad);
        if (!string.IsNullOrEmpty(q.Tipo))
            query = query.Where(r => r.Tipo == q.Tipo);
        if (q.EmpresaReportadaId.HasValue)
            query = query.Where(r => r.EmpresaReportadaId == q.EmpresaReportadaId.Value);
        if (q.FechaDesde.HasValue)
            query = query.Where(r => r.FechaReporte >= q.FechaDesde.Value);
        if (q.FechaHasta.HasValue)
            query = query.Where(r => r.FechaReporte <= q.FechaHasta.Value);
        if (q.SoloConPenalidad == true)
            query = query.Where(r => r.AplicaPenalidad);

        var total    = await query.CountAsync();
        var page     = q.Page < 1 ? 1 : q.Page;
        var pageSize = q.PageSize <= 0 ? 20 : Math.Min(q.PageSize, 100);

        var items = await query
            .OrderByDescending(r => r.FechaReporte)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RacListItemDto
            {
                Id                    = r.Id,
                Codigo                = r.Codigo,
                ProyectoNombre        = ctx.Project
                    .Where(p => p.ProjectId == r.ProyectoId)
                    .Select(p => p.ProjectDescription)
                    .FirstOrDefault(),
                ProyectoAbreviacion   = ctx.Project
                    .Where(p => p.ProjectId == r.ProyectoId)
                    .Select(p => p.Abbreviation)
                    .FirstOrDefault(),
                Tipo                  = r.Tipo,
                CategoriaNombre       = r.Categoria.Nombre,
                CategoriaAmbito       = r.Categoria.Ambito ?? "",
                Severidad             = r.Severidad,
                Estado                = r.Estado,
                FechaReporte          = r.FechaReporte,
                PlazoLevantamiento    = r.PlazoLevantamiento,
                AplicaPenalidad       = r.AplicaPenalidad,
                EmpresaReportadaNombre = r.EmpresaReportadaId != null
                    ? ctx.Contributor
                        .Where(c => c.ContributorId == r.EmpresaReportadaId)
                        .Select(c => c.ContributorName)
                        .FirstOrDefault()
                    : null,
                ReportanteNombre      = r.ReportanteNombre,
            })
            .ToListAsync();

        return new RacPagedResult<RacListItemDto>
        {
            Items    = items,
            Total    = total,
            Page     = page,
            PageSize = pageSize
        };
    }

    public async Task<RacDetalleDto?> GetDetalleAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();

        var rac = await ctx.SsomaRacs
            .Include(r => r.Categoria)
            .Include(r => r.Fotos.OrderBy(f => f.Orden))
            .Include(r => r.Penalidad)
                .ThenInclude(p => p!.Infraccion)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rac == null) return null;

        var proyectoNombre = await ctx.Project
            .Where(p => p.ProjectId == rac.ProyectoId)
            .Select(p => p.ProjectDescription)
            .FirstOrDefaultAsync();

        string? reportanteNombre = rac.ReportanteNombre;
        if (rac.ReportanteId.HasValue && string.IsNullOrEmpty(reportanteNombre))
        {
            reportanteNombre = await ctx.Person
                .Where(p => p.UserId == rac.ReportanteId.Value)
                .Select(p => p.FullName)
                .FirstOrDefaultAsync();
        }

        string? observadoNombre = null;
        if (rac.ObservadoWorkerId.HasValue)
        {
            observadoNombre = await ctx.Worker
                .Where(w => w.Id == rac.ObservadoWorkerId.Value)
                .Select(w => w.Person != null ? w.Person.FullName : null)
                .FirstOrDefaultAsync();
        }

        string? empReportanteNombre = null;
        if (rac.EmpresaReportanteId.HasValue)
        {
            empReportanteNombre = await ctx.Contributor
                .Where(c => c.ContributorId == rac.EmpresaReportanteId.Value)
                .Select(c => c.ContributorName)
                .FirstOrDefaultAsync();
        }

        string? empReportadaNombre = null;
        if (rac.EmpresaReportadaId.HasValue)
        {
            empReportadaNombre = await ctx.Contributor
                .Where(c => c.ContributorId == rac.EmpresaReportadaId.Value)
                .Select(c => c.ContributorName)
                .FirstOrDefaultAsync();
        }

        return new RacDetalleDto
        {
            Id                      = rac.Id,
            Codigo                  = rac.Codigo,
            ProyectoId              = rac.ProyectoId,
            ProyectoNombre          = proyectoNombre,
            Tipo                    = rac.Tipo,
            CategoriaId             = rac.CategoriaId,
            CategoriaNombre         = rac.Categoria.Nombre,
            CategoriaAmbito         = rac.Categoria.Ambito ?? "",
            Severidad               = rac.Severidad,
            EsAnonimoReportante     = rac.EsAnonimoReportante,
            ReportanteId            = rac.ReportanteId,
            ReportanteNombre        = reportanteNombre,
            ReportanteCargo         = rac.ReportanteCargo,
            EmpresaReportanteId     = rac.EmpresaReportanteId,
            EmpresaReportanteNombre = empReportanteNombre,
            EsAnonimoObservado      = rac.EsAnonimoObservado,
            ObservadoWorkerId       = rac.ObservadoWorkerId,
            ObservadoNombre         = observadoNombre,
            EmpresaReportadaId      = rac.EmpresaReportadaId,
            EmpresaReportadaNombre  = empReportadaNombre,
            ProyectoPiso            = rac.ProyectoPiso,
            LugarDescripcion        = rac.LugarDescripcion,
            Latitud                 = rac.Latitud,
            Longitud                = rac.Longitud,
            Descripcion             = rac.Descripcion,
            PlanAccion              = rac.PlanAccion,
            FechaReporte            = rac.FechaReporte,
            PlazoLevantamiento      = rac.PlazoLevantamiento,
            Estado                  = rac.Estado,
            FechaCierre             = rac.FechaCierre,
            CierreDescripcion       = rac.CierreDescripcion,
            AplicaPenalidad         = rac.AplicaPenalidad,
            PdfUrl                  = rac.PdfUrl,
            CreatedAt               = rac.CreatedAt,
            Fotos = rac.Fotos.Select(f => new RacFotoDto
            {
                Id            = f.Id,
                Url           = f.Url,
                Tipo          = f.Tipo,
                NombreArchivo = f.NombreArchivo,
                Orden         = f.Orden
            }).ToList(),
            Penalidad = rac.Penalidad is null ? null : new RacPenalidadResumenDto
            {
                Id               = rac.Penalidad.Id,
                Codigo           = rac.Penalidad.Codigo,
                Estado           = rac.Penalidad.Estado,
                MontoCalculado   = rac.Penalidad.MontoCalculado,
                InfraccionNombre = rac.Penalidad.Infraccion?.Nombre
            }
        };
    }
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
    public async Task<RacDetalleDto> CerrarAsync(int id, RacCerrarRequest req, int userId)
    {
        using var ctx = _factory.CreateDbContext();

        var rac = await ctx.SsomaRacs.FindAsync(id)
            ?? throw new AbrilException("RAC no encontrado.", 404);

        if (rac.Estado == "Cerrado")
            throw new AbrilException("El RAC ya está cerrado.", 400);

        rac.Estado            = "Cerrado";
        rac.FechaCierre       = DateTime.UtcNow;
        rac.CierreDescripcion = req.CierreDescripcion;
        rac.CerradoPorId      = userId;
        rac.UpdatedAt         = DateTime.UtcNow;

        await ctx.SaveChangesAsync();

        return await GetDetalleAsync(id)
            ?? throw new AbrilException("RAC no encontrado.", 404);
    }

    public async Task<RacFotoUploadResult> SubirFotoAsync(int racId, IFormFile file, string tipo, int userId)
    {
        using var ctx = _factory.CreateDbContext();

        var existe = await ctx.SsomaRacs.AnyAsync(r => r.Id == racId);
        if (!existe)
            throw new AbrilException("RAC no encontrado.", 404);

        var orden = await ctx.SsomaRacFotos.CountAsync(f => f.RacId == racId) + 1;

        using var stream = file.OpenReadStream();
        var path = await _spService.SubirFotoAsync(stream, file.FileName, racId);

        var foto = new SsomaRacFoto
        {
            RacId         = racId,
            Url           = path,
            Tipo          = tipo,
            NombreArchivo = file.FileName,
            Orden         = orden,
            SubidoPor     = userId,
            CreatedAt     = DateTime.UtcNow
        };

        ctx.SsomaRacFotos.Add(foto);
        await ctx.SaveChangesAsync();

        return new RacFotoUploadResult
        {
            Id            = foto.Id,
            Url           = foto.Url,
            Tipo          = foto.Tipo,
            NombreArchivo = foto.NombreArchivo ?? file.FileName
        };
    }
    public byte[] GenerarPdf(RacDetalleDto rac) => RacPdfService.GenerarPdf(rac);

    public async Task<byte[]> GetPdfAsync(int id)
    {
        var rac = await GetDetalleAsync(id)
            ?? throw new AbrilException("RAC no encontrado.", 404);
        return RacPdfService.GenerarPdf(rac);
    }
    public async Task<RacDashboardDto> GetDashboardAsync()
    {
        using var ctx = _factory.CreateDbContext();
        var ahora = DateTime.UtcNow;

        var totalAbiertos     = await ctx.SsomaRacs.CountAsync(r => r.Estado == "Abierto");
        var totalCerrados     = await ctx.SsomaRacs.CountAsync(r => r.Estado == "Cerrado");
        var totalConPenalidad = await ctx.SsomaRacs.CountAsync(r => r.AplicaPenalidad);
        var criticosAbiertos  = await ctx.SsomaRacs.CountAsync(r => r.Estado == "Abierto" && r.Severidad == "CRITICO");
        var altosAbiertos     = await ctx.SsomaRacs.CountAsync(r => r.Estado == "Abierto" && r.Severidad == "ALTO");
        var vencidosAbiertos  = await ctx.SsomaRacs.CountAsync(r => r.Estado == "Abierto" && r.PlazoLevantamiento < ahora);

        // ── PorProyecto ───────────────────────────────────────────────────────
        var porProyectoRaw = await ctx.SsomaRacs
            .GroupBy(r => r.ProyectoId)
            .Select(g => new
            {
                ProyectoId = g.Key,
                Total      = g.Count(),
                Abiertos   = g.Count(r => r.Estado == "Abierto"),
                Cerrados   = g.Count(r => r.Estado == "Cerrado")
            })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToListAsync();

        var proyIds = porProyectoRaw.Select(x => x.ProyectoId).ToList();
        var proyNombres = await ctx.Project
            .Where(p => proyIds.Contains(p.ProjectId))
            .Select(p => new { p.ProjectId, p.ProjectDescription })
            .ToListAsync();

        var porProyecto = porProyectoRaw.Select(x => new RacPorProyectoDto
        {
            ProyectoId     = x.ProyectoId,
            ProyectoNombre = proyNombres.FirstOrDefault(p => p.ProjectId == x.ProyectoId)?.ProjectDescription ?? "",
            Total          = x.Total,
            Abiertos       = x.Abiertos,
            Cerrados       = x.Cerrados
        }).ToList();

        // ── PorCategoria ──────────────────────────────────────────────────────
        var porCategoriaRaw = await ctx.SsomaRacs
            .GroupBy(r => r.CategoriaId)
            .Select(g => new { CategoriaId = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToListAsync();

        var catIds = porCategoriaRaw.Select(x => x.CategoriaId).ToList();
        var cats   = await ctx.SsomaRacCategorias
            .Where(c => catIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Nombre, c.Ambito })
            .ToListAsync();

        var porCategoria = porCategoriaRaw.Select(x => new RacPorCategoriaDto
        {
            CategoriaNombre = cats.FirstOrDefault(c => c.Id == x.CategoriaId)?.Nombre ?? "",
            Ambito          = cats.FirstOrDefault(c => c.Id == x.CategoriaId)?.Ambito ?? "",
            Total           = x.Total
        }).ToList();

        // ── Tendencia — últimos 6 meses ───────────────────────────────────────
        var fechaCorte = new DateTime(ahora.Year, ahora.Month, 1).AddMonths(-5);
        var tendencia  = await ctx.SsomaRacs
            .Where(r => r.FechaReporte >= fechaCorte)
            .GroupBy(r => new { r.FechaReporte.Year, r.FechaReporte.Month })
            .Select(g => new RacTendenciaDto
            {
                Anio        = g.Key.Year,
                Mes         = g.Key.Month,
                Total       = g.Count(),
                Actos       = g.Count(r => r.Tipo == "ACTO"),
                Condiciones = g.Count(r => r.Tipo == "CONDICION")
            })
            .OrderBy(x => x.Anio).ThenBy(x => x.Mes)
            .ToListAsync();

        return new RacDashboardDto
        {
            TotalAbiertos     = totalAbiertos,
            TotalCerrados     = totalCerrados,
            TotalConPenalidad = totalConPenalidad,
            CriticosAbiertos  = criticosAbiertos,
            AltosAbiertos     = altosAbiertos,
            VencidosAbiertos  = vencidosAbiertos,
            PorProyecto       = porProyecto,
            PorCategoria      = porCategoria,
            Tendencia         = tendencia
        };
    }
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
