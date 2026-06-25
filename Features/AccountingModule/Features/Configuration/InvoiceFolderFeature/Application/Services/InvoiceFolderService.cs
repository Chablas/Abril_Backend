using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Application.Interfaces;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Options;

namespace Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Application.Services
{
    public class InvoiceFolderService : IInvoiceFolderService
    {
        private readonly IInvoiceFolderRepository _repository;
        private readonly IGraphSharePointService _sharePointService;
        private readonly string[] _allowedHosts;

        public InvoiceFolderService(
            IInvoiceFolderRepository repository,
            IGraphSharePointService sharePointService,
            IConfiguration configuration)
        {
            _repository = repository;
            _sharePointService = sharePointService;

            // Hosts permitidos del tenant. Se derivan del sitio ya configurado (mismo tenant
            // para toda la organización): "abrilinmob.sharepoint.com" → tenant "abrilinmob"
            // → se aceptan el de sitios y el de OneDrive personal "-my".
            var siteHost = SharePointSiteRef.FromConfig(configuration, "CostosYPresupuestos").Hostname.ToLowerInvariant();
            var tenant = siteHost.Split('.')[0].Replace("-my", "");
            _allowedHosts = new[] { $"{tenant}.sharepoint.com", $"{tenant}-my.sharepoint.com" };
        }

        public Task<PagedResult<InvoiceFolderDto>> GetPaged(InvoiceFolderFilterDto filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return _repository.GetPaged(filter);
        }

        public async Task<FolderBrowseDto> ResolveLink(string linkUrl)
        {
            var resolved = await ResolveAndValidateAsync(linkUrl);
            var children = await _sharePointService.GetChildFoldersByItemIdAsync(resolved.DriveId, resolved.ItemId);

            return new FolderBrowseDto
            {
                DriveId    = resolved.DriveId,
                FolderId   = resolved.ItemId,
                FolderName = resolved.Name,
                Folders    = children.Select(c => new FolderItemDto { Id = c.ItemId, Name = c.Name ?? "" }).ToList(),
            };
        }

        public async Task<List<FolderItemDto>> GetChildFolders(string driveId, string folderId)
        {
            if (string.IsNullOrWhiteSpace(driveId) || string.IsNullOrWhiteSpace(folderId))
                throw new AbrilException("Faltan datos de la carpeta.");

            var children = await _sharePointService.GetChildFoldersByItemIdAsync(driveId, folderId);
            return children.Select(c => new FolderItemDto { Id = c.ItemId, Name = c.Name ?? "" }).ToList();
        }

        public async Task Create(InvoiceFolderCreateDto dto, int userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new AbrilException("Debe ingresar un nombre para la carpeta.");

            if (await _repository.NameExistsAsync(dto.Name))
                throw new AbrilException("Ya existe una carpeta con ese nombre.");

            var folder = await ValidateChosenFolderAsync(dto.LinkUrl, dto.DriveId, dto.FolderId);

            await _repository.Create(dto, folder.DriveId, folder.ItemId, folder.Name, folder.WebUrl, userId);
        }

        public async Task Update(InvoiceFolderUpdateDto dto, int userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new AbrilException("Debe ingresar un nombre para la carpeta.");

            if (!await _repository.ExistsAsync(dto.InvoiceFolderId))
                throw new AbrilException("La carpeta no existe.");

            if (await _repository.NameExistsAsync(dto.Name, dto.InvoiceFolderId))
                throw new AbrilException("Ya existe otra carpeta con ese nombre.");

            var folder = await ValidateChosenFolderAsync(dto.LinkUrl, dto.DriveId, dto.FolderId);

            await _repository.Update(dto, folder.DriveId, folder.ItemId, folder.Name, folder.WebUrl, userId);
        }

        public Task<bool> Delete(int invoiceFolderId, int userId)
            => _repository.Delete(invoiceFolderId, userId);

        /// <summary>
        /// Valida que el link pertenezca al tenant y lo resuelve a driveId + folderId.
        /// Exige que apunte a una carpeta (no a un archivo).
        /// </summary>
        private async Task<ShareLinkResolveDto> ResolveAndValidateAsync(string? linkUrl)
        {
            if (string.IsNullOrWhiteSpace(linkUrl))
                throw new AbrilException("Debe ingresar el link de la carpeta.");

            var link = linkUrl.Trim();

            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new AbrilException("El link no es una URL válida.");

            if (!_allowedHosts.Contains(uri.Host.ToLowerInvariant()))
                throw new AbrilException(
                    $"El link no pertenece a la organización. Solo se permiten enlaces de: {string.Join(", ", _allowedHosts)}.");

            var resolved = await _sharePointService.ResolveShareLinkAsync(link)
                ?? throw new AbrilException(
                    "No se pudo acceder a la carpeta del link. Verifique que el enlace sea correcto y que la aplicación tenga acceso.");

            if (!resolved.IsFolder)
                throw new AbrilException("El link debe apuntar a una carpeta, no a un archivo.");

            return resolved;
        }

        /// <summary>
        /// Valida que la carpeta elegida (driveId+folderId) esté dentro del drive del link validado
        /// y que exista y sea una carpeta. Devuelve sus datos para persistir.
        /// </summary>
        private async Task<ShareLinkResolveDto> ValidateChosenFolderAsync(string? linkUrl, string? driveId, string? folderId)
        {
            var resolvedLink = await ResolveAndValidateAsync(linkUrl);

            if (string.IsNullOrWhiteSpace(driveId) || string.IsNullOrWhiteSpace(folderId))
                throw new AbrilException("Debe seleccionar una carpeta.");

            if (!string.Equals(driveId, resolvedLink.DriveId, StringComparison.Ordinal))
                throw new AbrilException("La carpeta seleccionada no corresponde al link indicado.");

            var folder = await _sharePointService.GetDriveItemAsync(driveId!, folderId!)
                ?? throw new AbrilException("La carpeta seleccionada no existe o no es accesible.");

            if (!folder.IsFolder)
                throw new AbrilException("El elemento seleccionado no es una carpeta.");

            return folder;
        }
    }
}
