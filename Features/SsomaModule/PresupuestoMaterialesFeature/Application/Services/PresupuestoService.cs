using System.Diagnostics;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.CostsModule.Features.Configuration.CostosPresupuestosEmailFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;

public class PresupuestoService : IPresupuestoService
{
    private readonly IPresupuestoRepository _repo;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IEmailService _emailService;
    private readonly ILogger<PresupuestoService> _logger;
    private readonly ICatalogoMaterialesService _catalogoService;
    private readonly ICostosPresupuestosEmailService _costosPresupuestosEmailService;
    private readonly IPresupuestoResumenExportService _resumenExportService;

    public PresupuestoService(
        IPresupuestoRepository repo, IDbContextFactory<AppDbContext> factory,
        IEmailService emailService, ILogger<PresupuestoService> logger,
        ICatalogoMaterialesService catalogoService,
        ICostosPresupuestosEmailService costosPresupuestosEmailService,
        IPresupuestoResumenExportService resumenExportService)
    {
        _repo            = repo;
        _factory         = factory;
        _emailService    = emailService;
        _logger          = logger;
        _catalogoService = catalogoService;
        _costosPresupuestosEmailService = costosPresupuestosEmailService;
        _resumenExportService = resumenExportService;
    }

    public async Task<PresupuestoDetalleDto> GenerarAsync(int projectId, GenerarPresupuestoDto dto, int? userId)
    {
        // Instrumentación temporal: "Generar presupuesto" está tardando ~30s y no hay forma de saber
        // cuál de los pasos es el lento sin medir cada uno por separado.
        var sw = Stopwatch.StartNew();
        void Lap(string paso) { _logger.LogWarning("[GenerarAsync] {Paso}: {Ms}ms", paso, sw.ElapsedMilliseconds); sw.Restart(); }

        using var ctx = _factory.CreateDbContext();
        var proyecto = await ctx.Project.FindAsync(projectId)
            ?? throw new AbrilException("Proyecto no encontrado.", 404);
        Lap("CreateDbContext + Project.FindAsync");

        // Drivers: usar override si viene en request, si no los del proyecto
        var hh    = dto.HhTotalCasa   ?? proyecto.HhTotalCasa   ?? 0;
        var area  = dto.AreaTechadaM2 ?? proyecto.AreaTechadaM2 ?? 0;
        var trab  = dto.Trabajadores  ?? ParseTrab(proyecto.CantTrabajadoresCasa);

        if (hh == 0 && area == 0)
            throw new AbrilException(
                "El proyecto no tiene HH ni Área Techada configurados. Actualice los drivers primero.", 400);

        // Ratios recomendados de todos los proyectos históricos
        var ratios = await _repo.ObtenerRatiosRecomendadosAsync();
        Lap("ObtenerRatiosRecomendadosAsync");

        // Calcular líneas de presupuesto
        var lineas = ratios.Select(r =>
        {
            var driver        = ObtenerDriver(r.VariableBase, hh, area, (decimal)trab);
            var cantidadCruda = driver > 0 ? r.RatioRecomendado * driver : 0;
            // La inmensa mayoría de los materiales SSOMA se compran en unidades enteras (pares de
            // guantes, cascos, kits, extintores...) — no tiene sentido pedir "1.13 kits". Solo las
            // pocas famílias medidas en una unidad continua (metros, litros, m², kg) se dejan con
            // decimales; el resto se redondea hacia arriba para no quedar corto en obra.
            var cantidad = EsUnidadContinua(r.UnidadMedida)
                ? Math.Round(cantidadCruda, 4)
                : Math.Ceiling(cantidadCruda);
            var total    = Math.Round(cantidad * r.PrecioRecomendado, 2);

            return new PresupuestoLineaDto
            {
                FamiliaId        = r.FamiliaId,
                NombreFamilia    = r.NombreFamilia,
                TipoId           = r.TipoId,
                NombreTipo       = r.NombreTipo,
                VariableBase     = r.VariableBase,
                RatioRecomendado = r.RatioRecomendado,
                NProyectosBase   = (int)r.NProyectos,
                ValorDriver      = driver,
                CantidadEstimada = cantidad,
                PrecioUnitario   = r.PrecioRecomendado,
                TotalEstimado    = total,
                TieneHistoria    = r.NProyectos > 0
            };
        }).ToList();

        var version         = await _repo.SiguienteVersionAsync(projectId);
        Lap("SiguienteVersionAsync");
        var presupuestoAnteriorId = await _repo.ObtenerUltimoPresupuestoIdAsync(projectId);
        Lap("ObtenerUltimoPresupuestoIdAsync");

        // Total real se calcula recién después de insertar las líneas, con la misma fórmula que
        // usan Personal/Vigilancia/Servicios/Kits (PresupuestoTotalHelper) — evita mantener la
        // suma de materiales duplicada en dos sitios.
        var presupuestoId = await _repo.CrearPresupuestoAsync(
            projectId, version, hh, area, trab, 0, userId, dto.Notas);
        Lap("CrearPresupuestoAsync");

        await _repo.InsertarLineasAsync(presupuestoId, lineas);
        Lap("InsertarLineasAsync");

        // Personal/Vigilancia/Servicios/Kits no se recalculan con los ratios (son 100% manuales), así
        // que se arrastran de la versión anterior — sin esto, cada nueva versión los perdía por
        // completo y el responsable SSOMA tenía que volver a cargarlos desde cero.
        if (presupuestoAnteriorId.HasValue)
            await _repo.CopiarDatosDeVersionAnteriorAsync(presupuestoAnteriorId.Value, presupuestoId);
        Lap("CopiarDatosDeVersionAnteriorAsync");

        await _repo.RecalcularTotalAsync(presupuestoId);
        Lap("RecalcularTotalAsync");

        var resultado = (await _repo.ObtenerDetalleAsync(presupuestoId))!;
        Lap("ObtenerDetalleAsync");
        return resultado;
    }

