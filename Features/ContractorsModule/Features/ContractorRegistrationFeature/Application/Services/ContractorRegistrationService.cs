using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Interfaces;
using Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Interfaces;
using Abril_Backend.Features.CostsModule.Features.Configuration.CostosPresupuestosEmailFeature.Application.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Options;
using Abril_Backend.Shared.Services.Sunat.Dtos;
using Abril_Backend.Shared.Services.Sunat.Interfaces;
using System.Text;

namespace Abril_Backend.Features.Contractors.ContractorRegistration.Application.Services
{
    public class ContractorRegistrationService : IContractorRegistrationService
    {
        private readonly IContractorRegistrationRepository _repository;
        private readonly ISunatService _sunatService;
        private readonly IGraphSharePointService _sharePointService;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ICostosPresupuestosEmailService _costosPresupuestosEmailService;
        private readonly SharePointSiteRef _site;

        public ContractorRegistrationService(
            IContractorRegistrationRepository repository,
            ISunatService sunatService,
            IGraphSharePointService sharePointService,
            IConfiguration configuration,
            IEmailService emailService,
            ICostosPresupuestosEmailService costosPresupuestosEmailService)
        {
            _repository = repository;
            _sunatService = sunatService;
            _sharePointService = sharePointService;
            _configuration = configuration;
            _emailService = emailService;
            _costosPresupuestosEmailService = costosPresupuestosEmailService;
            _site = SharePointSiteRef.FromConfig(configuration, "CostosYPresupuestos");
        }

        public async Task<List<ContractorPersonTypeDto>> GetPersonTypes()
        {
            return await _repository.GetPersonTypes();
        }

        public async Task Create(ContributorCreateDto dto, int? userId, string? accessToken = null)
        {
            // 1. Validar elegibilidad ANTES de subir archivos y obtener el número de intento.
            //    Lanza AbrilException si el contratista está en espera (1) o ya aprobado (2).
            int attemptNumber = await _repository.ValidateAndGetAttemptNumberAsync(dto.ContributorRuc);

            // 2. Subir archivos a SharePoint en la subcarpeta del intento correspondiente.
            //    Esto ocurre solo después de confirmar que el envío está permitido,
            //    evitando carpetas huérfanas en caso de rechazo.
            string? logoUrl       = null;
            string? brochureUrl   = null;
            string? fichaRucUrl   = null;
            string? referencesUrl = null;

            if (dto.LogoFile is not null || dto.BrochureFile is not null || dto.FichaRucFile is not null || dto.ReferencesListFile is not null)
            {
                var listId          = _configuration["SharePoint:Sites:CostosYPresupuestos:ContractorLibraryId"]
                                      ?? throw new InvalidOperationException("SharePoint:Sites:CostosYPresupuestos:ContractorLibraryId no está configurado.");
                var desiredFolder   = Sanitize($"{dto.ContributorRuc} - {dto.ContributorName}");

                // Buscar carpeta existente para este RUC (independientemente de si la razón social cambió).
                // · Si existe y el nombre coincide → se reutiliza tal cual.
                // · Si existe pero el nombre cambió  → se renombra a la razón social actual.
                // · Si no existe                    → Graph la crea automáticamente al subir el primer archivo.
                var existing = await _sharePointService.FindContractorFolderAsync(_site, listId, dto.ContributorRuc);
                if (existing is not null
                    && !string.Equals(existing.Value.Name, desiredFolder, StringComparison.OrdinalIgnoreCase))
                {
                    await _sharePointService.RenameFolderInLibraryAsync(_site, listId, existing.Value.Id, desiredFolder);
                }

                var folderPath = $"{desiredFolder}/Solicitud {attemptNumber}";

                if (dto.LogoFile is not null)
                    logoUrl = await UploadFile(listId, folderPath, "logo", dto.LogoFile);

                if (dto.BrochureFile is not null)
                    brochureUrl = await UploadFile(listId, folderPath, "brochure", dto.BrochureFile);

                if (dto.FichaRucFile is not null)
                    fichaRucUrl = await UploadFile(listId, folderPath, "ficha_ruc", dto.FichaRucFile);

                if (dto.ReferencesListFile is not null)
                    referencesUrl = await UploadFile(listId, folderPath, "lista_referencias", dto.ReferencesListFile);
            }

            // 3. Crear el registro en base de datos (nuevo Contractor por cada intento).
            await _repository.Create(dto, userId, logoUrl, brochureUrl, fichaRucUrl, referencesUrl);

            // 4. Notificar al equipo interno.
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

            var costosEmails = await _costosPresupuestosEmailService.GetActiveEmails();
            await _emailService.SendAsync(
                to:     costosEmails,
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
                site:        _site,
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
