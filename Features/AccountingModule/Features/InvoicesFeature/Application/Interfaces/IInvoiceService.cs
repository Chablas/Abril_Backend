using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Dtos;
using Abril_Backend.Shared.Services.Sunat.Dtos;

namespace Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Interfaces
{
    public interface IInvoiceService
    {
        Task<InvoiceInitDto> GetInit(InvoiceFilterDto filter);
        Task<PagedResult<InvoiceDto>> GetPaged(InvoiceFilterDto filter);
        Task Create(InvoiceCreateDto dto, int userId);
        Task<InvoiceSupplierDto> CreateSupplier(InvoiceSupplierCreateDto dto, int userId);
        Task<SunatContributorDto?> GetByRuc(string ruc);
    }
}
