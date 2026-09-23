namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

public class ResiduoTipoDto
{
    public int Id { get; set; }
    public string? CodigoSigersol { get; set; }
    public string Nombre { get; set; } = "";
    public bool EsPeligroso { get; set; }
    public bool Activo { get; set; }
    public decimal? FactorVigente { get; set; }
}

public class ResiduoTipoUpsertDto
{
    public string? CodigoSigersol { get; set; }
    public string Nombre { get; set; } = "";
    public bool EsPeligroso { get; set; }
    public bool Activo { get; set; } = true;
}

public class ResiduoTipoFactorDto
{
    public int Id { get; set; }
    public int ResiduoTipoId { get; set; }
    public decimal FactorM3aTon { get; set; }
    public DateOnly VigenciaDesde { get; set; }
    public DateOnly? VigenciaHasta { get; set; }
}

public class ResiduoTipoFactorUpsertDto
{
    public decimal FactorM3aTon { get; set; }
    public DateOnly VigenciaDesde { get; set; }
    public DateOnly? VigenciaHasta { get; set; }
}
