using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Dtos;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Services
{
    public class ControlVencimientosService : IControlVencimientosService
    {
        private const long MaxBytes = 15 * 1024 * 1024;
        private static readonly string[] AllowedExtensions =
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".webp" };

        private readonly IControlVencimientosRepository _repository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;

        public ControlVencimientosService(
            IControlVencimientosRepository repository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver)
        {
            _repository = repository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
        }

        public Task<List<VecinoLicenciaDto>> GetLicencias() => _repository.GetLicencias();

        public async Task<VecinoLicenciaDto> CreateLicencia(VecinoLicenciaCreateDto dto, IFormFile file, int userId)
        {
            if (file == null || file.Length == 0)
                throw new AbrilException("No se adjuntó ningún archivo.", 400);
            if (file.Length > MaxBytes)
                throw new AbrilException("El archivo supera el tamaño máximo permitido (15 MB).", 400);

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                throw new AbrilException("Formato no válido. Use PDF, Word, Excel o imagen.", 400);

            if (dto.FechaRecordatorio > dto.FechaVencimiento)
                throw new AbrilException("La fecha de recordatorio no puede ser posterior a la fecha de vencimiento.", 400);

            var dias = dto.FechaVencimiento.DayNumber - dto.FechaRecordatorio.DayNumber;
            if (dias < 0)
                throw new AbrilException("Los días de antelación no pueden ser negativos.", 400);
            // Se recalcula desde las fechas para garantizar consistencia con lo enviado.
            dto.DiasAntes = dias;

            var container = _containerResolver.GetVecinoEntregablesContainerName();

            string archivoUrl;
            using (var stream = file.OpenReadStream())
            {
                var uploaded = await _fileStorageService.UploadFilesAsync(
                    new[] { (stream, $"{Guid.NewGuid()}{extension}") },
                    container);
                archivoUrl = uploaded.First();
            }

            return await _repository.CreateLicencia(dto, archivoUrl, file.FileName, userId);
        }
    }
}
