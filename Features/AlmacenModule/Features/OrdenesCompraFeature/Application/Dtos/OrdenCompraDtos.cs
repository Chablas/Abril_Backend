namespace Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Application.Dtos;

public class CreateAlmacenOrdenCompraDTO
{
    public int ProyectoId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Proveedor { get; set; } = string.Empty;
    public int? ContratistaId { get; set; }
    public decimal Monto { get; set; }
    public string Moneda { get; set; } = "PEN";
    public DateTime Fecha { get; set; }
}

public class AlmacenOrdenCompraListItemDTO
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Proveedor { get; set; } = string.Empty;
    public int? ContratistaId { get; set; }
    public decimal Monto { get; set; }
    public string Moneda { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string ArchivoUrl { get; set; } = string.Empty;
    public string ArchivoNombre { get; set; } = string.Empty;
    public string? SubidoPor { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AlmacenOrdenCompraQueryParams
{
    public int? ProyectoId { get; set; }
    public string? Tipo { get; set; }
    public string? Search { get; set; }
    public int Pagina { get; set; } = 1;
    public int PorPagina { get; set; } = 20;
}

public class AlmacenOrdenCompraListResponseDTO
{
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int PorPagina { get; set; }
    public List<AlmacenOrdenCompraListItemDTO> Items { get; set; } = [];
}
