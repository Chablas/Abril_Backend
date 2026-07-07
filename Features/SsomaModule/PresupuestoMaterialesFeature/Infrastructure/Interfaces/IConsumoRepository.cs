using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

public interface IConsumoRepository
{
    Task<bool> ExisteHashAsync(string hash);
    Task<bool> ExisteSolapamientoFechasAsync(int projectId, DateOnly fechaMin, DateOnly fechaMax, int? excluirCargaId = null);
    Task<SsConsumoCarga> CrearCargaAsync(SsConsumoCarga carga);
    Task InsertarLineasBulkAsync(IEnumerable<SsConsumoLinea> lineas);
    Task<List<SsConsumoLinea>> ObtenerLineasSinEstandarizarAsync(int cargaId);
    Task ActualizarLineaEstandarizadaAsync(long lineaId, int itemId, bool perteneceSsoma, string metodo, decimal score, string? estadoRevision, decimal factorConversion = 1);
    Task ActualizarContadoresCargaAsync(int cargaId, int estandarizadas, int pendientes);
    Task<List<ConsumoCargaResumenDto>> ObtenerCargasPorProyectoAsync(int projectId);
    Task<List<MaterialPendienteDto>> ObtenerPendientesRevisionAsync(int projectId);
    Task<List<MaterialPendienteGlobalDto>> ObtenerPendientesRevisionGlobalAsync();
    Task<List<MaterialNoSsomaDto>> ObtenerNoSsomaAsync();
    Task<SsConsumoLinea?> ObtenerLineaPorIdAsync(long lineaId);
    Task ActualizarRevisionAsync(long lineaId, string decision, int? itemIdConfirmado);
    Task<int> AsignarHitosPorFechaAsync(int projectId);
}
