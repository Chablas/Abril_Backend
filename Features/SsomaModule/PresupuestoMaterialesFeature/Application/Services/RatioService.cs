using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;

public class RatioService : IRatioService
{
    private readonly IRatioRepository _repo;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public RatioService(IRatioRepository repo, IDbContextFactory<AppDbContext> factory)
    {
        _repo = repo;
        _factory = factory;
    }

    public async Task<CalcularRatiosResultDto> CalcularRatiosProyectoAsync(int projectId)
    {
        using var ctx = _factory.CreateDbContext();
        var proyecto = await ctx.Project.FindAsync(projectId)
            ?? throw new AbrilException("Proyecto no encontrado.", 404);

        var consumos = await _repo.ObtenerConsumosPorProyectoAsync(projectId);
        if (consumos.Count == 0)
            throw new AbrilException("El proyecto no tiene consumos estandarizados para calcular ratios.", 400);

        var resultado = new CalcularRatiosResultDto
        {
            ProjectId = projectId,
            ProjectDescription = proyecto.ProjectDescription
        };

        // Obtener valores driver del proyecto
        var hh           = proyecto.HhTotalCasa ?? 0;
        var areaTechada  = proyecto.AreaTechadaM2 ?? 0;
        var trabajadores = ParseTrabajadores(proyecto.CantTrabajadoresCasa);

        // Calcular ratio para cada familia
        var todosLosRatios = new List<(int familiaId, decimal ratio)>();

        foreach (var consumo in consumos)
        {
            var driver = ObtenerDriver(consumo.VariableBase, hh, areaTechada, trabajadores, consumo.CantidadTotal);

            if (driver == 0)
            {
                resultado.FamiliasSinDriver++;
                resultado.Advertencias.Add($"'{consumo.NombreFamilia}': driver '{consumo.VariableBase}' = 0 en proyecto {proyecto.ProjectDescription}. Ratio no calculado.");
                continue;
            }

            var ratio = consumo.CantidadTotal / driver;
            todosLosRatios.Add((consumo.FamiliaId, ratio));

            await _repo.UpsertRatioAsync(
                consumo.FamiliaId, projectId, consumo.VariableBase,
                consumo.CantidadTotal, consumo.PrecioUnitarioPromedio,
                driver, ratio, esOutlier: false);

            resultado.RatiosCalculados++;
        }

        // Detectar outliers con IQR sobre los ratios del proyecto
        if (todosLosRatios.Count >= 4)
            await MarcarOutliersAsync(todosLosRatios, projectId);

        return resultado;
    }

    public async Task<List<RatioProyectoDto>> ObtenerRatiosProyectoAsync(int projectId) =>
        await _repo.ObtenerRatiosPorProyectoAsync(projectId);

    public async Task<RatioFamiliaComparacionDto?> ObtenerComparacionFamiliaAsync(int familiaId)
    {
        var ratios = await _repo.ObtenerRatiosPorFamiliaAsync(familiaId);
        if (ratios.Count == 0) return null;

        var primero = ratios[0];
        var valores = ratios.Where(r => !r.EsOutlier).Select(r => r.RatioCantidad).OrderBy(x => x).ToList();

        return new RatioFamiliaComparacionDto
        {
            FamiliaId = familiaId,
            NombreFamilia = primero.NombreFamilia,
            TipoMaterial = primero.TipoMaterial,
            VariableBase = primero.VariableBase,
            PromedioRatio = valores.Count > 0 ? valores.Average() : 0,
            MedianaRatio = valores.Count > 0 ? Mediana(valores) : 0,
            MinRatio = valores.Count > 0 ? valores.Min() : 0,
            MaxRatio = valores.Count > 0 ? valores.Max() : 0,
            PromedioPrecioUnitario = ratios.Where(r => !r.EsOutlier).Select(r => r.PrecioUnitarioPromedio).DefaultIfEmpty(0).Average(),
            Proyectos = ratios.Select(r => new RatioProyectoItemDto
            {
                ProjectId = r.ProjectId,
                ProjectDescription = r.ProjectDescription,
                RatioCantidad = r.RatioCantidad,
                PrecioUnitario = r.PrecioUnitarioPromedio,
                CantidadTotal = r.CantidadTotal,
                ValorDriver = r.ValorDriver,
                EsOutlier = r.EsOutlier
            }).ToList()
        };
    }

    public async Task<ResumenRatiosDto> ObtenerResumenAsync()
    {
        var proyectos = await _repo.ObtenerResumenAsync();
        using var ctx = _factory.CreateDbContext();
        var totalFamilias = await ctx.SsMaterialFamilia.CountAsync(f => f.Activo && f.PerteneceSsoma);
        return new ResumenRatiosDto { Proyectos = proyectos, TotalFamilias = totalFamilias };
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static decimal ObtenerDriver(string variableBase, decimal hh, decimal area, decimal trabajadores, decimal cantidadTotal) =>
        variableBase switch
        {
            "HH"           => hh,
            "AREATECHADA"  => area,
            "TRABAJADORES" => trabajadores,
            "CALCULADO"    => cantidadTotal > 0 ? 1 : 0, // ratio = cantidad / 1 = cantidad misma
            "FIJO"         => 1,
            "METRADO"      => 1,
            _              => hh
        };

    private static decimal ParseTrabajadores(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return 0;
        var clean = new string(valor.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return decimal.TryParse(clean, out var d) ? d : 0;
    }

    private async Task MarcarOutliersAsync(List<(int familiaId, decimal ratio)> ratios, int projectId)
    {
        var valores = ratios.Select(r => r.ratio).OrderBy(x => x).ToList();
        var n = valores.Count;
        var q1 = valores[n / 4];
        var q3 = valores[3 * n / 4];
        var iqr = q3 - q1;
        var limiteInf = q1 - 1.5m * iqr;
        var limiteSup = q3 + 1.5m * iqr;

        foreach (var (familiaId, ratio) in ratios)
        {
            var esOutlier = ratio < limiteInf || ratio > limiteSup;
            if (esOutlier)
            {
                // Actualizar solo el flag de outlier
                var consumo = ratios.First(r => r.familiaId == familiaId);
                // Re-upsert con esOutlier=true (los demás valores ya están en DB)
                // Usamos el repo con valores dummy — el ON CONFLICT solo actualiza es_outlier
                using var ctx = _factory.CreateDbContext();
                var existing = await ctx.SsRatioProyecto
                    .FirstOrDefaultAsync(r => r.FamiliaId == familiaId && r.ProjectId == projectId);
                if (existing != null)
                {
                    existing.EsOutlier = true;
                    await ctx.SaveChangesAsync();
                }
            }
        }
    }

    private static decimal Mediana(List<decimal> sorted)
    {
        var n = sorted.Count;
        return n % 2 == 0 ? (sorted[n / 2 - 1] + sorted[n / 2]) / 2 : sorted[n / 2];
    }
}
