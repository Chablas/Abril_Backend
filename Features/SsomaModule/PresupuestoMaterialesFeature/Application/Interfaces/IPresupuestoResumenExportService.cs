using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;

public interface IPresupuestoResumenExportService
{
    /// <summary>Junta las 5 fuentes de costo del presupuesto vigente del proyecto (Materiales,
    /// Personal, Vigilancia, Servicios fijos, Kits) en un único resumen de recursos. Null si el
    /// proyecto todavía no tiene ningún presupuesto generado.</summary>
    Task<PresupuestoResumenRecursosDto?> ObtenerResumenAsync(int projectId);

    /// <summary>Arma el resumen y lo exporta como Excel (bytes del .xlsx). Null si el proyecto
    /// todavía no tiene ningún presupuesto generado.</summary>
    Task<byte[]?> ExportarExcelAsync(int projectId);
}
