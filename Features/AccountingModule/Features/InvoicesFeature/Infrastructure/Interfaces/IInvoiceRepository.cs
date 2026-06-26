using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Application.Dtos;

namespace Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Interfaces
{
    public interface IInvoiceRepository
    {
        Task<List<InvoiceSupplierDto>> GetSuppliers();
        Task<List<InvoicePaymentFormDto>> GetPaymentForms();
        /// <summary>Razones sociales que maneja Abril (contribuyentes con es_abril = true).</summary>
        Task<List<InvoiceSupplierDto>> GetAbrilCompanies();
        /// <summary>Carpetas de OneDrive configuradas (para el desplegable "Carpeta a guardar").</summary>
        Task<List<InvoiceFolderOptionDto>> GetFolderOptions();
        /// <summary>Monedas activas (reusa la tabla currency de Adjudicaciones).</summary>
        Task<List<InvoiceCurrencyDto>> GetCurrencies();
        Task<PagedResult<InvoiceDto>> GetPaged(InvoiceFilterDto filter);
        Task<InvoiceDashboardDto> GetDashboard(InvoiceFilterDto filter);
        /// <summary>Detalle completo de una factura. Null si no existe.</summary>
        Task<InvoiceDetailDto?> GetDetail(int invoiceId);
        /// <summary>Actualiza una factura. documentUrl null = conservar el documento actual.</summary>
        Task Update(InvoiceUpdateDto dto, string? documentUrl, int userId);

        /// <summary>Destino (driveId+folderId) de una carpeta de facturas activa. Null si no existe.</summary>
        Task<(string DriveId, string FolderId)?> GetFolderDestination(int invoiceFolderId);
        /// <summary>Nombre/razón social de un contribuyente. Null si no existe.</summary>
        Task<string?> GetContributorName(int contributorId);
        /// <summary>Nombre de una razón social de Abril (es_abril = true). Null si no existe o no es de Abril.</summary>
        Task<string?> GetAbrilContributorName(int abrilContributorId);

        Task Create(InvoiceCreateDto dto, string? documentUrl, int userId);
        Task<InvoiceImportResultDto> ImportInvoices(List<InvoiceImportRowDto> rows, Dictionary<int, string?> docUrlByIndex, int userId);

        /// <summary>Crea un contribuyente/proveedor. Lanza AbrilException si el RUC ya existe.</summary>
        Task<InvoiceSupplierDto> CreateSupplier(InvoiceSupplierCreateDto dto, int userId);
    }
}
