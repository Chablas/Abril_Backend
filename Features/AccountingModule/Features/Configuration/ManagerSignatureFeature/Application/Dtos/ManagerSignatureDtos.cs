namespace Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Dtos
{
    /// <summary>Firma del usuario actual. La imagen se entrega como data URL para mostrarla directo.</summary>
    public class ManagerSignatureDto
    {
        /// <summary>data:image/png;base64,… para usar directamente en un &lt;img src&gt;.</summary>
        public string ImageDataUrl { get; set; } = null!;
        public DateTime? UpdatedDateTime { get; set; }
    }

    /// <summary>Datos para guardar la firma: el data URL PNG exportado por el canvas.</summary>
    public class ManagerSignatureSaveDto
    {
        /// <summary>data:image/png;base64,… (o solo el base64) generado con canvas.toDataURL('image/png').</summary>
        public string ImageBase64 { get; set; } = null!;
    }
}
