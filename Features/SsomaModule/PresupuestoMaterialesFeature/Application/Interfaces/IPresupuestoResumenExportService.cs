using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;

public interface IPresupuestoResumenExportService
{
    /// <summary>Junta las 5 fuentes de costo del presupuesto vigente del proyecto (Materiales,
    /// Personal, Vigilancia, Servicios fijos, Kits) en un único resumen de recursos. Null si el
    /// proyecto todavía no tiene ningún presupuesto generado.</summary>
    Task<PresupuestoResumenRecursosDto?> ObtenerResumenAsync(int projectId);

    /// <summary>Agrupa el Desagregado en las ~14 partidas del presupuesto general de obra (ver
    /// PPTO SOMA REFERENCIAL.xlsx) — la vista que realmente usa Costos. Null si el proyecto
    /// todavía no tiene ningún presupuesto generado.</summary>
    Task<PresupuestoResumenRecursosDto?> ObtenerResumenAgregadoAsync(int projectId);

    /// <summary>Arma el resumen y lo exporta como Excel (bytes del .xlsx) con dos hojas: "RESUMEN"
    /// (partidas agregadas) y "DESAGREGADO DE RECURSOS" (detalle plano). Null si el proyecto
    /// todavía no tiene ningún presupuesto generado.</summary>
    Task<byte[]?> ExportarExcelAsync(int projectId);
}
