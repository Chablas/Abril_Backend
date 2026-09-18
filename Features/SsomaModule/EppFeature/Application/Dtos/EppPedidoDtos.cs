namespace Abril_Backend.Features.SsomaModule.EppFeature.Application.Dtos
{
    public class EppPedidoLineaDto
    {
        public string NombreTecnico { get; set; } = null!;
        public string NombreComercial { get; set; } = null!;
        public string? Marca { get; set; }
        public string? Modelo { get; set; }
        public string? Talla { get; set; }
        public int Cantidad { get; set; }
    }

    public class EppPedidoListDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = null!;
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        public string? GeneradoPorNombre { get; set; }
        public DateTimeOffset Fecha { get; set; }
        public string Estado { get; set; } = null!;
        public int TotalLineas { get; set; }
        public int TotalUnidades { get; set; }
    }

    public class EppPedidoDetalleDto : EppPedidoListDto
    {
        public string? Observaciones { get; set; }
        public List<EppPedidoLineaDto> Lineas { get; set; } = new();
    }

    public class EppPedidoLineaCreateDto
    {
        public int? EppItemId { get; set; }
        public int? EppModeloId { get; set; }
        public string NombreTecnico { get; set; } = null!;
        public string NombreComercial { get; set; } = null!;
        public string? Marca { get; set; }
        public string? Modelo { get; set; }
        public string? Talla { get; set; }
        public int Cantidad { get; set; }
    }

    public class EppPedidoCreateDto
    {
        public int ProjectId { get; set; }
        public string? Observaciones { get; set; }
        public List<EppPedidoLineaCreateDto> Lineas { get; set; } = new();
    }
}
