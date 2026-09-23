namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

public class ResiduoConstanciaDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? EoRsId { get; set; }
    public string? NombreEoRs { get; set; }
    public string? Contratista { get; set; }
    public string? Destino { get; set; }
    public int PeriodoAnio { get; set; }
    public int PeriodoMes { get; set; }
    public string? NumeroCertificado { get; set; }
    public string ArchivoUrl { get; set; } = "";
}

public class ResiduoConstanciaUpsertDto
{
    public int ProjectId { get; set; }
    public int? EoRsId { get; set; }
    public string? Contratista { get; set; }
    public string? Destino { get; set; }
    public int PeriodoAnio { get; set; }
    public int PeriodoMes { get; set; }
    public string? NumeroCertificado { get; set; }
}

public class ResiduoConstanciaFinalDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public DateOnly FechaEmision { get; set; }
    public string ArchivoUrl { get; set; } = "";
    public string? Observaciones { get; set; }
}

public class ResiduoConstanciaFinalUpsertDto
{
    public int ProjectId { get; set; }
    public DateOnly FechaEmision { get; set; }
    public string? Observaciones { get; set; }
}
