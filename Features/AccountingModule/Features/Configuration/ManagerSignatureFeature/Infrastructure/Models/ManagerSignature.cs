namespace Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Infrastructure.Models
{
    /// <summary>
    /// Firma del Gerente General (singleton). Imagen PNG dibujada en Configuración que se estampa
    /// en los documentos firmados de las facturas. Existe a lo sumo una fila vigente (state = true).
    /// </summary>
    public class ManagerSignature
    {
        public int ManagerSignatureId { get; set; }
        /// <summary>Bytes de la imagen de la firma (PNG con fondo transparente).</summary>
        public byte[] ImageBytes { get; set; } = null!;
        public string Mime { get; set; } = "image/png";
        public DateTimeOffset CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
