using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Shared.Services.Residentes.Interfaces;

namespace Abril_Backend.Application.Services
{
    public class ResidentReportIncidenceService : IResidentReportIncidenceService
    {
        private readonly IResidentReportIncidenceRepository _repository;
        private readonly IResidenteProyectoResolver _residentes;
        private readonly IStorageContainerResolver _containerResolver;
        private readonly IFileStorageService _fileStorageService;
        public ResidentReportIncidenceService(
            IResidentReportIncidenceRepository repository,
            IResidenteProyectoResolver residentes,
            IStorageContainerResolver containerResolver,
            IFileStorageService fileStorageService
            )
        {
            _containerResolver = containerResolver;
            _fileStorageService = fileStorageService;
            _repository = repository;
            _residentes = residentes;
        }

        /// <summary>El RESIDENTE ve las incidencias de las obras donde es el residente de
        /// Configuración → Proyectos (ya no la tabla antigua project_resident).</summary>
        public async Task<PagedResult<ResidentReportIncidenceDTO>> GetPaged(int page, int userId, bool isResidente, int? projectId = null, int? stateId = null)
        {
            List<int>? allowedProjectIds = null;

            if (isResidente)
            {
                var assignedProjects = await _repository.GetProyectosDelResidente(userId);
                allowedProjectIds = assignedProjects.Select(p => p.ProjectId).ToList();

                if (projectId.HasValue && !allowedProjectIds.Contains(projectId.Value))
                    projectId = null;
            }

            return await _repository.GetPaged(page, projectId, stateId, allowedProjectIds);
        }

        public async Task<List<ProjectSimpleDTO>> GetAssignedProjects(int userId, bool isResidente)
        {
            if (!isResidente)
                return new List<ProjectSimpleDTO>();

            return await _repository.GetProyectosDelResidente(userId);
        }
        public async Task Create(ResidentReportIncidenceCreateDTO dto, int userId)
        {
            if (dto.Images == null || !dto.Images.Any())
                throw new AbrilException("No se pusieron archivos.");

            if (dto.Images.Count() > 3)
                throw new AbrilException("Máximo 3 archivos por subida");

            var container = _containerResolver.GetResidentIncidentContainerName();

            var filesToUpload = new List<(Stream Stream, string FileName)>();
            var streams = new List<Stream>();

            foreach (var image in dto.Images)
            {
                if (image.Length == 0)
                    throw new AbrilException("Empty file detected.");

                var extension = Path.GetExtension(image.FileName);

                var fileName = $"{Guid.NewGuid()}{extension}";

                var stream = image.OpenReadStream();

                streams.Add(stream);
                filesToUpload.Add((stream, fileName));
            }

            List<string> uploadedUrls;

            try
            {
                uploadedUrls = await _fileStorageService.UploadFilesAsync(filesToUpload, container);

                await _repository.Create(dto, uploadedUrls, userId);
            }
            finally
            {
                foreach (var stream in streams)
                    stream.Dispose();
            }
        }

        public async Task CreateResponse(ResidentReportResponseCreateDTO dto, int userId)
        {
            var projectId = await _repository.GetProjectId(dto.ResidentReportIncidenceId);

            if (projectId == null)
                throw new AbrilException("Incidencia no encontrada.", 404);

            // El rol RESIDENTE lo exige el controller; acá, que sea el residente de ESE proyecto.
            var isAssignedResident = await _residentes.EsResidenteDelProyectoAsync(userId, projectId.Value);

            if (!isAssignedResident)
                throw new AbrilException("No estás asignado como residente de este proyecto.", 403);

            if (dto.Images == null || !dto.Images.Any())
                throw new AbrilException("No se pusieron archivos.");

            if (dto.Images.Count() > 3)
                throw new AbrilException("Máximo 3 archivos por subida");

            var container = _containerResolver.GetResidentIncidentContainerName();

            var filesToUpload = new List<(Stream Stream, string FileName)>();
            var streams = new List<Stream>();

            foreach (var image in dto.Images)
            {
                if (image.Length == 0)
                    throw new AbrilException("Empty file detected.");

                var extension = Path.GetExtension(image.FileName);

                var fileName = $"{Guid.NewGuid()}{extension}";

                var stream = image.OpenReadStream();

                streams.Add(stream);
                filesToUpload.Add((stream, fileName));
            }

            List<string> uploadedUrls;

            try
            {
                uploadedUrls = await _fileStorageService.UploadFilesAsync(filesToUpload, container);
                await _repository.CreateResponse(dto, uploadedUrls, userId);
            }
            finally
            {
                foreach (var stream in streams)
                    stream.Dispose();
            }
        }

        public async Task UpdateIncidenceState(UpdateIncidenceDTO incidenceId, int userId)
        {
            await _repository.UpdateIncidenceState(incidenceId, userId);
        }
    }
}