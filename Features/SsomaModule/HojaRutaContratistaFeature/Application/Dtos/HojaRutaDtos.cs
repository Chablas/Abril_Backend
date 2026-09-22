namespace Abril_Backend.Features.SsomaModule.HojaRutaContratistaFeature.Application.Dtos;

/// <summary>Valores posibles de HojaRutaItemDto.Estado. String plano (no enum) porque no hay
/// JsonStringEnumConverter global en Program.cs — un enum C# serializaría como número y
/// rompería silenciosamente el contrato con el frontend, que espera estos strings literales.
/// "Informativo" es para ítems que no son pass/fail (ej. observaciones RAC, donde interesa el
/// conteo, no un aprobado/no aprobado). "Manual" es para ítems sin fuente automática todavía.</summary>
public static class HojaRutaEstado
{
    public const string Cumple = "Cumple";
    public const string Pendiente = "Pendiente";
    public const string NoAplica = "NoAplica";
    public const string Informativo = "Informativo";
    public const string Manual = "Manual";
}

public record HojaRutaItemDto(
    string Codigo,
    string Nombre,
    string Estado,
    string? Detalle,
    int? TotalRequerido = null,
    int? TotalCumplido = null);

public record ContratistaActivoDto(int ContributorId, string Nombre);

public record HojaRutaResumenDto(
    int ContributorId,
    string EmpresaNombre,
    int ProyectoId,
    int Anio,
    int NumeroSemana,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    int TotalTrabajadoresActivos,
    List<HojaRutaItemDto> Items);
