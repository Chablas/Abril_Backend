using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Interfaces;

public interface IIndicadoresProactivosService
{
    Task<List<InspeccionTipoDto>> GetTiposInspeccionAsync();
    Task<ProgInspeccionResumenDto> GetProgInspeccionAsync(int proyectoId, int mes, int anio);
    Task GuardarProgInspeccionAsync(GuardarProgInspeccionRequest request, int userId);
    Task<IndicadorProactivoProyectoDto> GetIndicadoresProyectoAsync(int proyectoId, int mes, int anio);
    Task<List<IndicadorProactivoProyectoDto>> GetSeguimientoTodosProyectosAsync(int mes, int anio);
    Task<PuntajeMesDto> GetPuntajeMesAsync(int proyectoId, int mes, int anio);
    Task<List<PuntajeMesDto>> GetPuntajeTodosProyectosAsync(int mes, int anio);

    // ── Indicadores reactivos IF / IG / IA ───────────────────────────────────
    Task<IndicadorReactivoProyectoDto> GetIndicadoresReactivosAsync(int proyectoId, int mes, int anio);
    Task<List<IndicadorReactivoProyectoDto>> GetIndicadoresReactivosTodosAsync(int mes, int anio);
}
