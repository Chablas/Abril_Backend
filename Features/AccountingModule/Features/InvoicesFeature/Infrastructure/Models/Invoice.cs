using Abril_Backend.Features.CostsModule.Shared.Models;

namespace Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Models
{
    /// <summary>Factura registrada en el módulo de Contabilidad.</summary>
    public class Invoice
    {
        public int InvoiceId { get; set; }
        public DateOnly IssueDate { get; set; }
        public string InvoiceNumber { get; set; } = null!;
        public int ContributorId { get; set; }
        public string Description { get; set; } = null!;
        public int InvoicePaymentFormId { get; set; }
        public decimal Total { get; set; }
        /// <summary>URL del documento/factura. Por ahora apunta a Azure Blob; podrá migrar de ubicación.</summary>
        public string? DocumentUrl { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }

        public Contributor? Contributor { get; set; }
        public InvoicePaymentForm? InvoicePaymentForm { get; set; }
    }
}
