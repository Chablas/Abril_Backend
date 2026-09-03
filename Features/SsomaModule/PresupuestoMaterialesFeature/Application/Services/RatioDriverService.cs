using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;

/// <summary>
/// Ratios historicos de los drivers del proyecto (HH total y N Trabajadores) por m2 de area
/// techada — mismo patron IQR que RatioService usa para materiales.
///
/// Cada driver tiene DOS fuentes, y la "oficial" (Cantidad/Ratio, la que entra a la mediana)
/// prioriza el valor manual sobre el calculado cuando el responsable ya lo cargó en Datos Base:
///   - Calculado ("en vivo"): HH = suma del Tareo de Control de Acceso o Excel de planilla;
///     Trabajadores = distintos que alguna vez tuvieron una vinculacion (worker_vinculaciones)
///     a ese proyecto. Puede quedar parcial o directamente vacio si la fuente no esta bien
///     cargada para ese proyecto (caso real: SAUCO con solo 3 filas en worker_vinculaciones).
///   - Manual (Project.HhTotalCasa / CantTrabajadoresCasa): lo que el responsable tipeo a mano
///     en Datos Base, tipicamente el total final real de un proyecto ya cerrado. Gana sobre el
///     calculado cuando existe.
/// Ambos valores se guardan (CantidadCalculado / CantidadManual) para poder comparar en la UI;
/// "es outlier" es solo informativo, la unica autoridad real sobre la mediana es IncluidoManual
/// — el responsable decide caso por caso, incluso si el proyecto sigue activo.
/// </summary>
public class RatioDriverService : IRatioDriverService
{
    public const string HH = "HH";
    public const string TRABAJADORES = "TRABAJADORES";

    private readonly IRatioDriverRepository _repo;
    public RatioDriverService(IRatioDriverRepository repo) => _repo = repo;

    public async Task<CalcularRatiosDriversResultDto> CalcularRatiosAsync()
    {
        var proyectos = await _repo.ObtenerProyectosConAreaAsync();
        if (proyectos.Count == 0)
            return new CalcularRatiosDriversResultDto { RatiosCalculados = 0, ProyectosSinArea = 0, ProyectosSinTareo = 0 };

        var projectIds = proyectos.Select(p => p.ProjectId).ToList();
        var hhPorProyecto = (await _repo.ObtenerHhRealPorProyectoAsync(projectIds)).ToDictionary(h => h.ProjectId);
        var trabPorProyecto = (await _repo.ObtenerTrabajadoresRealPorProyectoAsync(projectIds)).ToDictionary(t => t.ProjectId);

        var items = new List<RatioDriverUpsertItem>();
        var sinTareo = 0;

        foreach (var p in proyectos)
        {
            // Un proyecto que sigue Activo/Inactivo (obra sin cerrar) aporta un HH/Trabajadores
            // parcial, no el total real de la obra — por eso arranca EXCLUIDO del cálculo de la
            // mediana por defecto. Solo un proyecto Finalizado se incluye automáticamente. El
            // responsable siempre puede forzar la inclusión a mano desde la pantalla de Ratios,
            // y esa decisión manual queda registrada y no se pisa en recálculos posteriores.
            var incluidoPorDefecto = p.CicloVida == "Finalizado";

            var hhCalculado = hhPorProyecto.TryGetValue(p.ProjectId, out var hh) ? hh.HhTotal : 0;
            var diasRegistrados = hhPorProyecto.TryGetValue(p.ProjectId, out var hhDias) ? hhDias.DiasRegistrados : 0;
            // El valor manual de Datos Base solo manda sobre el calculado cuando el proyecto ya
            // NO está Activo (Finalizado, Inactivo, o el campo nunca se cargó — típico en obras
            // viejas). Antes esto se decidía por HhFuente=="HH_REAL", pero ese campo es facil de
            // olvidar actualizar; el estado del proyecto es lo que el responsable ya revisa y
            // controla. Si sigue Activo, el manual es un proyectado/estimado de presupuesto, no
            // un dato real definitivo (ver caso CEDRO 33: su manual era un proyectado inicial,
            // muy distinto del real que ya se está midiendo por Tareo/Excel).
            var esManualConfiable = p.CicloVida != "Activo";
            var hhManual = esManualConfiable ? p.HhTotalCasa : null;
            var hhProyectado = !esManualConfiable ? p.HhTotalCasa : null;
            var hhOficial = hhManual ?? hhCalculado;

            if (hhOficial > 0)
            {
                items.Add(new RatioDriverUpsertItem
                {
                    TipoDriver = HH,
                    ProjectId = p.ProjectId,
                    AreaTechada = p.AreaTechada,
                    Cantidad = hhOficial,
                    Ratio = hhOficial / p.AreaTechada,
                    CantidadCalculado = hhCalculado,
                    CantidadManual = hhManual,
                    CantidadProyectado = hhProyectado,
                    // El acumulado real (Excel/Tareo) manda por defecto sobre el manual: si el
                    // proyecto ya cerró y tiene Excel subido, ese acumulado ES el real final, no
                    // una estimación aparte. El manual solo se usa por defecto si todavía no hay
                    // ningún dato real medido (proyecto cerrado sin Excel ni Tareo completo).
                    FuenteCantidadDefault = hhCalculado > 0 ? "CALCULADO" : (hhManual != null ? "MANUAL" : null),
                    DiasRegistrados = diasRegistrados,
                    IncluidoManualDefault = incluidoPorDefecto,
                });
            }
            else
            {
                sinTareo++;
            }

            var trabCalculado = trabPorProyecto.TryGetValue(p.ProjectId, out var trab) ? trab.TotalTrabajadoresDistintos : 0;
            var trabManual = esManualConfiable && decimal.TryParse(p.CantTrabajadoresCasa, out var trabManualParsed)
                ? trabManualParsed
                : (decimal?)null;
            var trabProyectado = !esManualConfiable && decimal.TryParse(p.CantTrabajadoresCasa, out var trabProyectadoParsed)
                ? trabProyectadoParsed
                : (decimal?)null;
            var trabOficial = trabManual ?? trabCalculado;

            if (trabOficial > 0)
            {
                items.Add(new RatioDriverUpsertItem
                {
                    TipoDriver = TRABAJADORES,
                    ProjectId = p.ProjectId,
                    AreaTechada = p.AreaTechada,
                    Cantidad = trabOficial,
                    Ratio = trabOficial / p.AreaTechada,
                    CantidadCalculado = trabCalculado,
                    CantidadManual = trabManual,
                    CantidadProyectado = trabProyectado,
                    FuenteCantidadDefault = trabCalculado > 0 ? "CALCULADO" : (trabManual != null ? "MANUAL" : null),
                    DiasRegistrados = 0,
                    IncluidoManualDefault = incluidoPorDefecto,
                });
            }
        }

        if (items.Count > 0)
        {
            await _repo.UpsertRatiosBulkAsync(items);
            await RecalcularOutliersAsync();
        }

        return new CalcularRatiosDriversResultDto
        {
            RatiosCalculados = items.Count,
            ProyectosSinArea = 0,
            ProyectosSinTareo = sinTareo,
        };
    }

