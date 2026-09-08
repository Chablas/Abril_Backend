namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

/// <summary>Fila de servicio de vigilancia externa asignada a un hito crítico real del cronograma
/// del proyecto — se factura por punto/turno, no por vigilante (eso es el rol VIGIA interno de
/// Dotación de personal, tabla aparte).</summary>
public class VigilanciaHitoDto
{
    public int Id { get; set; }
    public int HitoId { get; set; }
    public string HitoDescripcion { get; set; } = "";
    public DateOnly? HitoFecha { get; set; }
    public bool EsHitoCritico { get; set; }
    public int? HitoSalidaId { get; set; }
    public string? HitoSalidaDescripcion { get; set; }
    public DateOnly? HitoSalidaFecha { get; set; }
    public int CantidadPuntos { get; set; }
    public decimal Semanas { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }
}

public class VigilanciaHitoItemInputDto
{
    public int HitoId { get; set; }
    public int? HitoSalidaId { get; set; }
    public int CantidadPuntos { get; set; }
    public decimal Semanas { get; set; }
}

public class VigilanciaHitoGuardarDto
{
    public List<VigilanciaHitoItemInputDto> Items { get; set; } = [];
}