    public Task<PresupuestoDetalleDto?> ObtenerDetalleAsync(int presupuestoId) =>
        _repo.ObtenerDetalleAsync(presupuestoId);

    public Task<List<PresupuestoResumenDto>> ObtenerPorProyectoAsync(int projectId) =>
        _repo.ObtenerPorProyectoAsync(projectId);

    public async Task<PresupuestoDetalleDto> ActualizarLineaAsync(
        int presupuestoId, int lineaId, ActualizarLineaPresupuestoDto dto)
    {
        await _repo.ActualizarLineaAsync(lineaId, dto.CantidadManual, dto.PrecioManual, dto.NotasLinea);
        return (await _repo.ObtenerDetalleAsync(presupuestoId))!;
    }

    public async Task EliminarAsync(int presupuestoId)
    {
        var estado = await _repo.ObtenerEstadoAsync(presupuestoId)
            ?? throw new AbrilException("Presupuesto no encontrado.", 404);
        if (estado != "BORRADOR")
            throw new AbrilException(
                "Solo se puede eliminar un presupuesto en estado BORRADOR — uno aprobado puede tener Control de consumo semanal real registrado encima.", 400);
        await _repo.EliminarAsync(presupuestoId);
    }

    public async Task<string> AprobarAsync(int presupuestoId)
    {
        var estado = await _repo.AprobarAsync(presupuestoId);
        await EnviarNotificacionAprobacionAsync(presupuestoId);
        return estado;
    }

    /// <summary>Reenvía el correo de aprobación de un presupuesto YA aprobado (ej. se corrigió algo
    /// en el Resumen después de aprobar, o el correo original no llegó) — a diferencia del envío
    /// automático al aprobar, este SÍ propaga el error si algo falla, para que quien lo pide sepa
    /// que no se mandó.</summary>
    public async Task ReenviarNotificacionAprobacionAsync(int presupuestoId)
    {
        var estado = await _repo.ObtenerEstadoAsync(presupuestoId)
            ?? throw new AbrilException("Presupuesto no encontrado.", 404);
        if (estado != "APROBADO")
            throw new AbrilException("Solo se puede reenviar la notificación de un presupuesto ya aprobado.", 400);

        await EnviarNotificacionAprobacionAsync(presupuestoId, propagarError: true);
    }

    /// <summary>Mismo resolver que usa el envío real (ver <see cref="EnviarNotificacionAprobacionAsync"/>)
    /// — expuesto aparte para que el frontend pueda mostrar "a quiénes se les va a avisar" ANTES de
    /// aprobar, sin duplicar la lógica (así la vista previa nunca puede mentir sobre el envío real).</summary>
    public async Task<List<PresupuestoDestinatarioDto>> ObtenerDestinatariosAprobacionAsync(int presupuestoId)
    {
        var detalle = await _repo.ObtenerDetalleAsync(presupuestoId);
        if (detalle == null) return [];

        using var ctx = _factory.CreateDbContext();
        var proyecto = await ctx.Project.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == detalle.ProjectId);
        if (proyecto == null) return [];

