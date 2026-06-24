using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Dtos;

namespace Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Interfaces
{
    public interface IInvoiceRepository
    {
        Task<List<InvoiceSupplierDto>> GetSuppliers();
        Task<List<InvoicePaymentFormDto>> GetPaymentForms();
        Task<PagedResult<InvoiceDto>> GetPaged(InvoiceFilterDto filter);
        Task Create(InvoiceCreateDto dto, string? documentUrl, int userId);

        /// <summary>Crea un contribuyente/proveedor. Lanza AbrilException si el RUC ya existe.</summary>
        Task<InvoiceSupplierDto> CreateSupplier(InvoiceSupplierCreateDto dto, int userId);
    }
}
