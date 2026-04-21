using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Interfaces;
using Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.Sunat.Dtos;
using Abril_Backend.Shared.Services.Sunat.Interfaces;

namespace Abril_Backend.Features.Contractors.ContractorRegistration.Application.Services
{
    public class ContractorRegistrationService : IContractorRegistrationService
    {
        private readonly IContractorRegistrationRepository _repository;
        private readonly ISunatService _sunatService;
        private readonly IGraphSharePointService _sharePointService;
        private readonly IConfiguration _configuration;

        public ContractorRegistrationService(
            IContractorRegistrationRepository repository,
            ISunatService sunatService,
            IGraphSharePointService sharePointService,
            IConfiguration configuration)
        {
            _repository = repository;
            _sunatService = sunatService;
            _sharePointService = sharePointService;
            _configuration = configuration;
        }

        public async Task Create(ContributorCreateDto dto, int? userId, string? accessToken = null)
        {
            string? brochureUrl = null;
            string? fichaRucUrl = null;
            string? referencesUrl = null;

            // Subir archivos a SharePoint usando permisos de aplicación (no requiere token del usuario).
            // Carpeta: {ruc} - {razonsocial}  (dentro de la biblioteca de contratistas)
            var listId     = _configuration["SharePoint:ContractorListId"]
                             ?? throw new InvalidOperationException("SharePoint:ContractorListId no está configurado.");
            var folderPath = Sanitize($"{dto.ContributorRuc} - {dto.ContributorName}");

            if (dto.BrochureFile is not null)
                brochureUrl = await UploadFile(listId, folderPath, "brochure", dto.BrochureFile);

            if (dto.FichaRucFile is not null)
                fichaRucUrl = await UploadFile(listId, folderPath, "ficha_ruc", dto.FichaRucFile);

            if (dto.ReferencesListFile is not null)
                referencesUrl = await UploadFile(listId, folderPath, "lista_referencias", dto.ReferencesListFile);

            await _repository.Create(dto, userId, brochureUrl, fichaRucUrl, referencesUrl);
        }

        public async Task<SunatContributorDto?> GetByRuc(string ruc)
        {
            return await _sunatService.GetByRucAsync(ruc);
        }

        private async Task<string?> UploadFile(string listId, string folderPath, string baseName, IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName);
            var fileName  = $"{baseName}{extension}";

            using var stream = file.OpenReadStream();
            return await _sharePointService.UploadToSharePointLibraryAsync(
                libraryName: listId,
                folderPath:  folderPath,
                fileName:    fileName,
                fileStream:  stream,
                contentType: file.ContentType);
        }

        /// <summary>Elimina caracteres no permitidos en nombres de carpeta de SharePoint.</summary>
        private static string Sanitize(string name)
        {
            var invalid = new HashSet<char> { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '#', '%' };
            var result  = string.Concat(name.Select(c => invalid.Contains(c) ? '-' : c)).Trim();
            return result.Length > 60 ? result[..60].TrimEnd() : result;
        }
    }
}
