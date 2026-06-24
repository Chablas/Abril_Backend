namespace Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Dtos
{
    /// <summary>Fila de la tabla de facturas.</summary>
    public class InvoiceDto
    {
        public int InvoiceId { get; set; }
        public DateOnly IssueDate { get; set; }
        public string InvoiceNumber { get; set; } = null!;
        public int ContributorId { get; set; }
        public string ContributorRuc { get; set; } = null!;
        public string ContributorName { get; set; } = null!;
        public string Description { get; set; } = null!;
        public int InvoicePaymentFormId { get; set; }
        public string InvoicePaymentFormDescription { get; set; } = null!;
        public decimal Total { get; set; }
        public string? DocumentUrl { get; set; }
        public DateTime CreatedDateTime { get; set; }
    }

    /// <summary>Proveedor (contribuyente) para el desplegable del formulario.</summary>
    public class InvoiceSupplierDto
    {
        public int ContributorId { get; set; }
        public string ContributorRuc { get; set; } = null!;
        public string ContributorName { get; set; } = null!;
    }

    /// <summary>Forma de pago para el desplegable del formulario.</summary>
    public class InvoicePaymentFormDto
    {
        public int InvoicePaymentFormId { get; set; }
        public string InvoicePaymentFormDescription { get; set; } = null!;
    }

    /// <summary>Carga inicial de la pantalla: datos de filtros/desplegables + primera página de la tabla.</summary>
    public class InvoiceInitDto
    {
        public List<InvoiceSupplierDto> Suppliers { get; set; } = new();
        public List<InvoicePaymentFormDto> PaymentForms { get; set; } = new();
        public Abril_Backend.Application.DTOs.PagedResult<InvoiceDto> Invoices { get; set; } = new();
    }

    /// <summary>Filtros de búsqueda de la tabla.</summary>
    public class InvoiceFilterDto
    {
        public string? Search { get; set; }
        public int? ContributorId { get; set; }
        public int Page { get; set; } = 1;
    }

    /// <summary>Datos de creación de una factura (se reciben como multipart/form-data).</summary>
    public class InvoiceCreateDto
    {
        public DateOnly IssueDate { get; set; }
        public string InvoiceNumber { get; set; } = null!;
        public int ContributorId { get; set; }
        public string Description { get; set; } = null!;
        public int InvoicePaymentFormId { get; set; }
        public decimal Total { get; set; }
        public IFormFile? DocumentFile { get; set; }
    }

    /// <summary>Alta de un nuevo proveedor desde el modal de consulta RUC.</summary>
    public class InvoiceSupplierCreateDto
    {
        public string ContributorRuc { get; set; } = null!;
        public string ContributorName { get; set; } = null!;
        public string ContributorAddress { get; set; } = null!;
        public string? ContributorEconomicActivityDescription { get; set; }
        public string? ContributorDistrict { get; set; }
        public string? ContributorProvince { get; set; }
        public string? ContributorDepartment { get; set; }
    }
}
