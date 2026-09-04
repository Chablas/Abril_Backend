using Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Application.Dtos;

namespace Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Application.Interfaces;

public interface ICostoService
{
    Task<CostoFiltrosDTO> GetFiltros();
    Task<CostoMatrizDTO> GetMatriz(int proyectoId, int anio, int mes);
    Task UpsertRegistro(UpsertCostoRegistroDTO body, string? creadoPor);
    Task UpsertProyeccion(UpsertCostoProyeccionDTO body, string? creadoPor);
    Task<CostoDashboardDTO> GetDashboard(int anio, int mes);
    Task<CostoEvolucionDTO> GetEvolucion(int anioDesde, int mesDesde, int cantidadMeses);
    Task UpsertMeta(UpsertCostoMetaDTO body, string? creadoPor);
}