        var lista = new List<PresupuestoDestinatarioDto>();
        void Agregar(string rol, string? email)
        {
            if (!string.IsNullOrWhiteSpace(email) && email.Trim().EndsWith("@abril.pe", StringComparison.OrdinalIgnoreCase))
                lista.Add(new PresupuestoDestinatarioDto { Rol = rol, Email = email.Trim() });
        }

        if (proyecto.ResidenteWorkersId.HasValue)
        {
            var residenteEmail = await ctx.Worker.AsNoTracking()
                .Where(w => w.Id == proyecto.ResidenteWorkersId.Value)
                .Select(w => w.EmailCorporativo)
                .FirstOrDefaultAsync();
            Agregar("Residente", residenteEmail);
        }

        Agregar("Coordinador SSOMA", proyecto.EmailCoordSsoma);

        var jefeSsomaEmail = await ctx.Worker.AsNoTracking()
            .Where(w => w.PuestoId == PuestoIds.JefeSsoma && w.WorkersEstadoId == WorkersEstadoIds.Activo)
            .Select(w => w.EmailCorporativo)
            .FirstOrDefaultAsync();
        Agregar("Jefe SSOMA", jefeSsomaEmail);

        // Oficina Técnica DEL PROYECTO — solo el rol de Oficina Técnica en sí (Ingeniero/Asistente),
        // no cualquier "Staff" destacado en obra (eso incluía Administrador de Obra, Producción,
        // Calidad, Prevencionista... nada que ver con Oficina Técnica).
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var oficinaTecnicaEmails = await ctx.Worker.AsNoTracking()
            .Where(w => w.WorkersEstadoId == WorkersEstadoIds.Activo
                     && w.PuestoCatalogo != null && w.PuestoCatalogo.Nombre.ToUpper().Contains("OFICINA TECNICA")
                     && ctx.WorkerVinculacion.Any(v => v.WorkerId == w.Id && v.ProyectoId == detalle.ProjectId
                            && (v.FechaFin == null || v.FechaFin >= hoy)))
            .Select(w => w.EmailCorporativo)
            .ToListAsync();
        foreach (var e in oficinaTecnicaEmails) Agregar("Oficina Técnica", e);

        // Costos y Presupuestos de oficina central — lista curada a mano (mismo mecanismo que ya
        // usan Adjudicaciones/Contratistas), no un query por puesto/área que podía traer gente que
        // no correspondía.
        var costosEmails = await _costosPresupuestosEmailService.GetActiveEmails();
        foreach (var e in costosEmails) Agregar("Costos y Presupuestos", e);

