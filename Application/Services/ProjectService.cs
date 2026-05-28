using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Options;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Application.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IProjectRepository _repository;
        private readonly IGraphSharePointService _sharePoint;
        private readonly SharePointSiteRef _site;

        private const string Library   = "Fotos de Proyectos";
        private const string FolderPath = "fotos-proyectos";

        public ProjectService(
            IProjectRepository repository,
            IGraphSharePointService sharePoint,
            IConfiguration configuration)
        {
            _repository = repository;
            _sharePoint = sharePoint;
            _site = SharePointSiteRef.FromConfig(configuration, "ProyectosAbril");
        }

        /*public async Task<List<ProjectScheduleSimpleDTO>> GetWithResidentByUserId(int userId)
        {
            var registros = await _repository.GetWithResidentByUserId(userId);
            return registros;
        }*/
        public async Task<PagedResult<ProjectDTO>> GetPagedWithResidents(int page, string? search = null)
        {
            return await _repository.GetPagedWithResidents(page, search);
        }

        public async Task<PagedResult<ProjectDTO>> GetPaged(int page, bool? activo = null)
        {
            return await _repository.GetPaged(page, activo);
        }

        public async Task<string> UploadFotoAsync(int projectId, IFormFile foto)
        {
            var extension = Path.GetExtension(foto.FileName).TrimStart('.');
            var fileName  = $"proyecto-{projectId}.{extension}";
            var contentType = foto.ContentType ?? "application/octet-stream";

            using var stream = foto.OpenReadStream();
            var result = await _sharePoint.UploadToSharePointLibraryAsync(
                site:        _site,
                libraryName: Library,
                folderPath:  FolderPath,
                fileName:    fileName,
                fileStream:  stream,
                contentType: contentType);

            var fotoUrl = result?.WebUrl
                ?? throw new InvalidOperationException("SharePoint no devolvió una URL para la foto.");

            await _repository.UpdateFotoUrlAsync(projectId, fotoUrl);
            return fotoUrl;
        }
    }
}