    public async Task<RatioDriverComparacionDto> ObtenerComparacionAsync(string tipoDriver)
    {
        var tipo = NormalizarTipo(tipoDriver);
        var proyectos = await _repo.ObtenerPorTipoAsync(tipo);
        // Igual que en materiales: el checkbox manual es la unica autoridad sobre que entra
        // al calculo — no se filtra por HhFuente ni por si el proyecto sigue activo.
        var incluidos = proyectos.Where(p => p.IncluidoManual).Select(p => p.Ratio).OrderBy(x => x).ToList();

        return new RatioDriverComparacionDto
        {
            TipoDriver = tipo,
            Proyectos = proyectos,
            MedianaRatio = incluidos.Count > 0 ? Mediana(incluidos) : 0,
            PromedioRatio = incluidos.Count > 0 ? incluidos.Average() : 0,
            MinRatio = incluidos.Count > 0 ? incluidos.Min() : 0,
            MaxRatio = incluidos.Count > 0 ? incluidos.Max() : 0,
        };
    }

    public Task ActualizarIncluidoManualAsync(string tipoDriver, int projectId, bool incluir) =>
        _repo.ActualizarIncluidoManualAsync(NormalizarTipo(tipoDriver), projectId, incluir);

    public Task ActualizarFuenteCantidadAsync(string tipoDriver, int projectId, string? fuente) =>
        _repo.ActualizarFuenteCantidadAsync(NormalizarTipo(tipoDriver), projectId, fuente?.Trim().ToUpperInvariant());

    public async Task<RatiosDriversRecomendadosDto> ObtenerRecomendadosAsync() =>
        new()
        {
            Hh = await CalcularRecomendadoAsync(HH),
            Trabajadores = await CalcularRecomendadoAsync(TRABAJADORES),
        };

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<RatioDriverRecomendadoDto?> CalcularRecomendadoAsync(string tipo)
    {
        var proyectos = await _repo.ObtenerPorTipoAsync(tipo);
        var validos = proyectos
            .Where(p => p.IncluidoManual && !p.EsOutlier)
            .Select(p => p.Ratio)
            .OrderBy(x => x)
            .ToList();
        if (validos.Count == 0) return null;
        return new RatioDriverRecomendadoDto { TipoDriver = tipo, RatioRecomendado = Mediana(validos), NProyectos = validos.Count };
    }

    /// <summary>Recalcula el flag de outlier (IQR) por tipo de driver, en un solo lote.</summary>
    private async Task RecalcularOutliersAsync()
    {
        var filas = await _repo.ObtenerTodosParaOutlierAsync();
        var updates = new List<RatioDriverOutlierUpdate>();

        foreach (var grupo in filas.GroupBy(f => f.TipoDriver))
        {
            var enGrupo = grupo.ToList();
            if (enGrupo.Count < 4)
            {
                foreach (var f in enGrupo)
                    updates.Add(new RatioDriverOutlierUpdate { Id = f.Id, EsOutlier = false });
                continue;
            }

            var valores = enGrupo.Select(f => f.Ratio).OrderBy(x => x).ToList();
            var n = valores.Count;
            var q1 = valores[n / 4];
            var q3 = valores[3 * n / 4];
            var iqr = q3 - q1;
            var limiteInf = q1 - 1.5m * iqr;
            var limiteSup = q3 + 1.5m * iqr;

            foreach (var f in enGrupo)
            {
                var esOutlier = f.Ratio < limiteInf || f.Ratio > limiteSup;
                updates.Add(new RatioDriverOutlierUpdate { Id = f.Id, EsOutlier = esOutlier });
            }
        }

        if (updates.Count > 0)
            await _repo.ActualizarOutliersBulkAsync(updates);
    }

    private static string NormalizarTipo(string tipo) => tipo.Trim().ToUpperInvariant();

    private static decimal Mediana(List<decimal> sorted)
    {
        var n = sorted.Count;
        return n % 2 == 0 ? (sorted[n / 2 - 1] + sorted[n / 2]) / 2 : sorted[n / 2];
    }
}
