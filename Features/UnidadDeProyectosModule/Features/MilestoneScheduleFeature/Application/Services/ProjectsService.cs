using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Options;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Services
{
    public class ProjectsService : IProjectsService
    {
        private readonly IProjectsRepository _repository;
        private readonly ICronogramaPermisosRepository _permisosRepository;
        private readonly IGraphSharePointService _sharePoint;
        private readonly SharePointSiteRef _site;

        private const string Library   = "Fotos de Proyectos";
        private const string FolderPath = "fotos-proyectos";

        public ProjectsService(
            IProjectsRepository repository,
            ICronogramaPermisosRepository permisosRepository,
            IGraphSharePointService sharePoint,
            IConfiguration configuration)
        {
            _repository = repository;
            _permisosRepository = permisosRepository;
            _sharePoint = sharePoint;
            _site = SharePointSiteRef.FromConfig(configuration, "ProyectosAbril");
        }

        /// <summary>El RESIDENTE solo ve los proyectos donde es el residente de Emails SSOMA,
        /// aunque además tenga un rol de solo lectura (USUARIO DE ABRIL) que ve todos. Quien
        /// administra el cronograma ve todos aunque también tenga el rol RESIDENTE.</summary>
        public async Task<PagedResult<MilestoneProjectDTO>> GetPagedWithResidents(int userId, int[] roleIds, bool esResidente, int page, int pageSize = 10, string? search = null)
        {
            var soloDelResidente = esResidente && !await _permisosRepository.AdministraAsync(roleIds);
            return await _repository.GetPagedWithResidents(userId, soloDelResidente, page, pageSize, search);
        }

        /// <summary>La foto y la característica de la tarjeta las cambia quien administra el
        /// cronograma o el residente del proyecto; el resto las ve en solo lectura.</summary>
        private async Task ValidarEdicionAsync(int projectId, int userId, int[] roleIds, bool esResidente)
        {
            var puedeEditar = await _permisosRepository.PuedeEditarProyectoAsync(userId, roleIds, esResidente, projectId);
            if (!puedeEditar)
                throw new AbrilException("No tienes permiso para modificar este proyecto.", 403);
        }

        public async Task<string> UploadFotoAsync(int projectId, IFormFile foto, int userId, int[] roleIds, bool esResidente)
        {
            await ValidarEdicionAsync(projectId, userId, roleIds, esResidente);

            var extension   = Path.GetExtension(foto.FileName).TrimStart('.');
            var fileName    = $"proyecto-{projectId}.{extension}";
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

            await _repository.UpdateFotoUrlAsync(projectId, fotoUrl, userId);
            return fotoUrl;
        }

        public async Task<string?> UpdateLevelDescriptionAsync(int projectId, string? levelDescription, int userId, int[] roleIds, bool esResidente)
        {
            await ValidarEdicionAsync(projectId, userId, roleIds, esResidente);
            return await _repository.UpdateLevelDescriptionAsync(projectId, levelDescription, userId);
        }
    }
}
