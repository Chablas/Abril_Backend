using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Application.Dtos;

namespace Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Application.Interfaces
{
    public interface IInvoiceFolderService
    {
        Task<PagedResult<InvoiceFolderDto>> GetPaged(InvoiceFolderFilterDto filter);
        /// <summary>Valida el link (tenant) y devuelve la carpeta resuelta + sus subcarpetas.</summary>
        Task<FolderBrowseDto> ResolveLink(string linkUrl);
        /// <summary>Lista las subcarpetas de una carpeta (navegación dentro del mismo drive).</summary>
        Task<List<FolderItemDto>> GetChildFolders(string driveId, string folderId);
        Task Create(InvoiceFolderCreateDto dto, int userId);
        Task Update(InvoiceFolderUpdateDto dto, int userId);
        Task<bool> Delete(int invoiceFolderId, int userId);
    }
}
