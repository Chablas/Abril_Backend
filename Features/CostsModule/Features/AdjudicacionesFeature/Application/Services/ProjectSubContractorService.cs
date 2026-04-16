using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Interfaces;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Interfaces;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos;
using Abril_Backend.Shared.Services.Email.Interfaces;
using Abril_Backend.Shared.Services.Email.Dtos;
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
            IHttpClientFactory httpClientFactory
            )
        {
            _projectSubContractorRepository = projectSubContractorRepository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
            _projectRepository = projectRepository;
            _delegatedMailService = delegatedMailService;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<PagedResult<ProjectSubContractorDTO>> GetPaged(ProjectSubContractorFilterDTO filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return await _projectSubContractorRepository.GetPaged(filter);
        }

        public async Task Create(ProjectSubContractorCreateDTO dto, int userId)
        {
            var container = _containerResolver.GetProjectSubContractorContainerName();

            var quotationUrls = await UploadFiles(dto.QuotationFiles, container);
            var comparativeUrls = await UploadFiles(dto.ComparativeFiles, container);

            await _projectSubContractorRepository.Create(dto, quotationUrls, comparativeUrls, userId);
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

        private async Task<List<string>> UploadFiles(List<IFormFile>? files, string container)
        {
            if (files == null || !files.Any())
                return new List<string>();

            var filesToUpload = new List<(Stream Stream, string FileName)>();
            var streams = new List<Stream>();

            foreach (var file in files)
            {
                if (file.Length == 0)
                    throw new AbrilException("Se detectó un archivo vacío.");

                var extension = Path.GetExtension(file.FileName);
                var fileName = $"{Guid.NewGuid()}{extension}";
                var stream = file.OpenReadStream();

                streams.Add(stream);
                filesToUpload.Add((stream, fileName));
            }

            try
            {
                return await _fileStorageService.UploadFilesAsync(filesToUpload, container);
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

            // Descargar archivos adjuntos en paralelo
            var attachments = await DownloadAttachmentsAsync(data.QuotationFileUrls, data.ComparativeFileUrls);

            // --- Primer correo: notificación de adjudicación al staff de obra ---
            var subject = $"{data.ProjectDescription} // {data.WorkItemDescription} // {data.CompanyName}";
            var body = BuildFirstEmailBody(data);

            var to = data.StaffEmails.Concat(CostosYPresupuestos).Distinct().ToList();

            await _delegatedMailService.SendAsync(
                graphAccessToken: dto.GraphAccessToken,
                to: to,
                subject: subject,
                body: body,
                isHtml: true,
                //bcc: new List<string> { BccEmail },
                attachments: attachments
            );

            // Actualizar estado de la adjudicación a 2 (notificada)
            await _projectSubContractorRepository.UpdateStatusToSent(dto.ProjectSubContractorId, userId);

            // TODO: Aquí va la lógica del segundo correo
        }

        private static string BuildFirstEmailBody(AdjudicacionNotificationDataDto data)
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

            // Nota: StaffProjectEmail almacena correos de grupos de distribución.
            // Los campos Nombre, Puesto y Número no están disponibles en el modelo actual.
            for (int i = 0; i < data.StaffEmails.Count; i++)
            {
                sb.AppendLine("    <tr>");
                sb.AppendLine($"      <td>{i + 1}</td><td></td><td></td><td></td>");
                sb.AppendLine($"      <td>{data.StaffEmails[i]}</td>");
                sb.AppendLine("    </tr>");
            }

            sb.AppendLine("  </tbody></table>");
            return sb.ToString();
        }

        private async Task<List<MailAttachmentDto>> DownloadAttachmentsAsync(
            List<string> quotationUrls,
            List<string> comparativeUrls)
        {
            var client = _httpClientFactory.CreateClient();
            var allUrls = quotationUrls.Concat(comparativeUrls).ToList();

            var downloadTasks = allUrls.Select(async url =>
            {
                try
                {
                    var bytes = await client.GetByteArrayAsync(url);
                    var fileName = Path.GetFileName(new Uri(url).LocalPath);
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