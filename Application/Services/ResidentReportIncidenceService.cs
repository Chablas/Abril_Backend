using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;

namespace Abril_Backend.Application.Services
{
    public class ResidentReportIncidenceService : IResidentReportIncidenceService
    {
        private readonly IResidentReportIncidenceRepository _repository;
        private readonly IStorageContainerResolver _containerResolver;
        private readonly IFileStorageService _fileStorageService;
        public ResidentReportIncidenceService(
            IResidentReportIncidenceRepository repository,
            IStorageContainerResolver containerResolver,
            IFileStorageService fileStorageService
            )
        {
            _containerResolver = containerResolver;
            _fileStorageService = fileStorageService;
            _repository = repository;
        }
        public async Task<PagedResult<ResidentReportIncidenceDTO>> GetPaged(int page)
        {
            return await _repository.GetPaged(page);
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
            await _repository.CreateResponse(dto, userId);
        }
    }
}