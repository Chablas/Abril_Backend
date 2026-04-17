using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Interfaces;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Interfaces;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos;
using Abril_Backend.Shared.Services.Email.Interfaces;
using Abril_Backend.Shared.Services.Email.Dtos;
using Abril_Backend.Shared.Services.Graph.Interfaces;
using Abril_Backend.Shared.Services.Graph.Dtos;
using System.Text;

namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Services
{
    public class ProjectSubContractorService : IProjectSubContractorService
    {
        private readonly IProjectSubContractorRepository _projectSubContractorRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;
        private readonly IProjectRepository _projectRepository;
        private readonly IDelegatedMailService _delegatedMailService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IGraphUserService _graphUserService;

        private static readonly List<string> CostosYPresupuestos = new()
        {
            //"eaguinaga@abril.pe",
            //"apimentel@abril.pe",
            //"bquicana@abril.pe",
            //"cavila@abril.pe",
            "alvarezvillegaschristian@gmail.com"
        };

        private const string BccEmail = "calvarez@abril.pe";

        public ProjectSubContractorService(
            IProjectSubContractorRepository projectSubContractorRepository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver,
            IProjectRepository projectRepository,
            IDelegatedMailService delegatedMailService,
            IHttpClientFactory httpClientFactory,
            IGraphUserService graphUserService)
        {
            _projectSubContractorRepository = projectSubContractorRepository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
            _projectRepository = projectRepository;
            _delegatedMailService = delegatedMailService;
            _httpClientFactory = httpClientFactory;
            _graphUserService = graphUserService;
        }

        public async Task<PagedResult<ProjectSubContractorDTO>> GetPaged(ProjectSubContractorFilterDTO filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return await _projectSubContractorRepository.GetPaged(filter);
        }

        public async Task Create(ProjectSubContractorCreateDTO dto, int userId)
        {
            var container = _containerResolver.GetProjectSubContractorContainerName();

            var quotationFiles = await UploadFiles(dto.QuotationFiles, container);
            var comparativeFiles = await UploadFiles(dto.ComparativeFiles, container);

            await _projectSubContractorRepository.Create(dto, quotationFiles, comparativeFiles, userId);
        }

        public async Task<ProjectSubContractorFormDataDTO> GetFormData()
        {
            var projectsTask = _projectRepository.GetAllFactory();
            var contractsTask = _projectSubContractorRepository.GetContractsFactory();
            var contractTypesTask = _projectSubContractorRepository.GetContractTypeFactory();
            var contractOriginsTask = _projectSubContractorRepository.GetContractOriginFactory();
            var paymentMethodsTask = _projectSubContractorRepository.GetPaymentMethodFactory();
            var currenciesTask = _projectSubContractorRepository.GetCurrencyFactory();
            var workItemsTask = _projectSubContractorRepository.GetWorkItemFactory();
            var workItemCategoriesTask = _projectSubContractorRepository.GetWorkItemCategoryFactory();
            var companiesTask = _projectSubContractorRepository.GetCompanyFactory();

            await Task.WhenAll(
                projectsTask,
                contractsTask,
                contractTypesTask,
                contractOriginsTask,
                paymentMethodsTask,
                currenciesTask,
                workItemsTask,
                workItemCategoriesTask,
                companiesTask);

            return new ProjectSubContractorFormDataDTO
            {
                Projects = await projectsTask,
                Contracts = await contractsTask,
                ContractTypes = await contractTypesTask,
                ContractOrigins = await contractOriginsTask,
                PaymentMethods = await paymentMethodsTask,
                Currencies = await currenciesTask,
                WorkItems = await workItemsTask,
                WorkItemCategories = await workItemCategoriesTask,
                Companies = await companiesTask
            };
        }

        private async Task<List<(string Url, string OriginalFileName)>> UploadFiles(List<IFormFile>? files, string container)
        {
            if (files == null || !files.Any())
                return new List<(string, string)>();

            var filesToUpload = new List<(Stream Stream, string FileName)>();
            var originalNames = new List<string>();
            var streams = new List<Stream>();

            foreach (var file in files)
            {
                if (file.Length == 0)
                    throw new AbrilException("Se detectó un archivo vacío.");

                var extension = Path.GetExtension(file.FileName);
                var storedName = $"{Guid.NewGuid()}{extension}";
                var stream = file.OpenReadStream();

                streams.Add(stream);
                filesToUpload.Add((stream, storedName));
                originalNames.Add(file.FileName);
            }

            try
            {
                var urls = await _fileStorageService.UploadFilesAsync(filesToUpload, container);
                return urls.Zip(originalNames, (url, name) => (url, name)).ToList();
            }
            finally
            {
                foreach (var stream in streams)
                    stream.Dispose();
            }
        }

        public async Task SendNotification(SendAdjudicacionNotificationDto dto, int userId)
        {
            var data = await _projectSubContractorRepository.GetNotificationData(dto.ProjectSubContractorId);

            // Consultar perfiles de Graph para todos los staff emails en una sola request
            var userProfiles = await _graphUserService.GetUsersByEmailsAsync(data.StaffEmails);

            Console.WriteLine($"[SendNotification] StaffEmails ({data.StaffEmails.Count}): {string.Join(", ", data.StaffEmails)}");
            Console.WriteLine($"[SendNotification] Perfiles obtenidos de Graph ({userProfiles.Count}):");
            foreach (var kv in userProfiles)
                Console.WriteLine($"  - {kv.Key} → Nombre: {kv.Value.DisplayName}, Puesto: {kv.Value.JobTitle}, Teléfono: {kv.Value.Phone}");

            if (userProfiles.Count == 0)
                Console.WriteLine("  [ADVERTENCIA] No se obtuvo ningún perfil. Verificar token y permisos User.Read.All.");

            var quotationAttachments = await DownloadAttachmentsAsync(data.QuotationFiles);

            var subject = $"{data.ProjectDescription} // {data.WorkItemDescription} // {data.CompanyName}";
            var internalRecipients = data.StaffEmails.Concat(CostosYPresupuestos).Distinct().ToList();

            // --- Correo 1: interno — staff de obra + costos y presupuestos ---
            // TODO: descomentar cuando se defina el cuerpo y los destinatarios correctos
            // var firstEmailBody = BuildInternalEmailBody(data);
            // await _delegatedMailService.SendAsync(
            //     graphAccessToken: dto.GraphAccessToken,
            //     to: internalRecipients,
            //     subject: subject,
            //     body: firstEmailBody,
            //     isHtml: true,
            //     attachments: quotationAttachments.Concat(await DownloadAttachmentsAsync(data.ComparativeFiles)).ToList()
            // );

            // --- Correo único: subcontratista ---
            // TO: subcontratista | CC: staff de obra + costos y presupuestos | Adjunto: solo cotización
            var emailBody = BuildSubcontractorEmailBody(data, userProfiles);
            await _delegatedMailService.SendAsync(
                graphAccessToken: dto.GraphAccessToken,
                to: new List<string> { data.ContractorEmail },
                subject: subject,
                body: emailBody,
                isHtml: true,
                cc: internalRecipients,
                attachments: quotationAttachments
            );

            // Actualizar estado de la adjudicación a 2 (notificada)
            await _projectSubContractorRepository.UpdateStatusToSent(dto.ProjectSubContractorId, userId);
        }

        private static string BuildInternalEmailBody(AdjudicacionNotificationDataDto data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<p>Estimados,</p>");
            sb.AppendLine($"<p>Se adjuntan los archivos de cotización y cuadro comparativo correspondientes a la adjudicación de " +
                          $"<strong>{data.WorkItemDescription}</strong> para el proyecto <strong>{data.ProjectDescription}</strong> " +
                          $"con la empresa <strong>{data.CompanyName}</strong>.</p>");
            return sb.ToString();
        }

        private static string BuildSubcontractorEmailBody(
            AdjudicacionNotificationDataDto data,
            Dictionary<string, GraphUserProfileDto> userProfiles)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<p>Estimado,</p>");
            sb.AppendLine($"<p>Se procede a la adjudicación de <strong>{data.WorkItemDescription}</strong> " +
                          $"para el PROYECTO <strong>{data.ProjectDescription}</strong>. " +
                          "En un siguiente correo, el área de oficina técnica enviará el contrato y la OS. " +
                          "Se remite la matriz de comunicaciones del proyecto a fin de coordinar:</p>");

            sb.AppendLine("<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;\">");
            sb.AppendLine($"  <caption><strong>Edificio Multifamiliar {data.ProjectDescription}</strong></caption>");
            sb.AppendLine("  <thead><tr>");
            sb.AppendLine("    <th>N</th><th>Nombre</th><th>Puesto</th><th>Número</th><th>Correo</th>");
            sb.AppendLine("  </tr></thead>");
            sb.AppendLine("  <tbody>");

            for (int i = 0; i < data.StaffEmails.Count; i++)
            {
                var email = data.StaffEmails[i];
                userProfiles.TryGetValue(email, out var profile);

                sb.AppendLine("    <tr>");
                sb.AppendLine($"      <td>{i + 1}</td>");
                sb.AppendLine($"      <td>{profile?.DisplayName ?? "-"}</td>");
                sb.AppendLine($"      <td>{profile?.JobTitle ?? "-"}</td>");
                sb.AppendLine($"      <td>{profile?.Phone ?? "-"}</td>");
                sb.AppendLine($"      <td>{email}</td>");
                sb.AppendLine("    </tr>");
            }

            sb.AppendLine("  </tbody></table>");
            return sb.ToString();
        }

        private async Task<List<MailAttachmentDto>> DownloadAttachmentsAsync(
            List<ProjectSubContractorFileDto> files)
        {
            if (files == null || files.Count == 0)
                return new List<MailAttachmentDto>();

            var client = _httpClientFactory.CreateClient();

            var downloadTasks = files.Select(async file =>
            {
                try
                {
                    var bytes = await client.GetByteArrayAsync(file.FileUrl);
                    var fileName = !string.IsNullOrWhiteSpace(file.OriginalFileName)
                        ? file.OriginalFileName
                        : Path.GetFileName(new Uri(file.FileUrl).LocalPath);

                    return new MailAttachmentDto
                    {
                        FileName = fileName,
                        ContentType = "application/octet-stream",
                        Content = bytes
                    };
                }
                catch
                {
                    return null;
                }
            });

            var results = await Task.WhenAll(downloadTasks);
            return results.Where(r => r is not null).Cast<MailAttachmentDto>().ToList();
        }
    }
}
