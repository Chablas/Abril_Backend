namespace Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Dtos;

public record IndicadoresReactivosQuery(int Mes, int Anio, int? ProyectoId = null);

public record IndicadorReactivoProyectoDto
{
    public int ProyectoId { get; init; }
    public string ProyectoNombre { get; init; } = "";
    public int Mes { get; init; }
    public int Anio { get; init; }

    // Horas Hombre Trabajadas del período
    public long HorasHombreTrabajadas { get; init; }

    // Contadores base
    public int TotalAccidentes { get; init; }
    public int TotalDiasPerdidos { get; init; }

    // Indicadores MINTRA (base 10^6)
    public decimal IndiceFrecuencia { get; init; }    // IF = accidentes × 10⁶ / HHT
    public decimal IndiceGravedad { get; init; }      // IG = días perdidos × 10⁶ / HHT
    public decimal IndiceAccidentabilidad { get; init; } // IA = IF × IG / 1000
}
