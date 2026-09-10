using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.Penalidad.Dtos;
using Abril_Backend.Features.Ssoma.Penalidad.Entities;
using Abril_Backend.Features.Ssoma.Rac.Entities;
using Abril_Backend.Features.Ssoma.Rac.Services;
using Abril_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.Penalidad.Services;

public class PenalidadService : IPenalidadService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IRacSharePointService _spService;
    private readonly IPenalidadNotificationService _notif;
    private readonly ILogger<PenalidadService> _logger;

    public PenalidadService(
        IDbContextFactory<AppDbContext> factory,
        IRacSharePointService spService,
        IPenalidadNotificationService notif,
        ILogger<PenalidadService> logger)
    {
        _factory   = factory;
        _spService = spService;
        _notif     = notif;
        _logger    = logger;
    }

    // ── Lectura ──────────────────────────────────────────────────────────────

    public async Task<PagedResult<PenalidadListItemDto>> GetListAsync(PenalidadListQuery q)
    {
        using var ctx = _factory.CreateDbContext();

        var query = ctx.SsomaPenalidades.AsQueryable();

        var empresaFiltro = q.EmpresaIdContratista ?? q.EmpresaId;
        if (empresaFiltro.HasValue)
            query = query.Where(p => p.EmpresaId == empresaFiltro.Value);
        if (q.ProyectoId.HasValue)
            query = query.Where(p => p.ProyectoId == q.ProyectoId.Value);
        if (!string.IsNullOrEmpty(q.Estado))
            query = query.Where(p => p.Estado == q.Estado);

        var total    = await query.CountAsync();
        var page     = q.Page < 1 ? 1 : q.Page;
        var pageSize = q.PageSize <= 0 ? 20 : Math.Min(q.PageSize, 100);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PenalidadListItemDto
            {
                Id                   = p.Id,
                Codigo               = p.Codigo,
                OrigenTipo           = p.OrigenTipo,
                OrigenId             = p.OrigenId,
                ProyectoNombre       = ctx.Project.Where(pr => pr.ProjectId == p.ProyectoId).Select(pr => pr.ProjectDescription).FirstOrDefault(),
                EmpresaNombre        = ctx.Contributor.Where(c => c.ContributorId == p.EmpresaId).Select(c => c.ContributorName).FirstOrDefault(),
                InfraccionNombre     = p.Infraccion != null ? p.Infraccion.Nombre : null,
                Severidad            = p.Severidad,
                MontoCalculado       = p.MontoCalculado,
                MontoFinal           = p.MontoFinal,
                Estado               = p.Estado,
                CreatedAt            = p.CreatedAt,
                PlazoDescargoVenceEn = p.PlazoDescargoVenceEn,
                ResueltaEn           = p.ResueltaEn,
                ResolucionTipo       = p.ResolucionTipo,
            })
            .ToListAsync();

        return new PagedResult<PenalidadListItemDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<PenalidadDetalleDto?> GetDetalleAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var pen = await ctx.SsomaPenalidades
            .Include(p => p.Infraccion)
            .Include(p => p.Historial)
            .FirstOrDefaultAsync(p => p.Id == id);

        return pen is null ? null : await MapDetalleAsync(ctx, pen);
    }

    private async Task<PenalidadDetalleDto> MapDetalleAsync(AppDbContext ctx, SsomaPenalidad pen)
    {
        var proyectoNombre = await ctx.Project.Where(p => p.ProjectId == pen.ProyectoId).Select(p => p.ProjectDescription).FirstOrDefaultAsync();
        var empresaNombre  = await ctx.Contributor.Where(c => c.ContributorId == pen.EmpresaId).Select(c => c.ContributorName).FirstOrDefaultAsync();

        // El argumento/recomendación de SSOMA es información interna de deliberación: solo se
        // expone una vez que Gerencia decidió (Aplicada/Anulada) — antes de eso el contratista
        // (y cualquier consumidor de este DTO) no debe verlo, para no viciar la decisión final.
        var decisionFirme = pen.Estado is "Aplicada" or "Anulada" or "EnApelacion";

        return new PenalidadDetalleDto
        {
            Id                       = pen.Id,
            Codigo                   = pen.Codigo,
            OrigenTipo               = pen.OrigenTipo,
            OrigenId                 = pen.OrigenId,
            ProyectoNombre           = proyectoNombre,
            EmpresaNombre            = empresaNombre,
            InfraccionNombre         = pen.Infraccion?.Nombre,
            Severidad                = pen.Severidad,
            MontoCalculado           = pen.MontoCalculado,
            MontoFinal               = pen.MontoFinal,
            Estado                   = pen.Estado,
            CreatedAt                = pen.CreatedAt,
            PlazoDescargoVenceEn     = pen.PlazoDescargoVenceEn,
            ResueltaEn               = pen.ResueltaEn,
            ResolucionTipo           = pen.ResolucionTipo,
            EmpresaId                = pen.EmpresaId,
            ProyectoId               = pen.ProyectoId,
            InfraccionId             = pen.InfraccionId,
            DescripcionOcurrido      = pen.DescripcionOcurrido,
            UitReferencia            = pen.UitReferencia,
            MotivoAjusteMonto        = pen.MontoAjustadoMotivo,
            MotivoRechazoResidente   = pen.MotivoRechazoResidente,
            MotivoRechazoGerencia    = pen.MotivoRechazoGerencia,
            DescargoTexto            = pen.DescargoTexto,
            DocumentoUrl             = pen.DocumentoUrl,
            DescargoFecha            = pen.DescargoFecha,
            DescargoPorIncomparecencia = pen.DescargoPorIncomparecencia,
            ArgumentoSsoma           = decisionFirme ? pen.ArgumentoSsoma : null,
            RecomendacionSsoma       = decisionFirme ? pen.RecomendacionSsoma : null,
            ResolucionTexto          = pen.ResolucionTexto,
            MotivoObjecionGerencia   = pen.MotivoObjecionGerencia,
            PdfNotificacionUrl       = pen.PdfNotificacionUrl,
            PdfResolucionUrl         = pen.PdfResolucionUrl,
            ApelacionUsada           = pen.ApelacionUsada,
            PlazoApelacionVenceEn    = pen.PlazoApelacionVenceEn,
            ApelacionTexto           = pen.ApelacionTexto,
            ApelacionDocumentoUrl    = pen.ApelacionDocumentoUrl,
            ApelacionFecha           = pen.ApelacionFecha,
            Historial = pen.Historial
                .OrderBy(h => h.CambioDateTime)
                .Select(h => new PenalidadEstadoHistorialDto
                {
                    EstadoAnterior = h.EstadoAnterior,
                    EstadoNuevo    = h.EstadoNuevo,
                    CambioDateTime = h.CambioDateTime,
                })
                .ToList(),
        };
    }

    // ── Registro + tipificación ──────────────────────────────────────────────

    public async Task<PenalidadCreadaDto> RegistrarAsync(PenalidadRegistrarRequest req, int userId)
    {
        using var ctx = _factory.CreateDbContext();

        var esAbril = await ctx.Contributor.Where(c => c.ContributorId == req.EmpresaId).Select(c => c.EsAbril).FirstOrDefaultAsync();
        if (esAbril) throw new AbrilException("La empresa Abril Ingeniería no puede ser objeto de penalidad.", 422);

        var infraccion = await ctx.SsomaRacInfracciones.FirstOrDefaultAsync(i => i.Id == req.InfraccionId && i.Activo)
            ?? throw new AbrilException("Infracción no encontrada o inactiva.", 404);

        // El valor de UIT solo hace falta cuando la infracción se tarifica por factor UIT — la
        // mayoría de infracciones de SST de la tabla vigente tienen monto fijo en soles y no
        // deberían bloquearse por la falta de un dato que ni siquiera usan.
        decimal monto;
        decimal uitReferencia = 0m;
        if (infraccion.MontoFijo.HasValue)
        {
            monto = infraccion.MontoFijo.Value;
        }
        else
        {
            var uitVigente = await ctx.SsomaUitAnios
                .Where(u => u.Activo && u.Anio == DateTime.UtcNow.Year)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException($"Esta infracción se tarifica por UIT y no hay un valor de UIT configurado para el año {DateTime.UtcNow.Year}.", 422);
            uitReferencia = uitVigente.Valor;
            monto = Math.Round((infraccion.FactorUit ?? 0m) * uitVigente.Valor, 2);
        }
        if (monto <= 0m)
            throw new AbrilException("La infracción no tiene un monto fijo ni un factor UIT configurado.", 422);

        var project = await ctx.Project.FirstOrDefaultAsync(p => p.ProjectId == req.ProyectoId)
            ?? throw new AbrilException("Proyecto no encontrado.", 404);

        var abbrev  = project.Abbreviation ?? req.ProyectoId.ToString();
        var year    = DateTime.UtcNow.Year;
        var nuevoContador = project.ContadorPenalidad + 1;
        var codigo  = $"PEN-{year}-{abbrev}-{nuevoContador:D3}";

        var pen = new SsomaPenalidad
        {
            Codigo              = codigo,
            OrigenTipo          = req.OrigenTipo,
            OrigenId            = req.OrigenId,
            EmpresaId           = req.EmpresaId,
            ProyectoId          = req.ProyectoId,
            InfraccionId        = req.InfraccionId,
            Severidad           = req.Severidad,
            MontoCalculado      = monto,
            UitReferencia       = uitReferencia,
            DescripcionOcurrido = req.DescripcionOcurrido,
            Estado              = "Registrada",
            CreatedBy           = userId,
            CreatedAt           = DateTime.UtcNow,
        };

        ctx.SsomaPenalidades.Add(pen);
        project.ContadorPenalidad = nuevoContador;
        await ctx.SaveChangesAsync();

        pen.Estado    = "PendienteResidente";
        pen.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var detalle = await MapDetalleAsync(ctx, pen);
        _ = Task.Run(async () => { try { await _notif.NotificarPendienteResidenteAsync(detalle); } catch { } });

        return new PenalidadCreadaDto { Id = pen.Id, Codigo = pen.Codigo };
    }

    // ── Aprobación previa (Residente → Gerencia) ─────────────────────────────

    public async Task<PenalidadDetalleDto> AprobarResidenteAsync(int id, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var pen = await Cargar(ctx, id);
        Exigir(pen, "PendienteResidente");

        pen.AprobadoResidentePorId = userId;
        pen.AprobadoResidenteEn    = DateTime.UtcNow;
        pen.Estado                 = "PendienteGerenciaInmobiliaria";
        pen.UpdatedAt              = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var detalle = await MapDetalleAsync(ctx, pen);
        _ = Task.Run(async () => { try { await _notif.NotificarPendienteGerenciaAsync(detalle); } catch { } });
        return detalle;
    }

    public async Task<PenalidadDetalleDto> RechazarResidenteAsync(int id, PenalidadRechazarRequest req, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var pen = await Cargar(ctx, id);
        Exigir(pen, "PendienteResidente");

        pen.MotivoRechazoResidente = req.Motivo;
        pen.Estado                 = "Rechazada";
        pen.UpdatedAt              = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
        return await MapDetalleAsync(ctx, pen);
    }

    public async Task<PenalidadDetalleDto> AprobarGerenciaAsync(int id, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var pen = await Cargar(ctx, id);
        Exigir(pen, "PendienteGerenciaInmobiliaria");

        pen.AprobadoGerenciaPorId  = userId;
        pen.AprobadoGerenciaEn     = DateTime.UtcNow;
        pen.PlazoDescargoVenceEn  = DateTime.UtcNow.AddHours(pen.PlazoDescargoHoras);
        pen.Estado                 = "NotificadaEnDescargo";
        pen.UpdatedAt              = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var detalle = await MapDetalleAsync(ctx, pen);

        // PDF de notificación (best-effort, no bloquea la transición de estado).
        try
        {
            var textoNotificacion = TextoNotificacionDescargo();
            var pdfBytes = await PenalidadPdfService.GenerarNotificacionAsync(detalle, textoNotificacion);
            using var stream = new MemoryStream(pdfBytes);
            var path = await _spService.SubirPenalidadPdfAsync(stream, $"{pen.Codigo}-notificacion.pdf", pen.Id);
            pen.PdfNotificacionUrl = path;
            await ctx.SaveChangesAsync();
            detalle.PdfNotificacionUrl = path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error generando PDF de notificación de penalidad {Codigo}", pen.Codigo);
        }

        var contratistaEmail = await ctx.Contributor.Where(c => c.ContributorId == pen.EmpresaId).Select(c => c.EmailAdministrador).FirstOrDefaultAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(contratistaEmail))
                    await _notif.NotificarDescargoAlContratistaAsync(detalle, contratistaEmail);
            }
            catch { }
        });

        return detalle;
    }

    public async Task<PenalidadDetalleDto> RechazarGerenciaAsync(int id, PenalidadRechazarRequest req, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var pen = await Cargar(ctx, id);
        Exigir(pen, "PendienteGerenciaInmobiliaria");

        pen.MotivoRechazoGerencia = req.Motivo;
        pen.Estado                = "Rechazada";
        pen.UpdatedAt             = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
        return await MapDetalleAsync(ctx, pen);
    }

    // ── Descargo del contratista ─────────────────────────────────────────────

    public async Task<string> SubirDocumentoDescargoAsync(int id, IFormFile file)
    {
        using var ctx = _factory.CreateDbContext();
        var existe = await ctx.SsomaPenalidades.AnyAsync(p => p.Id == id);
        if (!existe) throw new AbrilException("Penalidad no encontrada.", 404);

        using var stream = file.OpenReadStream();
        return await _spService.SubirPenalidadDescargoAsync(stream, file.FileName, id);
    }

    public async Task PresentarDescargoAsync(int id, PenalidadDescargaRequest req, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var pen = await Cargar(ctx, id);
        Exigir(pen, "NotificadaEnDescargo");

        if (string.IsNullOrWhiteSpace(req.DocumentoUrl))
            throw new AbrilException("El documento de sustento es obligatorio.", 400);

        pen.DescargoTexto     = req.DescargoTexto;
        pen.DocumentoUrl      = req.DocumentoUrl;
        pen.DescargoFecha     = DateTime.UtcNow;
        pen.DescargoUsuarioId = userId;
        pen.Estado            = "DescargoPresentado";
        pen.UpdatedAt         = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        pen.Estado    = "EnEvaluacionSsoma";
        pen.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var detalle = await MapDetalleAsync(ctx, pen);
        _ = Task.Run(async () => { try { await _notif.NotificarEvaluacionPendienteAsync(detalle); } catch { } });
    }

    public async Task<PenalidadDetalleDto> EvaluarDescargoAsync(int id, PenalidadEvaluarDescargoRequest req, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var pen = await Cargar(ctx, id);
        Exigir(pen, "EnEvaluacionSsoma");

        if (req.Recomendacion is not ("Aprobar" or "Rechazar"))
            throw new AbrilException("Recomendacion debe ser 'Aprobar' o 'Rechazar'.", 400);

        pen.ArgumentoSsoma     = req.Argumento;
        pen.RecomendacionSsoma = req.Recomendacion;
        pen.EvaluadoPorSsomaId = userId;
        pen.EvaluadoSsomaEn    = DateTime.UtcNow;
        pen.Estado             = "PendienteDecisionGerencia";
        pen.UpdatedAt          = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var detalle = await MapDetalleAsync(ctx, pen);
        _ = Task.Run(async () => { try { await _notif.NotificarDecisionPendienteAsync(detalle); } catch { } });
        return detalle;
    }

    // ── Decisión final ───────────────────────────────────────────────────────

    public async Task<PenalidadDetalleDto> DecidirGerenciaAsync(int id, PenalidadDecidirGerenciaRequest req, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var pen = await Cargar(ctx, id);
        Exigir(pen, "PendienteDecisionGerencia");

        if (req.ResolucionTipo is not ("Aplicada" or "Anulada"))
            throw new AbrilException("ResolucionTipo debe ser 'Aplicada' o 'Anulada'.", 400);

        pen.ResolucionTipo         = req.ResolucionTipo;
        pen.ResolucionTexto        = req.ResolucionTexto;
        pen.MontoFinal             = req.MontoFinal ?? pen.MontoCalculado;
        pen.MontoAjustadoMotivo    = req.MotivoAjusteMonto;
        pen.MotivoObjecionGerencia = req.MotivoObjecionGerencia;
        pen.ResueltoPorId          = userId;
        pen.ResueltaEn             = DateTime.UtcNow;
        pen.Estado                 = req.ResolucionTipo;
        pen.UpdatedAt              = DateTime.UtcNow;
        // Solo "Aplicada" da derecho a apelar — "Anulada" ya favorece al contratista, no hay
        // nada que apelar.
        if (req.ResolucionTipo == "Aplicada")
            pen.PlazoApelacionVenceEn = DateTime.UtcNow.AddDays(pen.PlazoApelacionDias);
        await ctx.SaveChangesAsync();

        var detalle = await MapDetalleAsync(ctx, pen);

        try
        {
            var pdfBytes = await PenalidadPdfService.GenerarResolucionAsync(detalle);
            using var stream = new MemoryStream(pdfBytes);
            var path = await _spService.SubirPenalidadPdfAsync(stream, $"{pen.Codigo}-resolucion.pdf", pen.Id);
            pen.PdfResolucionUrl = path;
            await ctx.SaveChangesAsync();
            detalle.PdfResolucionUrl = path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error generando PDF de resolución de penalidad {Codigo}", pen.Codigo);
        }

        var contratistaEmail = await ctx.Contributor.Where(c => c.ContributorId == pen.EmpresaId).Select(c => c.EmailAdministrador).FirstOrDefaultAsync();
        _ = Task.Run(async () => { try { await _notif.NotificarDecisionFinalAsync(detalle, contratistaEmail); } catch { } });

        return detalle;
    }

    // ── Apelación — una sola vez, solo desde "Aplicada", exige evidencia nueva ──────────────

    public async Task<PenalidadDetalleDto> ApelarAsync(int id, PenalidadApelarRequest req, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var pen = await Cargar(ctx, id);
        Exigir(pen, "Aplicada");

        if (pen.ApelacionUsada)
            throw new AbrilException("Ya se presentó una apelación para esta penalidad; solo se admite una.", 400);
        if (pen.PlazoApelacionVenceEn.HasValue && DateTime.UtcNow > pen.PlazoApelacionVenceEn.Value)
            throw new AbrilException("Venció el plazo para apelar esta penalidad.", 400);
        if (string.IsNullOrWhiteSpace(req.DocumentoUrl))
            throw new AbrilException("La apelación exige adjuntar evidencia nueva (no basta con repetir el descargo original).", 400);

        pen.ApelacionTexto        = req.Texto;
        pen.ApelacionDocumentoUrl = req.DocumentoUrl;
        pen.ApelacionFecha        = DateTime.UtcNow;
        pen.ApelacionUsuarioId    = userId;
        pen.ApelacionUsada        = true;
        pen.Estado                = "EnApelacion";
        pen.UpdatedAt             = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var detalle = await MapDetalleAsync(ctx, pen);
        _ = Task.Run(async () => { try { await _notif.NotificarApelacionPresentadaAsync(detalle); } catch { } });
        return detalle;
    }

    public async Task<PenalidadDetalleDto> DecidirApelacionAsync(int id, PenalidadDecidirApelacionRequest req, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var pen = await Cargar(ctx, id);
        Exigir(pen, "EnApelacion");

        if (req.ResolucionTipo is not ("Aplicada" or "Anulada"))
            throw new AbrilException("ResolucionTipo debe ser 'Aplicada' o 'Anulada'.", 400);

        pen.ResolucionTipo         = req.ResolucionTipo;
        pen.ResolucionTexto        = req.ResolucionTexto;
        pen.MotivoObjecionGerencia = req.MotivoObjecionGerencia;
        pen.ResueltoPorId          = userId;
        pen.ResueltaEn             = DateTime.UtcNow;
        pen.Estado                 = req.ResolucionTipo;
        pen.UpdatedAt              = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var detalle = await MapDetalleAsync(ctx, pen);

        try
        {
            var pdfBytes = await PenalidadPdfService.GenerarResolucionAsync(detalle);
            using var stream = new MemoryStream(pdfBytes);
            var path = await _spService.SubirPenalidadPdfAsync(stream, $"{pen.Codigo}-resolucion-apelacion.pdf", pen.Id);
            pen.PdfResolucionUrl = path;
            await ctx.SaveChangesAsync();
            detalle.PdfResolucionUrl = path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error generando PDF de resolución de apelación {Codigo}", pen.Codigo);
        }

        var contratistaEmail = await ctx.Contributor.Where(c => c.ContributorId == pen.EmpresaId).Select(c => c.EmailAdministrador).FirstOrDefaultAsync();
        _ = Task.Run(async () => { try { await _notif.NotificarDecisionFinalAsync(detalle, contratistaEmail); } catch { } });

        return detalle;
    }

    public async Task<string> GetPdfNotificacionAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var url = await ctx.SsomaPenalidades.Where(p => p.Id == id).Select(p => p.PdfNotificacionUrl).FirstOrDefaultAsync()
            ?? throw new AbrilException("Penalidad no encontrada.", 404);
        if (string.IsNullOrWhiteSpace(url)) throw new AbrilException("No hay PDF de notificación disponible.", 404);
        return url;
    }

    public async Task<string> GetPdfResolucionAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var url = await ctx.SsomaPenalidades.Where(p => p.Id == id).Select(p => p.PdfResolucionUrl).FirstOrDefaultAsync()
            ?? throw new AbrilException("Penalidad no encontrada.", 404);
        if (string.IsNullOrWhiteSpace(url)) throw new AbrilException("No hay PDF de resolución disponible.", 404);
        return url;
    }

    // ── Recordatorios y vencimientos (cron) ──────────────────────────────────

    public async Task ProcesarRecordatoriosYVencimientosAsync()
    {
        using var ctx = _factory.CreateDbContext();
        var ahora = DateTime.UtcNow;

        // Recordatorio: penalidades en descargo cuyo plazo vence en menos de 12h y todavía no se avisó hoy.
        var porVencer = await ctx.SsomaPenalidades
            .Where(p => p.Estado == "NotificadaEnDescargo"
                     && p.PlazoDescargoVenceEn != null
                     && p.PlazoDescargoVenceEn > ahora
                     && p.PlazoDescargoVenceEn <= ahora.AddHours(12))
            .ToListAsync();

        foreach (var pen in porVencer)
        {
            var detalle = await MapDetalleAsync(ctx, pen);
            var contratistaEmail = await ctx.Contributor.Where(c => c.ContributorId == pen.EmpresaId).Select(c => c.EmailAdministrador).FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(contratistaEmail))
            {
                try { await _notif.RecordatorioDescargoAsync(detalle, contratistaEmail); }
                catch (Exception ex) { _logger.LogWarning(ex, "Error enviando recordatorio de penalidad {Codigo}", pen.Codigo); }
            }
        }

        // Vencidas sin respuesta: se dan por aceptadas y pasan a evaluación por incomparecencia.
        var vencidas = await ctx.SsomaPenalidades
            .Where(p => p.Estado == "NotificadaEnDescargo" && p.PlazoDescargoVenceEn != null && p.PlazoDescargoVenceEn <= ahora)
            .ToListAsync();

        foreach (var pen in vencidas)
        {
            pen.DescargoPorIncomparecencia = true;
            pen.Estado    = "EnEvaluacionSsoma";
            pen.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();

            var detalle = await MapDetalleAsync(ctx, pen);
            try { await _notif.NotificarEvaluacionPendienteAsync(detalle); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error notificando vencimiento de penalidad {Codigo}", pen.Codigo); }
        }
    }

    // ── Catálogos ────────────────────────────────────────────────────────────

    public async Task<List<InfraccionAdminDto>> GetInfraccionesAsync(bool soloActivas)
    {
        using var ctx = _factory.CreateDbContext();
        var query = ctx.SsomaRacInfracciones.AsQueryable();
        if (soloActivas) query = query.Where(i => i.Activo);
        return await query.OrderBy(i => i.Nombre).Select(i => new InfraccionAdminDto
        {
            Id = i.Id, Nombre = i.Nombre, FactorUit = i.FactorUit, MontoFijo = i.MontoFijo,
            Descripcion = i.Descripcion, Activo = i.Activo,
        }).ToListAsync();
    }

    public async Task<InfraccionAdminDto> CrearInfraccionAsync(InfraccionUpsertRequest req)
    {
        using var ctx = _factory.CreateDbContext();
        ValidarInfraccion(req);
        var entidad = new SsomaRacInfraccion
        {
            Nombre = req.Nombre, FactorUit = req.FactorUit, MontoFijo = req.MontoFijo,
            Descripcion = req.Descripcion, Activo = req.Activo,
        };
        ctx.SsomaRacInfracciones.Add(entidad);
        await ctx.SaveChangesAsync();
        return new InfraccionAdminDto { Id = entidad.Id, Nombre = entidad.Nombre, FactorUit = entidad.FactorUit, MontoFijo = entidad.MontoFijo, Descripcion = entidad.Descripcion, Activo = entidad.Activo };
    }

    public async Task<InfraccionAdminDto> ActualizarInfraccionAsync(int id, InfraccionUpsertRequest req)
    {
        using var ctx = _factory.CreateDbContext();
        ValidarInfraccion(req);
        var entidad = await ctx.SsomaRacInfracciones.FindAsync(id) ?? throw new AbrilException("Infracción no encontrada.", 404);
        entidad.Nombre = req.Nombre; entidad.FactorUit = req.FactorUit; entidad.MontoFijo = req.MontoFijo;
        entidad.Descripcion = req.Descripcion; entidad.Activo = req.Activo;
        await ctx.SaveChangesAsync();
        return new InfraccionAdminDto { Id = entidad.Id, Nombre = entidad.Nombre, FactorUit = entidad.FactorUit, MontoFijo = entidad.MontoFijo, Descripcion = entidad.Descripcion, Activo = entidad.Activo };
    }

    private static void ValidarInfraccion(InfraccionUpsertRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new AbrilException("El nombre es obligatorio.", 400);
        if (req.MontoFijo is null && req.FactorUit is null) throw new AbrilException("Debe indicar un monto fijo o un factor UIT.", 400);
    }

    public async Task<List<UitAnioAdminDto>> GetUitAniosAsync()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsomaUitAnios.OrderByDescending(u => u.Anio)
            .Select(u => new UitAnioAdminDto { Id = u.Id, Anio = u.Anio, Valor = u.Valor, Activo = u.Activo })
            .ToListAsync();
    }

    public async Task<UitAnioAdminDto> CrearUitAnioAsync(UitAnioUpsertRequest req)
    {
        using var ctx = _factory.CreateDbContext();
        if (req.Valor <= 0) throw new AbrilException("El valor de la UIT debe ser mayor a 0.", 400);
        if (await ctx.SsomaUitAnios.AnyAsync(u => u.Anio == req.Anio))
            throw new AbrilException($"Ya existe un valor de UIT para el año {req.Anio}.", 409);

        var entidad = new SsomaUitAnio { Anio = req.Anio, Valor = req.Valor, Activo = req.Activo };
        ctx.SsomaUitAnios.Add(entidad);
        await ctx.SaveChangesAsync();
        return new UitAnioAdminDto { Id = entidad.Id, Anio = entidad.Anio, Valor = entidad.Valor, Activo = entidad.Activo };
    }

    public async Task<UitAnioAdminDto> ActualizarUitAnioAsync(int id, UitAnioUpsertRequest req)
    {
        using var ctx = _factory.CreateDbContext();
        if (req.Valor <= 0) throw new AbrilException("El valor de la UIT debe ser mayor a 0.", 400);
        var entidad = await ctx.SsomaUitAnios.FindAsync(id) ?? throw new AbrilException("Registro de UIT no encontrado.", 404);
        entidad.Anio = req.Anio; entidad.Valor = req.Valor; entidad.Activo = req.Activo;
        await ctx.SaveChangesAsync();
        return new UitAnioAdminDto { Id = entidad.Id, Anio = entidad.Anio, Valor = entidad.Valor, Activo = entidad.Activo };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<SsomaPenalidad> Cargar(AppDbContext ctx, int id) =>
        await ctx.SsomaPenalidades.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new AbrilException("Penalidad no encontrada.", 404);

    private static void Exigir(SsomaPenalidad pen, string estadoRequerido)
    {
        if (pen.Estado != estadoRequerido)
            throw new AbrilException($"La penalidad debe estar en estado '{estadoRequerido}' (actual: '{pen.Estado}').", 400);
    }

    private static string TextoNotificacionDescargo() => """
        En el marco de nuestro compromiso con la seguridad e integridad de los trabajadores y el
        cumplimiento de la normativa legal vigente, le informamos que se ha identificado una
        posible infracción asociada a su empresa. Nuestro objetivo no es sancionar sino asegurar
        que se mantengan las condiciones de trabajo seguras y el cumplimiento contractual y legal
        aplicable. Se le otorgan 48 horas desde la recepción de esta notificación para presentar
        su descargo y/o sustento documentario. De no recibir respuesta en dicho plazo, se dará por
        aceptada la observación conforme al procedimiento vigente.
        """;
}
