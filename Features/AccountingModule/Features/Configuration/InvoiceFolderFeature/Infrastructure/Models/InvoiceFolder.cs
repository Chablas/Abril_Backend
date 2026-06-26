namespace Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Infrastructure.Models
{
    /// <summary>
    /// Carpeta de OneDrive/SharePoint donde se guardan las facturas. Cada registro tiene un
    /// nombre identificativo (p. ej. "Facturas urgentes", "Valorizaciones") que se elige al
    /// registrar una factura.
    /// </summary>
    public class InvoiceFolder
    {
        public int InvoiceFolderId { get; set; }
        /// <summary>Nombre identificativo de la carpeta mostrado en el desplegable.</summary>
        public string Name { get; set; } = null!;
        /// <summary>Link original pegado por el usuario (para mostrar/reabrir).</summary>
        public string LinkUrl { get; set; } = null!;
        /// <summary>Ubicación estable resuelta vía Graph Shares API.</summary>
        public string DriveId { get; set; } = null!;
        public string FolderId { get; set; } = null!;
        public string? FolderName { get; set; }
        public string? WebUrl { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
