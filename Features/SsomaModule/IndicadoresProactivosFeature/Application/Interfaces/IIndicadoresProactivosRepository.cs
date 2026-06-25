using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Interfaces;

public interface IIndicadoresProactivosRepository
{
    // ── Programación de inspecciones ──────────────────────────────────────────

    Task<List<InspeccionTipoDto>> GetTiposInspeccionAsync();

    Task<ProgInspeccionResumenDto> GetProgInspeccionAsync(int proyectoId, int mes, int anio);

    Task GuardarProgInspeccionAsync(GuardarProgInspeccionRequest request, int userId);

    // ── Cálculo de metas e indicadores ───────────────────────────────────────

    /// <summary>
    /// Devuelve metas y actuals por empresa para un proyecto y período.
    /// Solo incluye empresas activas (>= 10 días en el mes).
    /// Si mes/anio corresponde al mes actual, proyecta la meta al cierre.
    /// </summary>
    Task<List<MetaEmpresaDto>> GetMetasEmpresaAsync(int proyectoId, int mes, int anio);

    /// <summary>
    /// Devuelve el resumen de indicadores proactivos de todos los proyectos activos
    /// para el dashboard de seguimiento (vista competitiva).
    /// </summary>
    Task<List<IndicadorProactivoProyectoDto>> GetSeguimientoTodosProyectosAsync(int mes, int anio);

    // ── Puntaje del mes ───────────────────────────────────────────────────────

    Task<PuntajeMesDto> GetPuntajeMesAsync(int proyectoId, int mes, int anio);

    Task<List<PuntajeMesDto>> GetPuntajeTodosProyectosAsync(int mes, int anio);
}
