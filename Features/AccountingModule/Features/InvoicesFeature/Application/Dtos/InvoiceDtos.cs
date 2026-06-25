using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Application.Dtos;

namespace Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Dtos
{
    /// <summary>Fila de la tabla de facturas.</summary>
    public class InvoiceDto
    {
        public int InvoiceId { get; set; }
        public DateOnly IssueDate { get; set; }
        public string Serie { get; set; } = null!;
        public string Correlativo { get; set; } = null!;
        /// <summary>Número completo para mostrar (serie-correlativo).</summary>
        public string InvoiceNumber => $"{Serie}-{Correlativo}";
        public int ContributorId { get; set; }
        public string ContributorRuc { get; set; } = null!;
        public string ContributorName { get; set; } = null!;
        public int? AbrilContributorId { get; set; }
        public string? AbrilContributorName { get; set; }
        public string Description { get; set; } = null!;
        public int InvoicePaymentFormId { get; set; }
        public string InvoicePaymentFormDescription { get; set; } = null!;
        public decimal Total { get; set; }
        public int? CurrencyId { get; set; }
        public string? CurrencyCode { get; set; }
        public string? CurrencySymbol { get; set; }
        public string? DocumentUrl { get; set; }
        public DateTime CreatedDateTime { get; set; }
    }

    /// <summary>Detalle completo de una factura (modal de ver/editar).</summary>
    public class InvoiceDetailDto
    {
        public int InvoiceId { get; set; }
        public DateOnly IssueDate { get; set; }
        public string Serie { get; set; } = null!;
        public string Correlativo { get; set; } = null!;
        public string InvoiceNumber => $"{Serie}-{Correlativo}";
        public int ContributorId { get; set; }
        public string ContributorRuc { get; set; } = null!;
        public string ContributorName { get; set; } = null!;
        public int? AbrilContributorId { get; set; }
        public string? AbrilContributorName { get; set; }
        public string? AbrilContributorRuc { get; set; }
        public string Description { get; set; } = null!;
        public int InvoicePaymentFormId { get; set; }
        public string InvoicePaymentFormDescription { get; set; } = null!;
        public decimal Total { get; set; }
        public int? CurrencyId { get; set; }
        public string? CurrencyCode { get; set; }
        public string? CurrencySymbol { get; set; }
        public int? InvoiceFolderId { get; set; }
        public string? InvoiceFolderName { get; set; }
        public string? DocumentUrl { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
    }

    /// <summary>Moneda para el desplegable del formulario (reusa la tabla currency).</summary>
    public class InvoiceCurrencyDto
    {
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; } = null!;
        public string CurrencyDescription { get; set; } = null!;
        public string? CurrencySymbol { get; set; }
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
        /// <summary>Razones sociales que maneja Abril (contribuyentes con es_abril = true).</summary>
        public List<InvoiceSupplierDto> AbrilCompanies { get; set; } = new();
        /// <summary>Carpetas de OneDrive configuradas para guardar facturas.</summary>
        public List<InvoiceFolderOptionDto> Folders { get; set; } = new();
        public List<InvoiceCurrencyDto> Currencies { get; set; } = new();
        public Abril_Backend.Application.DTOs.PagedResult<InvoiceDto> Invoices { get; set; } = new();
    }

    /// <summary>Filtros de búsqueda de la tabla.</summary>
    public class InvoiceFilterDto
    {
        public string? Search { get; set; }
        public string? Serie { get; set; }
        public string? Correlativo { get; set; }
        /// <summary>Razón social del proveedor (contribuyente).</summary>
        public int? ContributorId { get; set; }
        /// <summary>RUC del proveedor.</summary>
        public string? ContributorRuc { get; set; }
        /// <summary>Razón social de Abril (es_abril = true).</summary>
        public int? AbrilContributorId { get; set; }
        /// <summary>RUC de la razón social de Abril.</summary>
        public string? AbrilContributorRuc { get; set; }
        public int? InvoicePaymentFormId { get; set; }
        public decimal? TotalMin { get; set; }
        public decimal? TotalMax { get; set; }
        public DateOnly? IssueDateFrom { get; set; }
        public DateOnly? IssueDateTo { get; set; }
        public int Page { get; set; } = 1;
    }

    /// <summary>Datos de creación de una factura (se reciben como multipart/form-data).</summary>
    public class InvoiceCreateDto
    {
        public DateOnly IssueDate { get; set; }
        public string Serie { get; set; } = null!;
        public string Correlativo { get; set; } = null!;
        public int ContributorId { get; set; }
        public string Description { get; set; } = null!;
        public int InvoicePaymentFormId { get; set; }
        public decimal Total { get; set; }
        public int CurrencyId { get; set; }
        /// <summary>Carpeta de OneDrive donde se guardará el documento.</summary>
        public int InvoiceFolderId { get; set; }
        /// <summary>Razón social de Abril (es_abril = true) a la que pertenece la factura.</summary>
        public int AbrilContributorId { get; set; }
        public IFormFile? DocumentFile { get; set; }
    }

    /// <summary>Datos de edición de una factura (multipart/form-data; documento opcional).</summary>
    public class InvoiceUpdateDto
    {
        public int InvoiceId { get; set; }
        public DateOnly IssueDate { get; set; }
        public string Serie { get; set; } = null!;
        public string Correlativo { get; set; } = null!;
        public int ContributorId { get; set; }
        public string Description { get; set; } = null!;
        public int InvoicePaymentFormId { get; set; }
        public decimal Total { get; set; }
        public int CurrencyId { get; set; }
        public int InvoiceFolderId { get; set; }
        public int AbrilContributorId { get; set; }
        /// <summary>Si se adjunta, reemplaza el documento (se vuelve a subir a OneDrive).</summary>
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