        return lista;
    }

    /// <summary>Notifica al aprobar el presupuesto usando <see cref="ObtenerDestinatariosAprobacionAsync"/>
    /// (Residente, Coordinador SSOMA del proyecto, Jefe SSOMA, Oficina Técnica del proyecto, Costos y
    /// Presupuestos de oficina central) — adjunta el Excel del Resumen y detalla las partidas en el
    /// cuerpo del correo. Nunca revienta la aprobación: si algo falla acá, solo queda logueado — la
    /// aprobación en sí ya se guardó.</summary>
    private async Task EnviarNotificacionAprobacionAsync(int presupuestoId, bool propagarError = false)
    {
        try
        {
            var detalle = await _repo.ObtenerDetalleAsync(presupuestoId);
            if (detalle == null) return;

            var destinatarios = await ObtenerDestinatariosAprobacionAsync(presupuestoId);
            var to = destinatarios.Select(d => d.Email).Distinct().ToList();
            if (to.Count == 0) return;

            var resumen = await _resumenExportService.ObtenerResumenAgregadoAsync(detalle.ProjectId);
            var filasHtml = resumen is null ? "" : string.Join("", resumen.Lineas.Select(l =>
                $"<tr><td style='padding:4px 12px;border-bottom:1px solid #eee'>{l.Descripcion}</td>" +
                $"<td style='padding:4px 12px;border-bottom:1px solid #eee;text-align:right'>S/ {l.CostoDirecto:N2}</td></tr>"));

            var html = $@"<h2>Presupuesto de Materiales SSOMA aprobado</h2>
<p>Se aprobó el presupuesto del proyecto <strong>{detalle.ProjectDescription}</strong>:</p>
<table style='border-collapse:collapse;font-family:Arial,sans-serif;font-size:14px;'>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Proyecto</td><td style='padding:6px 12px'>{detalle.ProjectDescription}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Versión</td><td style='padding:6px 12px'>v{detalle.Version}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Total estimado</td><td style='padding:6px 12px'>S/ {detalle.TotalEstimado:N2}</td></tr>
</table>
{(filasHtml.Length == 0 ? "" : $@"<h3 style='margin-top:20px;font-family:Arial,sans-serif'>Detalle por partida</h3>
<table style='border-collapse:collapse;font-family:Arial,sans-serif;font-size:13px;width:100%;max-width:560px'>
{filasHtml}
</table>")}
<p style='font-size:12px;color:#666;margin-top:24px;'>Se adjunta el Excel completo con el detalle. Esta notificación se generó automáticamente por el sistema Abril.</p>";

            List<EmailAttachment>? attachments = null;
            var excelBytes = await _resumenExportService.ExportarExcelAsync(detalle.ProjectId);
            if (excelBytes is not null)
            {
                attachments =
                [
                    new EmailAttachment
                    {
                        FileName = $"Resumen_Presupuesto_SSOMA_{detalle.ProjectDescription}_v{detalle.Version}.xlsx",
                        ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        Content = excelBytes,
                    }
                ];
            }

            await _emailService.SendAsync(
                to: to,
                subject: $"[Presupuesto de Materiales Aprobado] {detalle.ProjectDescription} — v{detalle.Version}",
                body: html,
                isHtml: true,
                attachments: attachments);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar la notificación de aprobación del presupuesto {PresupuestoId}.", presupuestoId);
            if (propagarError)
                throw new AbrilException("No se pudo enviar el correo. Revisa los destinatarios e intenta de nuevo.", 500);
        }
    }

    public Task ActualizarCantidadManualPorFamiliaAsync(int projectId, int familiaId, decimal? cantidadManual) =>
        _repo.ActualizarCantidadManualPorFamiliaAsync(projectId, familiaId, cantidadManual);

    /// <summary>Crea (o reutiliza, si ya existe por nombre) la família en el catálogo maestro y la
    /// agrega de una vez como línea manual del presupuesto — para no obligar al responsable SSOMA a
    /// pasar primero por Catálogo cuando el material simplemente no existía todavía.</summary>
    public async Task<PresupuestoDetalleDto> AgregarFamiliaManualAsync(int presupuestoId, AgregarFamiliaManualDto dto)
    {
        var familia = await _catalogoService.CrearFamiliaAsync(new CrearFamiliaCatalogoDto
        {
            Nombre       = dto.Nombre,
            TipoId       = dto.TipoId,
            VariableBase = dto.VariableBase,
            UnidadMedida = dto.UnidadMedida,
        });

        await _repo.InsertarLineaManualAsync(presupuestoId, familia.Id, dto.CantidadManual, dto.PrecioManual, dto.NotasLinea);
        return (await _repo.ObtenerDetalleAsync(presupuestoId))!;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static decimal ObtenerDriver(string variableBase, decimal hh, decimal area, decimal trab) =>
        variableBase switch
        {
            "HH"           => hh,
            "AREATECHADA"  => area,
            "TRABAJADORES" => trab,
            _              => 1   // CALCULADO / FIJO / METRADO → ratio = cantidad absoluta
        };

    // Unidades de medida que sí se compran/miden en fracciones (metros, área, volumen, peso) — todo
    // lo demás (UND, PAR, KIT, JGO, y cualquier unidad_medida vacía/desconocida) se trata como
    // unidad entera. Ajustar esta lista si aparece algún caso nuevo real en Catálogo.
    private static readonly HashSet<string> UnidadesContinuas =
        new(StringComparer.OrdinalIgnoreCase) { "ML", "M", "M2", "M3", "L", "GL", "GAL", "KG" };

    private static bool EsUnidadContinua(string? unidadMedida) =>
        !string.IsNullOrWhiteSpace(unidadMedida) && UnidadesContinuas.Contains(unidadMedida.Trim());

    private static int ParseTrab(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var clean = new string(s.Where(char.IsDigit).ToArray());
        return int.TryParse(clean, out var v) ? v : 0;
    }
}
