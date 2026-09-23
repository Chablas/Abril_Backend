using System.Globalization;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.CharlasFeature.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.Rac.Entities;
using Abril_Backend.Features.SsomaModule.HojaRutaContratistaFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.HojaRutaContratistaFeature.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.HojaRutaContratistaFeature.Application.Services;

/// <summary>
/// Resumen semanal de cumplimiento de un contratista para efectos de valorización — agrega
/// lectura sobre módulos ya existentes (ss_hab_trabajador, ss_hab_empresa, ss_hab_equipo,
/// Dossier, RAC, Entregables de accidente). No crea ni escribe ningún dato nuevo: es de solo
/// lectura, la misma filosofía que VigenciaRevisionService/RetiroAutomaticoService pero sin
/// efectos secundarios.
/// </summary>
public class HojaRutaService : IHojaRutaService
{
    private static readonly string[] TiposDocDossierRequeridos = ["EPP", "ATS", "PETAR"];
    private static readonly string[] EstadosDocumentoCumple = ["Aprobado", "NA"];

    private readonly IDbContextFactory<AppDbContext> _factory;

    public HojaRutaService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<ContratistaActivoDto>> GetContratistasActivosAsync(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();

        var empresaIds = await ctx.WorkerVinculacion.AsNoTracking()
            .Where(v => v.ProyectoId == proyectoId && v.EmpresaId != null && v.FechaFin == null)
            .Select(v => v.EmpresaId!.Value)
            .Distinct()
            .ToListAsync();

        if (empresaIds.Count == 0) return [];

        return await ctx.Contributor.AsNoTracking()
            .Where(c => empresaIds.Contains(c.ContributorId) && !c.EsAbril)
            .OrderBy(c => c.ContributorName)
            .Select(c => new ContratistaActivoDto(c.ContributorId, c.ContributorName))
            .ToListAsync();
    }

    public async Task<HojaRutaResumenDto> GetResumenAsync(int contributorId, int proyectoId, int anio, int numeroSemana)
    {
        var fechaInicio = DateOnly.FromDateTime(ISOWeek.ToDateTime(anio, numeroSemana, DayOfWeek.Monday));
        var fechaFin = fechaInicio.AddDays(6);

        using var ctx = _factory.CreateDbContext();

        var empresa = await ctx.Contributor.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ContributorId == contributorId);

        var workerIds = await ctx.WorkerVinculacion.AsNoTracking()
            .Where(v => v.EmpresaId == contributorId && v.ProyectoId == proyectoId
                     && v.FechaInicio <= fechaFin
                     && (v.FechaFin == null || v.FechaFin >= fechaInicio))
            .Select(v => v.WorkerId)
            .Distinct()
            .ToListAsync();

        var items = new List<HojaRutaItemDto>
        {
            await GetItemTrabajadorAsync(ctx, workerIds, HabItemIds.InduccionObra, "induccion", "Inducción de obra"),
            await GetItemTrabajadorAsync(ctx, workerIds, HabItemIds.Sctr, "sctr", "SCTR activo"),
            await GetItemTrabajadorAsync(ctx, workerIds, HabItemIds.VidaLey, "vidaley", "Vida ley"),
            await GetItemTrabajadorAsync(ctx, workerIds, HabItemIds.CertAptitud, "emo", "EMO aprobados"),
            await GetItemTrabajadorAsync(ctx, workerIds, HabItemIds.CarnetRetcc, "retcc", "Carnet RETCC"),
            await GetItemHojaAtencionSctrAsync(ctx, contributorId, proyectoId),
            await GetItemCharlasAsync(ctx, contributorId, proyectoId, fechaInicio, fechaFin),
        };

        items.AddRange(await GetItemsDossierAsync(ctx, contributorId, proyectoId, anio, numeroSemana));
        items.Add(await GetItemInformeAccidenteAsync(ctx, contributorId, fechaInicio, fechaFin));
        items.Add(await GetItemObservacionesRacAsync(ctx, contributorId, proyectoId, fechaInicio, fechaFin));
        items.Add(await GetItemCertificadoOperatividadAsync(ctx, contributorId));
        items.Add(new HojaRutaItemDto(
            "residuos_solidos", "Certificados de disposición de residuos sólidos",
            HojaRutaEstado.Manual, "Aún no automatizado — se verifica manualmente."));

