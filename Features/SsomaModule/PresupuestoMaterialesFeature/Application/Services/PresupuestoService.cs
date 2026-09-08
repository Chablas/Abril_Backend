using System.Diagnostics;
using Abril_Backend.Application.Exceptions;
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

    public PresupuestoService(
        IPresupuestoRepository repo, IDbContextFactory<AppDbContext> factory,
        IEmailService emailService, ILogger<PresupuestoService> logger)
    {
        _repo         = repo;
        _factory      = factory;
        _emailService = emailService;
        _logger       = logger;
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

        var gerenteInmobiliarioEmail = await ctx.Worker.AsNoTracking()
            .Where(w => w.PuestoCatalogo != null && w.PuestoCatalogo.Nombre.ToUpper() == "GERENTE INMOBILIARIO"
                     && w.WorkersEstadoId == WorkersEstadoIds.Activo)
            .Select(w => w.EmailCorporativo)
            .FirstOrDefaultAsync();
        Agregar("Gerente Inmobiliario", gerenteInmobiliarioEmail);

        var costosEmails = await ctx.Worker.AsNoTracking()
            .Where(w => w.PuestoCatalogo != null && w.PuestoCatalogo.AreaDestinoScopeId == AreaScopeIds.CostosYPresupuestos
                     && w.WorkersEstadoId == WorkersEstadoIds.Activo)
            .Select(w => w.EmailCorporativo)
            .ToListAsync();
        foreach (var e in costosEmails) Agregar("Costos y Presupuestos", e);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var oficinaTecnicaEmails = await ctx.Worker.AsNoTracking()
            .Where(w => w.ObraOficinaStaffId == ObraOficinaStaffIds.Staff
                     && w.WorkersEstadoId == WorkersEstadoIds.Activo
                     && ctx.WorkerVinculacion.Any(v => v.WorkerId == w.Id && v.ProyectoId == detalle.ProjectId
                            && (v.FechaFin == null || v.FechaFin >= hoy)))
            .Select(w => w.EmailCorporativo)
            .ToListAsync();
        foreach (var e in oficinaTecnicaEmails) Agregar("Oficina Técnica", e);

        return lista;
    }

    /// <summary>Notifica al aprobar el presupuesto usando <see cref="ObtenerDestinatariosAprobacionAsync"/>
    /// (Residente, Coordinador SSOMA, Jefe SSOMA, Gerente Inmobiliario, Costos y Presupuestos, Oficina
    /// Técnica del proyecto). Nunca revienta la aprobación: si algo falla acá, solo queda logueado — la
    /// aprobación en sí ya se guardó.</summary>
    private async Task EnviarNotificacionAprobacionAsync(int presupuestoId)
    {
        try
        {
            var detalle = await _repo.ObtenerDetalleAsync(presupuestoId);
            if (detalle == null) return;

            var destinatarios = await ObtenerDestinatariosAprobacionAsync(presupuestoId);
            var to = destinatarios.Select(d => d.Email).Distinct().ToList();
            if (to.Count == 0) return;

            var html = $@"<h2>Presupuesto de Materiales SSOMA aprobado</h2>
<p>Se aprobó el presupuesto del proyecto <strong>{detalle.ProjectDescription}</strong>:</p>
<table style='border-collapse:collapse;font-family:Arial,sans-serif;font-size:14px;'>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Proyecto</td><td style='padding:6px 12px'>{detalle.ProjectDescription}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Versión</td><td style='padding:6px 12px'>v{detalle.Version}</td></tr>
<tr><td style='padding:6px 12px;font-weight:600;background:#f9fafb'>Total estimado</td><td style='padding:6px 12px'>S/ {detalle.TotalEstimado:N2}</td></tr>
</table>
<p style='font-size:12px;color:#666;margin-top:24px;'>Esta notificación se generó automáticamente por el sistema Abril.</p>";

            await _emailService.SendAsync(
                to: to,
                subject: $"[Presupuesto de Materiales Aprobado] {detalle.ProjectDescription} — v{detalle.Version}",
                body: html,
                isHtml: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar la notificación de aprobación del presupuesto {PresupuestoId}.", presupuestoId);
        }
    }

    public Task ActualizarCantidadManualPorFamiliaAsync(int projectId, int familiaId, decimal? cantidadManual) =>
        _repo.ActualizarCantidadManualPorFamiliaAsync(projectId, familiaId, cantidadManual);

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
