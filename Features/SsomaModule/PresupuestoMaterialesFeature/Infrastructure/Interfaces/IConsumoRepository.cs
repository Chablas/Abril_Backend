using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

public interface IConsumoRepository
{
    Task<SsConsumoCarga> CrearCargaAsync(SsConsumoCarga carga);
    /// <summary>Líneas activas con guía de origen conocida (carga acumulativa), para matchear contra el archivo nuevo.</summary>
    Task<List<SsConsumoLinea>> ObtenerLineasActivasConGuiaPorProyectoAsync(int projectId);
    /// <summary>
    /// Aplica en una sola transacción el diff entre la carga acumulada nueva y lo ya guardado:
    /// inserta las líneas nuevas, actualiza cantidad/precio de las regularizadas (conservando su
    /// clasificación de catálogo) y da de baja (activo=false) las que ya no aparecen en el archivo.
    /// </summary>
    Task AplicarDiffCargaAsync(
        IEnumerable<SsConsumoLinea> nuevas,
        IEnumerable<(long LineaId, decimal Cantidad, decimal PrecioUnitario, decimal PrecioTotal, DateOnly FechaGuia)> actualizaciones,
        IEnumerable<long> idsDarDeBaja,
        string motivoBaja);
    Task<List<SsConsumoLinea>> ObtenerLineasSinEstandarizarAsync(int cargaId);
    Task ActualizarLineaEstandarizadaAsync(long lineaId, int itemId, bool perteneceSsoma, string metodo, decimal score, string? estadoRevision, decimal factorConversion = 1);
    Task ActualizarContadoresCargaAsync(int cargaId, int estandarizadas, int pendientes);
    Task ActualizarResumenCargaAsync(int cargaId, int totalLineas, int nuevas, int actualizadas, int eliminadas);
    Task<List<ConsumoCargaResumenDto>> ObtenerCargasPorProyectoAsync(int projectId);
    Task<List<MaterialPendienteDto>> ObtenerPendientesRevisionAsync(int projectId);
    Task<List<MaterialPendienteGlobalDto>> ObtenerPendientesRevisionGlobalAsync();
    Task<List<MaterialNoSsomaDto>> ObtenerNoSsomaAsync();
    Task<SsConsumoLinea?> ObtenerLineaPorIdAsync(long lineaId);
    Task ActualizarRevisionAsync(long lineaId, string decision, int? itemIdConfirmado);
    Task<int> AsignarHitosPorFechaAsync(int projectId);
}
