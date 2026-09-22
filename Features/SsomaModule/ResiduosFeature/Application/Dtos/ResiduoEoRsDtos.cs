namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

public class ResiduoEoRsDto
{
    public int Id { get; set; }
    public string Ruc { get; set; } = "";
    public string RazonSocial { get; set; } = "";
    public string TipoOperador { get; set; } = "";
    public string? NumeroRegistroMinam { get; set; }
    public DateOnly? VigenciaRegistro { get; set; }
    public string? Direccion { get; set; }
    public string? AmbitoGestion { get; set; }
    public bool Activo { get; set; }
}

public class ResiduoEoRsUpsertDto
{
    public string Ruc { get; set; } = "";
    public string RazonSocial { get; set; } = "";
    public string TipoOperador { get; set; } = "";
    public string? NumeroRegistroMinam { get; set; }
    public DateOnly? VigenciaRegistro { get; set; }
    public string? Direccion { get; set; }
    public string? AmbitoGestion { get; set; }
    public bool Activo { get; set; } = true;
}

public class ResiduoEoRsDocumentoDto
{
    public int Id { get; set; }
    public int EoRsId { get; set; }
    public string TipoDocumento { get; set; } = "";
    public string? Numero { get; set; }
    public DateOnly? VigenciaDesde { get; set; }
    public DateOnly? VigenciaHasta { get; set; }
    public string? ArchivoUrl { get; set; }
    public bool Cumple { get; set; }
}

public class ResiduoEoRsDocumentoUpsertDto
{
    public string TipoDocumento { get; set; } = "";
    public string? Numero { get; set; }
    public DateOnly? VigenciaDesde { get; set; }
    public DateOnly? VigenciaHasta { get; set; }
    public bool Cumple { get; set; }
}
