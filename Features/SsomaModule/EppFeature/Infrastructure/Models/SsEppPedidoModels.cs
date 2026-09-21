namespace Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models
{
    // Pedido de EPP generado por Logística/SSOMA para un proyecto — código correlativo tipo
    // "PED-EPP-2026-0001", generado luego del insert (usa el Id) para que sea irrepetible sin
    // depender de una secuencia aparte.
    public class SsEppPedido
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = null!;
        public int ProjectId { get; set; }
        public int? GeneradoPorId { get; set; }
        public DateTimeOffset Fecha { get; set; }
        /// <summary>"Generado" — únicamente informativo por ahora, sin flujo de aprobación.</summary>
        public string Estado { get; set; } = "Generado";
        public string? Observaciones { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public ICollection<SsEppPedidoLinea> Lineas { get; set; } = new List<SsEppPedidoLinea>();
    }

    // Snapshot de la línea al momento del pedido: si el catálogo cambia después (renombran el
    // ítem, dan de baja el modelo), el pedido histórico no debe cambiar retroactivamente.
    public class SsEppPedidoLinea
    {
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public int? EppItemId { get; set; }
        public int? EppModeloId { get; set; }
        public string NombreTecnico { get; set; } = null!;
        public string NombreComercial { get; set; } = null!;
        public string? Marca { get; set; }
        public string? Modelo { get; set; }
        public string? Talla { get; set; }
        public int Cantidad { get; set; }

        public SsEppPedido? Pedido { get; set; }
    }
}
