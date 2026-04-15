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

        public ContractorRegistrationService(
            IContractorRegistrationRepository repository,
            ISunatService sunatService,
            IGraphSharePointService sharePointService)
        {
            _repository = repository;
            _sunatService = sunatService;
            _sharePointService = sharePointService;
        }

        public async Task Create(CompanyCreateDto dto, int? userId, string? accessToken = null)
        {
            string? brochureUrl = null;
            string? fichaRucUrl = null;
            string? referencesUrl = null;

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                // Carpeta: Homologación de Contratistas/{ruc} - {razonsocial}
                // Graph API crea automáticamente las carpetas intermedias si no existen
                var folderPath = $"Homologación de Contratistas/{dto.CompanyRuc} - {dto.CompanyName}";

                if (dto.BrochureFile is not null)
                    brochureUrl = await UploadFile(accessToken, folderPath, "brochure", dto.BrochureFile);

                if (dto.FichaRucFile is not null)
                    fichaRucUrl = await UploadFile(accessToken, folderPath, "ficha_ruc", dto.FichaRucFile);

                if (dto.ReferencesListFile is not null)
                    referencesUrl = await UploadFile(accessToken, folderPath, "lista_referencias", dto.ReferencesListFile);
            }

            await _repository.Create(dto, userId, brochureUrl, fichaRucUrl, referencesUrl);
        }

        public async Task<SunatCompanyDto?> GetByRuc(string ruc)
        {
            return await _sunatService.GetByRucAsync(ruc);
        }

        private async Task<string?> UploadFile(string accessToken, string folderPath, string baseName, IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{baseName}{extension}";

            using var stream = file.OpenReadStream();
            return await _sharePointService.UploadFileAsync(accessToken, folderPath, fileName, stream, file.ContentType);
        }
    }
}