        return new HojaRutaResumenDto(
            contributorId,
            empresa?.ContributorName ?? $"Empresa #{contributorId}",
            proyectoId, anio, numeroSemana, fechaInicio, fechaFin,
            workerIds.Count, items);
    }

    /// <summary>Ítems por trabajador (ss_hab_trabajador × ss_item_trabajador): cumple solo si
    /// TODOS los trabajadores activos de esa empresa+proyecto+semana tienen Estado="Aprobado"
    /// para ese ítem. El control de acceso ya bloquea el ingreso sin esto, así que un trabajador
    /// activo (con vinculación vigente) sin el ítem aprobado es una inconsistencia a reportar,
    /// no una espera esperada.</summary>
    private static async Task<HojaRutaItemDto> GetItemTrabajadorAsync(
        AppDbContext ctx, List<int> workerIds, int itemId, string codigo, string nombre)
    {
        if (workerIds.Count == 0)
            return new HojaRutaItemDto(codigo, nombre, HojaRutaEstado.NoAplica, "Sin trabajadores activos esta semana.");

        var aprobados = await ctx.SsHabTrabajador.AsNoTracking()
            .CountAsync(h => workerIds.Contains(h.WorkerId) && h.ItemId == itemId && h.Estado == "Aprobado");

        var estado = aprobados == workerIds.Count ? HojaRutaEstado.Cumple : HojaRutaEstado.Pendiente;
        var detalle = $"{aprobados}/{workerIds.Count} trabajadores con el ítem aprobado.";
        return new HojaRutaItemDto(codigo, nombre, estado, detalle, workerIds.Count, aprobados);
    }

    private static async Task<HojaRutaItemDto> GetItemHojaAtencionSctrAsync(
        AppDbContext ctx, int contributorId, int proyectoId)
    {
        var hab = await ctx.SsHabEmpresa.AsNoTracking()
            .Where(h => h.EmpresaId == contributorId && h.ProyectoId == proyectoId
                     && h.ItemId == HabItemEmpresaIds.HojaAtencionSctr)
            .OrderByDescending(h => h.Id)
            .FirstOrDefaultAsync();

        if (hab == null)
            return new HojaRutaItemDto("hoja_atencion_sctr", "Hoja de Atención SCTR", HojaRutaEstado.Pendiente,
                "No se encontró el ítem en Habilitación > Empresa.");

        var estado = hab.Estado == "Aprobado" ? HojaRutaEstado.Cumple : HojaRutaEstado.Pendiente;
        return new HojaRutaItemDto("hoja_atencion_sctr", "Hoja de Atención SCTR", estado, $"Estado: {hab.Estado}.");
    }

    private static async Task<HojaRutaItemDto> GetItemCharlasAsync(
        AppDbContext ctx, int contributorId, int proyectoId, DateOnly fechaInicio, DateOnly fechaFin)
    {
        var charlas = await ctx.SsCharlaContratista.AsNoTracking()
            .Where(c => c.EmpresaId == contributorId && c.ProyectoId == proyectoId
                     && c.Fecha >= fechaInicio && c.Fecha <= fechaFin && c.State)
            .ToListAsync();

        if (charlas.Count == 0)
            return new HojaRutaItemDto("charlas", "Charlas de seguridad", HojaRutaEstado.Pendiente,
                "No se subió ninguna charla esta semana.");

        var aprobadas = charlas.Count(c => c.Estado != "Rechazado");
        var estado = aprobadas == charlas.Count ? HojaRutaEstado.Cumple : HojaRutaEstado.Pendiente;
        return new HojaRutaItemDto("charlas", "Charlas de seguridad", estado,
            $"{charlas.Count} charla(s) subida(s), {charlas.Count - aprobadas} rechazada(s).");
    }

    private static async Task<List<HojaRutaItemDto>> GetItemsDossierAsync(
        AppDbContext ctx, int contributorId, int proyectoId, int anio, int numeroSemana)
    {
        var semana = await ctx.SsDossierSemana.AsNoTracking()
            .Include(s => s.Documentos)
            .FirstOrDefaultAsync(s => s.ContributorId == contributorId && s.ProyectoId == proyectoId
                                   && s.Anio == anio && s.NumeroSemana == numeroSemana);

        var resultado = new List<HojaRutaItemDto>();
        foreach (var tipo in TiposDocDossierRequeridos)
        {
            var codigo = tipo.ToLowerInvariant();
            var doc = semana?.Documentos.FirstOrDefault(d => d.TipoDoc == tipo);

            if (semana == null || doc == null)
            {
                resultado.Add(new HojaRutaItemDto(codigo, tipo, HojaRutaEstado.Pendiente,
                    "El contratista aún no generó el Dossier de esta semana."));
                continue;
            }

            var estado = EstadosDocumentoCumple.Contains(doc.Estado) ? HojaRutaEstado.Cumple : HojaRutaEstado.Pendiente;
            resultado.Add(new HojaRutaItemDto(codigo, tipo, estado, $"Estado: {doc.Estado}."));
        }

        return resultado;
    }

    /// <summary>Solo exige el informe si hubo al menos un accidente de ese contratista en la
    /// semana — si no hubo ninguno, el ítem no aplica, no es una falta.</summary>
    private static async Task<HojaRutaItemDto> GetItemInformeAccidenteAsync(
        AppDbContext ctx, int contributorId, DateOnly fechaInicio, DateOnly fechaFin)
    {
        var fechaInicioDt = DateTime.SpecifyKind(fechaInicio.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var fechaFinDt = DateTime.SpecifyKind(fechaFin.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        var accidenteIds = await ctx.SsomaAccidenteIncidente.AsNoTracking()
            .Where(a => a.ContributorId == contributorId && a.Fecha >= fechaInicioDt && a.Fecha <= fechaFinDt)
            .Select(a => a.Id)
            .ToListAsync();

        if (accidenteIds.Count == 0)
            return new HojaRutaItemDto("informe_accidente", "Informe de accidente/incidente",
                HojaRutaEstado.NoAplica, "Sin accidentes reportados esta semana.");

        var entregables = await ctx.SsomaEntregable.AsNoTracking()
            .Where(e => accidenteIds.Contains(e.AccidenteIncidenteId)
                     && e.TipoId == SsomaEntregableTipoIds.RegistroDeAccidentes)
            .Select(e => e.Estado)
            .ToListAsync();

        var presentados = entregables.Count(e => e is "Presentado" or "Aprobado");
        var estado = accidenteIds.Count > 0 && presentados == accidenteIds.Count
            ? HojaRutaEstado.Cumple : HojaRutaEstado.Pendiente;

        return new HojaRutaItemDto("informe_accidente", "Informe de accidente/incidente", estado,
            $"{presentados}/{accidenteIds.Count} accidente(s) con informe presentado.");
    }

    /// <summary>Informativo, no pass/fail — el usuario pidió el conteo de abiertas vs cerradas,
    /// no un aprobado/no aprobado.</summary>
    private static async Task<HojaRutaItemDto> GetItemObservacionesRacAsync(
        AppDbContext ctx, int contributorId, int proyectoId, DateOnly fechaInicio, DateOnly fechaFin)
    {
        var fechaInicioDt = DateTime.SpecifyKind(fechaInicio.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var fechaFinDt = DateTime.SpecifyKind(fechaFin.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        var racs = await ctx.SsomaRacs.AsNoTracking()
            .Where(r => r.EmpresaReportadaId == contributorId && r.ProyectoId == proyectoId
                     && r.FechaReporte >= fechaInicioDt && r.FechaReporte <= fechaFinDt)
            .Select(r => r.Estado)
            .ToListAsync();

        var cerradas = racs.Count(e => e == "Cerrado");
        var abiertas = racs.Count - cerradas;

        return new HojaRutaItemDto("observaciones_rac", "Levantamiento de observaciones",
            HojaRutaEstado.Informativo, $"{cerradas} cerrada(s), {abiertas} abierta(s) de {racs.Count} total.");
    }

    /// <summary>Solo aplica si el contratista tiene equipos registrados; si no tiene ninguno,
    /// no es una falta.</summary>
    private static async Task<HojaRutaItemDto> GetItemCertificadoOperatividadAsync(AppDbContext ctx, int contributorId)
    {
        var equipoIds = await ctx.SsEquipo.AsNoTracking()
            .Where(e => e.PropietarioEmpresaId == contributorId && e.Activo)
            .Select(e => e.Id)
            .ToListAsync();

        if (equipoIds.Count == 0)
            return new HojaRutaItemDto("cert_operatividad", "Certificado de operatividad de equipos",
                HojaRutaEstado.NoAplica, "El contratista no tiene equipos registrados.");

        var aprobados = await ctx.SsHabEquipo.AsNoTracking()
            .CountAsync(h => equipoIds.Contains(h.EquipoId)
                           && h.ItemId == HabItemEquipoIds.CertificadoOperatividad
                           && h.Estado == "Aprobado");

        var estado = aprobados == equipoIds.Count ? HojaRutaEstado.Cumple : HojaRutaEstado.Pendiente;
        return new HojaRutaItemDto("cert_operatividad", "Certificado de operatividad de equipos", estado,
            $"{aprobados}/{equipoIds.Count} equipo(s) con certificado aprobado.");
    }
}
