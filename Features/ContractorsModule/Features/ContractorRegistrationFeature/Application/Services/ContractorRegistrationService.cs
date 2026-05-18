using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Interfaces;
using Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.Sunat.Dtos;
using Abril_Backend.Shared.Services.Sunat.Interfaces;
using System.Text;

namespace Abril_Backend.Features.Contractors.ContractorRegistration.Application.Services
{
    public class ContractorRegistrationService : IContractorRegistrationService
    {
        private static readonly List<string> _costoYPresupuestosEmails = new()
        {
            //"eaguinaga@abril.pe",
            //"apimentel@abril.pe",
            //"bquicana@abril.pe",
            //"cavila@abril.pe",
            "alvarezvillegaschristian@gmail.com",
        };

        private readonly IContractorRegistrationRepository _repository;
        private readonly ISunatService _sunatService;
        private readonly IGraphSharePointService _sharePointService;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public ContractorRegistrationService(
            IContractorRegistrationRepository repository,
            ISunatService sunatService,
            IGraphSharePointService sharePointService,
            IConfiguration configuration,
            IEmailService emailService)
        {
            _repository = repository;
            _sunatService = sunatService;
            _sharePointService = sharePointService;
            _configuration = configuration;
            _emailService = emailService;
        }

        public async Task<List<ContractorPersonTypeDto>> GetPersonTypes()
        {
            return await _repository.GetPersonTypes();
        }

        public async Task Create(ContributorCreateDto dto, int? userId, string? accessToken = null)
        {
            string? logoUrl       = null;
            string? brochureUrl   = null;
            string? fichaRucUrl   = null;
            string? referencesUrl = null;

            if (dto.LogoFile is not null || dto.BrochureFile is not null || dto.FichaRucFile is not null || dto.ReferencesListFile is not null)
            {
                var listId     = _configuration["SharePoint:ContractorListId"]
                                 ?? throw new InvalidOperationException("SharePoint:ContractorListId no está configurado.");
                var folderPath = Sanitize($"{dto.ContributorRuc} - {dto.ContributorName}");

                if (dto.LogoFile is not null)
                    logoUrl = await UploadFile(listId, folderPath, "logo", dto.LogoFile);

                if (dto.BrochureFile is not null)
                    brochureUrl = await UploadFile(listId, folderPath, "brochure", dto.BrochureFile);

                if (dto.FichaRucFile is not null)
                    fichaRucUrl = await UploadFile(listId, folderPath, "ficha_ruc", dto.FichaRucFile);

                if (dto.ReferencesListFile is not null)
                    referencesUrl = await UploadFile(listId, folderPath, "lista_referencias", dto.ReferencesListFile);
            }

            await _repository.Create(dto, userId, logoUrl, brochureUrl, fichaRucUrl, referencesUrl);

            await SendNewContractorNotificationAsync(dto);
        }

        private async Task SendNewContractorNotificationAsync(ContributorCreateDto dto)
        {
            var subject = $"Nuevo contratista registrado para revisión: {dto.ContributorName}";

            var body = new StringBuilder();
            body.AppendLine("<p>Estimado equipo de Costos y Presupuestos,</p>");
            body.AppendLine("<p>Se ha registrado un nuevo contratista en el sistema y se encuentra pendiente de revisión. A continuación se detallan los datos registrados:</p>");
            body.AppendLine("<ul>");
            body.AppendLine($"  <li><strong>Razón social:</strong> {dto.ContributorName}</li>");
            body.AppendLine($"  <li><strong>RUC:</strong> {dto.ContributorRuc}</li>");
            if (!string.IsNullOrWhiteSpace(dto.ContributorAddress))
                body.AppendLine($"  <li><strong>Dirección:</strong> {dto.ContributorAddress}</li>");
            if (!string.IsNullOrWhiteSpace(dto.ContributorDistrict))
                body.AppendLine($"  <li><strong>Distrito:</strong> {dto.ContributorDistrict}</li>");
            if (!string.IsNullOrWhiteSpace(dto.ContributorProvince))
                body.AppendLine($"  <li><strong>Provincia:</strong> {dto.ContributorProvince}</li>");
            if (!string.IsNullOrWhiteSpace(dto.ContributorDepartment))
                body.AppendLine($"  <li><strong>Departamento:</strong> {dto.ContributorDepartment}</li>");
            if (!string.IsNullOrWhiteSpace(dto.LegalRepresentativeFullName))
                body.AppendLine($"  <li><strong>Representante legal:</strong> {dto.LegalRepresentativeFullName}</li>");
            if (!string.IsNullOrWhiteSpace(dto.LegalRepresentativeDni))
                body.AppendLine($"  <li><strong>DNI del representante:</strong> {dto.LegalRepresentativeDni}</li>");
            body.AppendLine("</ul>");
            body.AppendLine("<p>Por favor, acceda al sistema para completar la revisión del contratista.</p>");

            await _emailService.SendAsync(
                to:     _costoYPresupuestosEmails,
                subject: subject,
                body:    body.ToString(),
                isHtml:  true);
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
            var result = await _sharePointService.UploadToSharePointLibraryAsync(
                libraryName: listId,
                folderPath:  folderPath,
                fileName:    fileName,
                fileStream:  stream,
                contentType: file.ContentType);
            return result?.WebUrl;
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
