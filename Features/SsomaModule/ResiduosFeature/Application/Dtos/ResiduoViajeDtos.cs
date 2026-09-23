namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

public class ResiduoViajeDto
{
    public long Id { get; set; }
    public int ProjectId { get; set; }
    public int? AutorizacionDmeId { get; set; }
    public int ResiduoTipoId { get; set; }
    public string? NombreResiduoTipo { get; set; }
    public int EoRsId { get; set; }
    public string? NombreEoRs { get; set; }
    public string? Contratista { get; set; }
    public string? Origen { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal CantidadM3 { get; set; }
    public decimal? CantidadTon { get; set; }
    public string TipoManejo { get; set; } = "";
    public string? GestorReceptor { get; set; }
    public string? DestinoFinal { get; set; }
    public string? NumeroRegistro { get; set; }
    public string? NumeroCertificado { get; set; }
    public string? ArchivoUrl { get; set; }
    public bool Activo { get; set; }
}

public class ResiduoViajeUpsertDto
{
    public int ProjectId { get; set; }
    public int? AutorizacionDmeId { get; set; }
    public int ResiduoTipoId { get; set; }
    public int EoRsId { get; set; }
    public string? Contratista { get; set; }
    public string? Origen { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal CantidadM3 { get; set; }
    /// <summary>Si viene con valor, se respeta (ajuste manual). Si es null, se calcula automáticamente
    /// desde el factor vigente del tipo de residuo para la fecha del viaje.</summary>
    public decimal? CantidadTon { get; set; }
    public string TipoManejo { get; set; } = "";
    public string? GestorReceptor { get; set; }
    public string? DestinoFinal { get; set; }
    public string? NumeroRegistro { get; set; }
    public string? NumeroCertificado { get; set; }
}

public class ResiduoViajeListFiltroDto
{
    public int? ProjectId { get; set; }
    public int? ResiduoTipoId { get; set; }
    public int? EoRsId { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public int Pagina { get; set; } = 1;
    public int TamanoPagina { get; set; } = 20;
}

public class ResiduoViajePagedDto
{
    public List<ResiduoViajeDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanoPagina { get; set; }
}
