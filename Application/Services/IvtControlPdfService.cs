using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Application.Services
{
    public class IvtControlPdfService : IIvtControlPdfService
    {
        private readonly IIvtControlPdfRepository _repository;
        private readonly IStorageContainerResolver _containerResolver;
        private readonly IFileStorageService _fileStorageService;
        public IvtControlPdfService(IIvtControlPdfRepository repository, IStorageContainerResolver containerResolver, IFileStorageService fileStorageService)
        {
            _containerResolver = containerResolver;
            _repository = repository;
            _fileStorageService = fileStorageService;
        }

        public async Task<bool> Create(IvtControlPdfCreateDTO dto, int userId)
        {
            if (dto.Pdf.Length == 0)
                throw new Exception();

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.Pdf.FileName)}";

            string fileUrl;

            using (var stream = dto.Pdf.OpenReadStream())
            {
                var container = _containerResolver.GetIvtContainerName();
                fileUrl = await _fileStorageService.UploadFileAsync(stream, fileName, container);
            }

            await _repository.Create(dto.ScheduleId, fileUrl, userId, dto.Pdf.FileName);
            return true;
        }

        public async Task<PagedResult<IvtControlPdfGetDTO>> GetPaged(int page)
        {
            var resultado = await _repository.GetPaged(page);
            return resultado;
        }
    }
}