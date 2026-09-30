using Microsoft.Extensions.Configuration;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Options;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Services
{
    /// <summary>Configuración → Carpeta de Contratos: una carpeta raíz de SharePoint por proyecto,
    /// pegada como link por el usuario y resuelta a driveId/itemId estable (misma mecánica que
    /// ActasReunionService.SaveFolder, pero por proyecto en vez de un singleton global).</summary>
    public class ProjectContractFolderService : IProjectContractFolderService
    {
        private readonly IProjectContractFolderRepository _repository;
        private readonly IGraphSharePointService _sharePointService;
        private readonly string[] _allowedHosts;

        public ProjectContractFolderService(
            IProjectContractFolderRepository repository,
            IGraphSharePointService sharePointService,
            IConfiguration configuration)
        {
            _repository = repository;
            _sharePointService = sharePointService;

            // Mismo criterio que ActasReunionService: solo se aceptan links del tenant de
            // SharePoint de la organización (no cualquier URL externa).
            var siteHost = SharePointSiteRef.FromConfig(configuration, "CostosYPresupuestos").Hostname.ToLowerInvariant();
            var tenant = siteHost.Split('.')[0].Replace("-my", "");
            _allowedHosts = new[] { $"{tenant}.sharepoint.com", $"{tenant}-my.sharepoint.com" };
        }

        public Task<ProjectContractFolderDTO?> GetByProjectIdAsync(int projectId)
            => _repository.GetByProjectIdAsync(projectId);

        public async Task<ProjectContractFolderDTO> SaveAsync(int projectId, ProjectContractFolderSaveDTO dto, int userId)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.LinkUrl))
                throw new AbrilException("Debe ingresar el link de la carpeta.", 400);

            var link = dto.LinkUrl.Trim();

            if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new AbrilException("El link no es una URL válida.", 400);

            if (!_allowedHosts.Contains(uri.Host.ToLowerInvariant()))
                throw new AbrilException(
                    $"El link no pertenece a la organización. Solo se permiten enlaces de: {string.Join(", ", _allowedHosts)}.", 400);

            var resolved = await _sharePointService.ResolveSharePointFolderUrlAsync(link)
                ?? throw new AbrilException(
                    "No se pudo acceder a la carpeta del link. Verifique que el enlace apunte a una carpeta/biblioteca y que la aplicación tenga acceso.", 422);

            if (!resolved.IsFolder)
                throw new AbrilException("El link debe apuntar a una carpeta, no a un archivo.", 400);

            await _repository.UpsertAsync(
                projectId, link, resolved.DriveId, resolved.ItemId, resolved.Name, resolved.WebUrl, userId);

            return await _repository.GetByProjectIdAsync(projectId)
                ?? throw new AbrilException("No se pudo guardar la carpeta.", 500);
        }
    }
}
