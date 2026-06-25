using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Infrastructure.Models;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Models;

namespace Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Models
{
    /// <summary>Factura registrada en el módulo de Contabilidad.</summary>
    public class Invoice
    {
        public int InvoiceId { get; set; }
        public DateOnly IssueDate { get; set; }
        /// <summary>Serie del comprobante (4 caracteres, empieza con 'F'). Ej. "F001".</summary>
        public string Serie { get; set; } = null!;
        /// <summary>Correlativo del comprobante (numérico, hasta 8 dígitos). Ej. "00001234".</summary>
        public string Correlativo { get; set; } = null!;
        /// <summary>Número completo legacy (serie-correlativo). Se conserva por auditoría.</summary>
        public string? InvoiceNumber { get; set; }
        public int ContributorId { get; set; }
        public string Description { get; set; } = null!;
        public int InvoicePaymentFormId { get; set; }
        public decimal Total { get; set; }
        /// <summary>Moneda del total (reusa la tabla currency de Adjudicaciones).</summary>
        public int? CurrencyId { get; set; }
        /// <summary>URL del documento/factura (OneDrive/SharePoint o, en pruebas, Azure Blob).</summary>
        public string? DocumentUrl { get; set; }
        /// <summary>Carpeta de OneDrive donde se guardó el documento.</summary>
        public int? InvoiceFolderId { get; set; }
        /// <summary>Razón social de Abril (contribuyente con es_abril = true) a la que pertenece la factura.</summary>
        public int? AbrilContributorId { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }

        public Contributor? Contributor { get; set; }
        public InvoicePaymentForm? InvoicePaymentForm { get; set; }
        public InvoiceFolder? InvoiceFolder { get; set; }
        public Contributor? AbrilContributor { get; set; }
        public Currency? Currency { get; set; }
    }
}
