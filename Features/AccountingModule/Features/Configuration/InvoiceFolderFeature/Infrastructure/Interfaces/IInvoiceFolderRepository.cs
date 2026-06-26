using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Application.Dtos;

namespace Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Infrastructure.Interfaces
{
    public interface IInvoiceFolderRepository
    {
        Task<PagedResult<InvoiceFolderDto>> GetPaged(InvoiceFolderFilterDto filter);
        Task<bool> NameExistsAsync(string name, int? excludeId = null);
        Task<bool> ExistsAsync(int invoiceFolderId);
        Task Create(InvoiceFolderCreateDto dto, string driveId, string folderId, string? folderName, string? webUrl, int userId);
        Task Update(InvoiceFolderUpdateDto dto, string driveId, string folderId, string? folderName, string? webUrl, int userId);
        Task<bool> Delete(int invoiceFolderId, int userId);
    }
}
