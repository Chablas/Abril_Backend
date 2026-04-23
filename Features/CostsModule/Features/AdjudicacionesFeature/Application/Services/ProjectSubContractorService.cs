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
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Helpers;
using ClosedXML.Excel;
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
        private readonly IGraphUserService _graphUserService;
        private readonly IGraphSharePointService _sharePointService;

        private static readonly List<string> CostosYPresupuestos = new()
        {
            "eaguinaga@abril.pe",
            "apimentel@abril.pe",
            "bquicana@abril.pe",
            "cavila@abril.pe",
            //"alvarezvillegaschristian@gmail.com"
        };

        private const string BccEmail = "calvarez@abril.pe";

        public ProjectSubContractorService(
            IProjectSubContractorRepository projectSubContractorRepository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver,
            IProjectRepository projectRepository,
            IDelegatedMailService delegatedMailService,
            IGraphUserService graphUserService,
            IGraphSharePointService sharePointService)
        {
            _projectSubContractorRepository = projectSubContractorRepository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
            _projectRepository = projectRepository;
            _delegatedMailService = delegatedMailService;
            _graphUserService = graphUserService;
            _sharePointService = sharePointService;
        }

        public async Task<PagedResult<ProjectSubContractorDTO>> GetPaged(ProjectSubContractorFilterDTO filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return await _projectSubContractorRepository.GetPaged(filter);
        }

        public async Task Create(ProjectSubContractorCreateDTO dto, int userId)
        {
            // Phase 1: persist the record and get the new ID (needed for the folder path).
            var newId = await _projectSubContractorRepository.Create(dto, userId);

            // Phase 2: fetch path data and upload files to SharePoint.
            var pathData = await _projectSubContractorRepository.GetPathDataAsync(newId);

            var quotationFiles   = await UploadFilesToSharePoint(dto.QuotationFiles,   pathData, AdjudicacionDocumentType.InitialQuotation);
            var comparativeFiles = await UploadFilesToSharePoint(dto.ComparativeFiles, pathData, AdjudicacionDocumentType.InitialComparative);

            // Phase 3: save file records.
            await _projectSubContractorRepository.SaveInitialFilesAsync(newId, quotationFiles, comparativeFiles, userId);
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
            var contributorsTask = _projectSubContractorRepository.GetCompanyFactory();

            await Task.WhenAll(
                projectsTask,
                contractsTask,
                contractTypesTask,
                contractOriginsTask,
                paymentMethodsTask,
                currenciesTask,
                workItemsTask,
                workItemCategoriesTask,
                contributorsTask);

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
                Contributors = await contributorsTask
            };
        }

        private async Task<List<(string Url, string OriginalFileName)>> UploadFilesToSharePoint(
            List<IFormFile>? files,
            AdjudicacionPathDataDto pathData,
            AdjudicacionDocumentType documentType)
        {
            if (files == null || files.Count == 0)
                return new List<(string, string)>();

            var folderPath = BuildSharePointPath(pathData, documentType);
            var results    = new List<(string Url, string OriginalFileName)>();

            foreach (var file in files)
            {
                if (file.Length == 0)
                    throw new AbrilException("Se detectó un archivo vacío.");

                using var stream = file.OpenReadStream();
                var fileUrl = await _sharePointService.UploadToSharePointLibraryAsync(
                    libraryName: "Adjudicaciones",
                    folderPath:  folderPath,
                    fileName:    file.FileName,
                    fileStream:  stream,
                    contentType: file.ContentType)
                    ?? throw new AbrilException("No se pudo obtener la URL del archivo subido.");

                results.Add((fileUrl, file.FileName));
            }

            return results;
        }

        public async Task SendNotification(SendAdjudicacionNotificationDto dto, int userId)
        {
            var data = await _projectSubContractorRepository.GetNotificationData(dto.ProjectSubContractorId);

            // Expandir grupos: staff de obra (van a la matriz) y oficina central (solo CC).
            var staffProfiles          = await _graphUserService.GetResolvedProfilesAsync(data.StaffEmails);
            var oficinaCentralProfiles = await _graphUserService.GetResolvedProfilesAsync(data.OficinaCentralEmails);

            Console.WriteLine($"[SendNotification] StaffEmails ({data.StaffEmails.Count}): {string.Join(", ", data.StaffEmails)}");
            Console.WriteLine($"[SendNotification] Perfiles staff resueltos ({staffProfiles.Count}):");
            foreach (var p in staffProfiles)
                Console.WriteLine($"  - {p.Mail} → Nombre: {p.DisplayName}, Puesto: {p.JobTitle}, Teléfono: {p.Phone}");

            Console.WriteLine($"[SendNotification] OficinaCentralEmails ({data.OficinaCentralEmails.Count}): {string.Join(", ", data.OficinaCentralEmails)}");
            Console.WriteLine($"[SendNotification] Perfiles oficina central resueltos ({oficinaCentralProfiles.Count}):");
            foreach (var p in oficinaCentralProfiles)
                Console.WriteLine($"  - {p.Mail}");

            if (staffProfiles.Count == 0 && oficinaCentralProfiles.Count == 0)
                Console.WriteLine("  [ADVERTENCIA] No se obtuvo ningún perfil. Verificar token y permisos User.Read.All / GroupMember.Read.All.");

            var quotationAttachments = await DownloadAttachmentsAsync(data.QuotationFiles);

            var subject = $"{data.ProjectDescription} // {data.WorkItemDescription} // {data.ContributorName}";

            // CC = staff de obra (expandidos) + oficina central (expandidos) + equipo costos y presupuestos.
            var expandedStaff          = staffProfiles.Select(p => p.Mail).Where(m => !string.IsNullOrWhiteSpace(m));
            var expandedOficinaCentral = oficinaCentralProfiles.Select(p => p.Mail).Where(m => !string.IsNullOrWhiteSpace(m));
            var internalRecipients     = expandedStaff
                .Concat(expandedOficinaCentral)
                .Concat(CostosYPresupuestos)
                .Distinct()
                .ToList();

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
            // TO: subcontratista | CC: todos los internos (staff + oficina central + costos) | Adjunto: cotización
            // Matriz de comunicaciones: solo staff de obra (staffProfiles).
            var emailBody = BuildSubcontractorEmailBody(data, staffProfiles);
            await _delegatedMailService.SendAsync(
                graphAccessToken: dto.GraphAccessToken,
                to: data.ContractorEmails,
                subject: subject,
                body: emailBody,
                isHtml: true,
                cc: internalRecipients,
                attachments: quotationAttachments
            );

            // Actualizar estado de la adjudicación a 2 (notificada)
            await _projectSubContractorRepository.UpdateStatusToSent(dto.ProjectSubContractorId, userId);
        }

        public async Task UpdateStatusAsync(int projectSubContractorId, int statusId, int userId)
        {
            await _projectSubContractorRepository.UpdateStatus(projectSubContractorId, statusId, userId);
        }

        public async Task SendScNotificationAsync(int projectSubContractorId, string graphAccessToken, IFormFile file, int userId)
        {
            var data     = await _projectSubContractorRepository.GetScNotificationDataAsync(projectSubContractorId);
            var pathData = await _projectSubContractorRepository.GetPathDataAsync(projectSubContractorId);

            // Leer bytes una sola vez: se usan para SP y para el adjunto del correo
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();

            // Subir a SharePoint
            var folderPath = BuildSharePointPath(pathData, AdjudicacionDocumentType.ScPackage);
            await _sharePointService.UploadToSharePointLibraryAsync(
                libraryName: "Adjudicaciones",
                folderPath:  folderPath,
                fileName:    file.FileName,
                fileStream:  new MemoryStream(fileBytes),
                contentType: file.ContentType);

            // Construir y enviar el correo
            var subject    = $"{data.ProjectDescription} : {file.FileName}";
            var body       = BuildScEmailBody(file.FileName, data.WorkItemDescription);
            var attachment = new MailAttachmentDto
            {
                FileName    = file.FileName,
                ContentType = file.ContentType ?? "application/octet-stream",
                Content     = fileBytes
            };

            await _delegatedMailService.SendAsync(
                graphAccessToken: graphAccessToken,
                to:          data.ContractorEmails,
                subject:     subject,
                body:        body,
                isHtml:      true,
                attachments: new List<MailAttachmentDto> { attachment });

            // Avanzar al estado 5
            await _projectSubContractorRepository.UpdateStatus(projectSubContractorId, 5, userId);
        }

        public async Task SendStep8NotificationAsync(int projectSubContractorId, string graphAccessToken, int userId)
        {
            var data = await _projectSubContractorRepository.GetStep8NotificationDataAsync(projectSubContractorId);

            if (data.OfTecnicaEmails.Count == 0)
                throw new AbrilException("No hay correos de Oficina Técnica configurados para este proyecto.");

            if (data.ScannedDocs.Count == 0)
                throw new AbrilException("No hay documentos escaneados adjuntos para enviar.");

            var attachments = await DownloadAttachmentsAsync(data.ScannedDocs);

            var subject = $"CONTRATOS FIRMADOS / {data.ProjectDescription}";
            var body    = BuildStep8EmailBody(data.ContributorName, data.ContractDescription);

            await _delegatedMailService.SendAsync(
                graphAccessToken: graphAccessToken,
                to:               data.OfTecnicaEmails,
                subject:          subject,
                body:             body,
                isHtml:           true,
                attachments:      attachments);

            await _projectSubContractorRepository.UpdateStatus(projectSubContractorId, 9, userId);
        }

        private static string BuildStep8EmailBody(string contributorName, string contractDescription)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<p>Estimados buenas tardes,</p>");
            sb.AppendLine("<p>Adjunto contratos escaneados y firmados. Se encuentran en recepción para su recojo.</p>");
            sb.AppendLine("<p><strong>CONTRATOS:</strong></p>");
            sb.AppendLine("<ul>");
            sb.AppendLine($"  <li>{contractDescription}: {contributorName}</li>");
            sb.AppendLine("</ul>");
            sb.AppendLine("<p>Saludos.</p>");
            return sb.ToString();
        }

        private static string BuildScEmailBody(string fileName, string workItemDescription)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<p>Buenos días estimados,</p>");
            sb.AppendLine($"<p>Adjunto contrato <strong>{fileName}</strong> por <strong>{workItemDescription}</strong>.</p>");
            sb.AppendLine("<p>Se solicita la impresión y firma de <strong>TODOS LOS DOCUMENTOS ADJUNTOS</strong> en 03 juegos. " +
                          "Asimismo, estos documentos deberán ser entregados en el orden de la numeración de cada documento. " +
                          "<strong>Entregarlos en oficina central: Cal. Mama Ocllo N°2647</strong> Urb. Risso Lima- Lima-Lince (L-V de 8:00am-5:30 pm).</p>");
            sb.AppendLine("<p>Tener en cuenta;</p>");
            sb.AppendLine("<ul>");
            sb.AppendLine("  <li>Las hojas del contrato <strong>no deberán estar engrampadas.</strong></li>");
            sb.AppendLine("  <li>No imprimir a doble cara</li>");
            sb.AppendLine("  <li>Firmar con lapicero azul todas las hojas adjuntas, no solo basta con un VB</li>");
            sb.AppendLine("  <li><span style=\"background-color: yellow;\">Llevarlo cuanto antes para que no restrinjan el pago de las valorizaciones</span></li>");
            sb.AppendLine("</ul>");
            sb.AppendLine("<p>Quedo atenta.</p>");
            sb.AppendLine("<p>Saludos.</p>");
            return sb.ToString();
        }

        private static string BuildInternalEmailBody(AdjudicacionNotificationDataDto data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<p>Estimados,</p>");
            sb.AppendLine($"<p>Se adjuntan los archivos de cotización y cuadro comparativo correspondientes a la adjudicación de " +
                          $"<strong>{data.WorkItemDescription}</strong> para el proyecto <strong>{data.ProjectDescription}</strong> " +
                          $"con la empresa <strong>{data.ContributorName}</strong>.</p>");
            return sb.ToString();
        }

        private static string BuildSubcontractorEmailBody(
            AdjudicacionNotificationDataDto data,
            List<GraphUserProfileDto> userProfiles)
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

            for (int i = 0; i < userProfiles.Count; i++)
            {
                var profile = userProfiles[i];
                sb.AppendLine("    <tr>");
                sb.AppendLine($"      <td>{i + 1}</td>");
                sb.AppendLine($"      <td>{profile.DisplayName ?? "-"}</td>");
                sb.AppendLine($"      <td>{profile.JobTitle ?? "-"}</td>");
                sb.AppendLine($"      <td>{profile.Phone ?? "-"}</td>");
                sb.AppendLine($"      <td>{profile.Mail}</td>");
                sb.AppendLine("    </tr>");
            }

            sb.AppendLine("  </tbody></table>");
            return sb.ToString();
        }

        public async Task SaveDates(int projectSubContractorId, UpdateDatesDTO dto, int userId)
        {
            await _projectSubContractorRepository.SaveDates(projectSubContractorId, dto, userId);
        }

        public async Task UpdateDocumentStatusAsync(
            int projectSubContractorId,
            AdjudicacionDocumentType documentType,
            int? statusId,
            string? observation,
            int userId)
        {
            await _projectSubContractorRepository.UpdateDocumentStatusAsync(
                projectSubContractorId, documentType, statusId, observation, userId);
        }

        public async Task<DocumentUploadResponseDto> UploadDocumentAsync(
            int projectSubContractorId,
            AdjudicacionDocumentType documentType,
            IFormFile file,
            int userId)
        {
            if (file is null || file.Length == 0)
                throw new AbrilException("El archivo no puede estar vacío.");

            var pathData   = await _projectSubContractorRepository.GetPathDataAsync(projectSubContractorId);
            var folderPath = BuildSharePointPath(pathData, documentType);
            var fileName   = file.FileName;

            string fileUrl;
            using (var stream = file.OpenReadStream())
            {
                fileUrl = await _sharePointService.UploadToSharePointLibraryAsync(
                    libraryName: "Adjudicaciones",
                    folderPath:  folderPath,
                    fileName:    fileName,
                    fileStream:  stream,
                    contentType: file.ContentType) ?? throw new AbrilException("No se pudo obtener la URL del archivo subido.");
            }

            await _projectSubContractorRepository.SaveDocumentAsync(
                projectSubContractorId, documentType, fileUrl, fileName, userId);

            return new DocumentUploadResponseDto
            {
                FileUrl          = fileUrl,
                OriginalFileName = fileName,
            };
        }

        // ── Generación de documentos ─────────────────────────────────────────

        public async Task<DocumentUploadResponseDto> GenerateDocumentAsync(
            int projectSubContractorId,
            AdjudicacionDocumentType documentType,
            int userId)
        {
            return documentType switch
            {
                AdjudicacionDocumentType.SummarySheet =>
                    await GenerateSummarySheetAsync(projectSubContractorId, userId),
                AdjudicacionDocumentType.Contract =>
                    await GenerateContractAsync(projectSubContractorId, userId),
                AdjudicacionDocumentType.Budget =>
                    await GenerateBudgetAsync(projectSubContractorId, userId),
                _ => throw new AbrilException(
                    $"La generación del documento '{documentType}' aún no está implementada.")
            };
        }

        private async Task<DocumentUploadResponseDto> GenerateSummarySheetAsync(
            int projectSubContractorId, int userId)
        {
            var data = await _projectSubContractorRepository.GetSummarySheetDataAsync(projectSubContractorId);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("1.HOJA RESUMEN");
            BuildSummarySheet(ws, data);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;

            var pathData = new AdjudicacionPathDataDto
            {
                ProjectSubContractorId = data.ProjectSubContractorId,
                ProjectDescription     = data.ProjectDescription,
                ContributorRuc         = data.ContributorRuc,
                ContributorName        = data.ContributorName,
                WorkItemDescription    = data.WorkItemDescription,
            };

            var folderPath = BuildSharePointPath(pathData, AdjudicacionDocumentType.SummarySheet);
            var fileName   = $"HOJA_RESUMEN_{data.ProjectSubContractorId:D4}.xlsx";
            const string xlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            var fileUrl = await _sharePointService.UploadToSharePointLibraryAsync(
                libraryName: "Adjudicaciones",
                folderPath:  folderPath,
                fileName:    fileName,
                fileStream:  ms,
                contentType: xlsxMime)
                ?? throw new AbrilException("No se pudo obtener la URL del archivo generado.");

            await _projectSubContractorRepository.SaveDocumentAsync(
                projectSubContractorId, AdjudicacionDocumentType.SummarySheet, fileUrl, fileName, userId);

            return new DocumentUploadResponseDto { FileUrl = fileUrl, OriginalFileName = fileName };
        }

        private async Task<DocumentUploadResponseDto> GenerateContractAsync(
            int projectSubContractorId, int userId)
        {
            var data = await _projectSubContractorRepository.GetSummarySheetDataAsync(projectSubContractorId);

            var templatePath = Path.Combine(
                AppContext.BaseDirectory,
                "Features", "CostsModule", "Features", "AdjudicacionesFeature",
                "Templates", "plantilla_contrato_con_placeholders.docx");

            if (!File.Exists(templatePath))
                throw new AbrilException(
                    "No se encontró la plantilla del contrato en el servidor. " +
                    "Contacte al administrador del sistema.");

            var plazo = (data.StartDate.HasValue && data.EndDate.HasValue)
                ? (int)(data.EndDate.Value.ToDateTime(TimeOnly.MinValue)
                      - data.StartDate.Value.ToDateTime(TimeOnly.MinValue)).TotalDays
                : 0;

            var currencySymbol = data.CurrencyCode == "USD" ? "US$" : "S/";

            var replacements = new Dictionary<string, string>
            {
                { "{{EMPRESA}}",        data.ContributorName },
                { "{{PROYECTO}}",       data.ProjectDescription },
                { "{{MONTO}}",          $"{currencySymbol} {data.Amount:N2}" },
                { "{{FECHA_INICIO}}",   data.StartDate?.ToString("dd/MM/yyyy") ?? "" },
                { "{{FECHA_FIN}}",      data.EndDate?.ToString("dd/MM/yyyy")   ?? "" },
                { "{{RUC}}",            data.ContributorRuc },
                { "{{TIPO_CONTRATO}}",  data.ContractTypeDescription },
                { "{{PARTIDA}}",        data.WorkItemDescription },
            };

            byte[] docBytes;
            using (var templateStream = File.OpenRead(templatePath))
                docBytes = WordTemplateHelper.FillTemplate(templateStream, replacements);

            var pathData = new AdjudicacionPathDataDto
            {
                ProjectSubContractorId = data.ProjectSubContractorId,
                ProjectDescription     = data.ProjectDescription,
                ContributorRuc         = data.ContributorRuc,
                ContributorName        = data.ContributorName,
                WorkItemDescription    = data.WorkItemDescription,
            };

            var folderPath = BuildSharePointPath(pathData, AdjudicacionDocumentType.Contract);
            var fileName   = $"CONTRATO_{data.ProjectSubContractorId:D4}.docx";
            const string docxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            string fileUrl;
            using (var ms = new MemoryStream(docBytes))
            {
                fileUrl = await _sharePointService.UploadToSharePointLibraryAsync(
                    libraryName: "Adjudicaciones",
                    folderPath:  folderPath,
                    fileName:    fileName,
                    fileStream:  ms,
                    contentType: docxMime)
                    ?? throw new AbrilException("No se pudo obtener la URL del archivo generado.");
            }

            await _projectSubContractorRepository.SaveDocumentAsync(
                projectSubContractorId, AdjudicacionDocumentType.Contract, fileUrl, fileName, userId);

            return new DocumentUploadResponseDto { FileUrl = fileUrl, OriginalFileName = fileName };
        }

        private async Task<DocumentUploadResponseDto> GenerateBudgetAsync(
            int projectSubContractorId, int userId)
        {
            var data = await _projectSubContractorRepository.GetSummarySheetDataAsync(projectSubContractorId);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("PRESUPUESTO");
            BuildBudget(ws, data);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;

            var pathData = new AdjudicacionPathDataDto
            {
                ProjectSubContractorId = data.ProjectSubContractorId,
                ProjectDescription     = data.ProjectDescription,
                ContributorRuc         = data.ContributorRuc,
                ContributorName        = data.ContributorName,
                WorkItemDescription    = data.WorkItemDescription,
            };

            var folderPath = BuildSharePointPath(pathData, AdjudicacionDocumentType.Budget);
            var fileName   = $"PRESUPUESTO_{data.ProjectSubContractorId:D4}.xlsx";
            const string xlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            var fileUrl = await _sharePointService.UploadToSharePointLibraryAsync(
                libraryName: "Adjudicaciones",
                folderPath:  folderPath,
                fileName:    fileName,
                fileStream:  ms,
                contentType: xlsxMime)
                ?? throw new AbrilException("No se pudo obtener la URL del archivo generado.");

            await _projectSubContractorRepository.SaveDocumentAsync(
                projectSubContractorId, AdjudicacionDocumentType.Budget, fileUrl, fileName, userId);

            return new DocumentUploadResponseDto { FileUrl = fileUrl, OriginalFileName = fileName };
        }

        private static void BuildBudget(IXLWorksheet ws, AdjudicacionSummarySheetDataDto data)
        {
            // ── Column widths ──────────────────────────────────────────────────
            ws.Column("A").Width = 2;    // left margin
            ws.Column("B").Width = 50;   // DESCRIPCIÓN
            ws.Column("C").Width = 10;   // UND
            ws.Column("D").Width = 14;   // METRADO
            ws.Column("E").Width = 16;   // P.U.
            ws.Column("F").Width = 16;   // COSTO TOTAL
            ws.Column("G").Width = 2;    // right margin

            // ── Row heights ────────────────────────────────────────────────────
            ws.Row(2).Height  = 32;
            ws.Row(10).Height = 22;
            ws.Row(11).Height = 22;

            var currencySymbol = data.CurrencyCode == "USD" ? "US$" : "S/";
            var currencyFmt    = $"\"{currencySymbol}\" #,##0.00";

            // ── Row 2: Title ───────────────────────────────────────────────────
            ws.Range("B2:F2").Merge();
            ws.Cell("B2").Value =
                $"PRESUPUESTO CONTRATO N° {data.ProjectSubContractorId:D4} " +
                $"A {data.ContractTypeDescription.ToUpper()} " +
                $"POR {data.WorkItemDescription.ToUpper()}";
            ws.Range("B2:F2").Style.Font.Bold                = true;
            ws.Range("B2:F2").Style.Font.FontSize            = 11;
            ws.Range("B2:F2").Style.Alignment.Horizontal     = XLAlignmentHorizontalValues.Center;
            ws.Range("B2:F2").Style.Alignment.Vertical       = XLAlignmentVerticalValues.Center;
            ws.Range("B2:F2").Style.Alignment.WrapText       = true;
            ws.Range("B2:F2").Style.Border.OutsideBorder     = XLBorderStyleValues.Medium;

            // ── Rows 5–8: Info block ───────────────────────────────────────────
            void InfoLabel(string cell, string text)
            {
                ws.Cell(cell).Value = text;
                ws.Cell(cell).Style.Font.Bold = true;
            }

            InfoLabel("B5", "Proyecto:");
            ws.Cell("C5").Value = data.ProjectDescription;
            ws.Range("C5:F5").Merge();

            InfoLabel("B6", "Contratista:");
            ws.Cell("C6").Value = data.ContributorName;
            ws.Range("C6:F6").Merge();

            InfoLabel("B7", "N° de niveles:");
            // (no data available — the user fills this in)
            ws.Range("C7:F7").Merge();

            InfoLabel("B8", "Fecha:");
            if (data.SigningDate.HasValue)
            {
                ws.Cell("C8").Value = data.SigningDate.Value.ToDateTime(TimeOnly.MinValue);
                ws.Cell("C8").Style.DateFormat.Format = "dd/MM/yyyy";
            }
            ws.Range("C8:F8").Merge();

            // ── Row 10: Section header ─────────────────────────────────────────
            ws.Range("B10:F10").Merge();
            ws.Cell("B10").Value = data.WorkItemDescription.ToUpper();
            ws.Range("B10:F10").Style.Font.Bold                = true;
            ws.Range("B10:F10").Style.Fill.BackgroundColor     = XLColor.FromHtml("#D9D9D9");
            ws.Range("B10:F10").Style.Alignment.Horizontal     = XLAlignmentHorizontalValues.Center;
            ws.Range("B10:F10").Style.Alignment.Vertical       = XLAlignmentVerticalValues.Center;
            ws.Range("B10:F10").Style.Border.OutsideBorder     = XLBorderStyleValues.Medium;

            // ── Row 11: Column headers ─────────────────────────────────────────
            void SetColHeader(string cell, string text)
            {
                ws.Cell(cell).Value = text;
                ws.Cell(cell).Style.Font.Bold                = true;
                ws.Cell(cell).Style.Fill.BackgroundColor     = XLColor.FromHtml("#D9D9D9");
                ws.Cell(cell).Style.Alignment.Horizontal     = XLAlignmentHorizontalValues.Center;
                ws.Cell(cell).Style.Alignment.Vertical       = XLAlignmentVerticalValues.Center;
                ws.Cell(cell).Style.Alignment.WrapText       = true;
                ws.Cell(cell).Style.Border.OutsideBorder     = XLBorderStyleValues.Thin;
            }

            SetColHeader("B11", "DESCRIPCIÓN");
            SetColHeader("C11", "UND");
            SetColHeader("D11", "METRADO");
            SetColHeader("E11", "P.U.");
            SetColHeader("F11", "COSTO TOTAL");

            // ── Row 12: Category row (work item category) ──────────────────────
            ws.Range("B12:F12").Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
            ws.Cell("B12").Value = data.WorkItemDescription.ToUpper();
            ws.Cell("B12").Style.Font.Bold = true;
            foreach (var col in new[] { "B", "C", "D", "E", "F" })
                ws.Cell($"{col}12").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // ── Rows 13–17: Empty item rows (template) ─────────────────────────
            const int firstItemRow = 13;
            const int lastItemRow  = 17;

            for (int r = firstItemRow; r <= lastItemRow; r++)
            {
                ws.Row(r).Height = 18;
                // COSTO TOTAL = METRADO * P.U.
                ws.Cell(r, 6).FormulaA1 = $"=IF(AND(D{r}<>\"\",E{r}<>\"\"),D{r}*E{r},\"\")";
                ws.Cell(r, 6).Style.NumberFormat.Format = currencyFmt;

                foreach (var col in new[] { "B", "C", "D", "E", "F" })
                {
                    ws.Cell($"{col}{r}").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    ws.Cell($"{col}{r}").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                }
                // Currency hint for P.U. column
                ws.Cell(r, 5).Style.NumberFormat.Format = currencyFmt;
            }

            // ── Summary rows ──────────────────────────────────────────────────
            int subtotalRow = lastItemRow + 2;   // 19
            int igvRow      = subtotalRow + 1;   // 20
            int totalRow    = igvRow + 1;         // 21

            ws.Row(subtotalRow).Height = 18;
            ws.Row(igvRow).Height      = 18;
            ws.Row(totalRow).Height    = 18;

            void SummaryLabel(int row, string text)
            {
                ws.Cell(row, 4).Value = text;
                ws.Cell(row, 4).Style.Font.Bold            = true;
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Cell(row, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Cell(row, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin; // P.U. col left border
            }

            // SUBTOTAL
            SummaryLabel(subtotalRow, "SUBTOTAL");
            ws.Cell(subtotalRow, 6).FormulaA1      = $"=SUM(F{firstItemRow}:F{lastItemRow})";
            ws.Cell(subtotalRow, 6).Style.NumberFormat.Format  = currencyFmt;
            ws.Cell(subtotalRow, 6).Style.Font.Bold            = true;
            ws.Cell(subtotalRow, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // IGV (18%)
            SummaryLabel(igvRow, "IGV (18%)");
            ws.Cell(igvRow, 6).FormulaA1      = $"=F{subtotalRow}*0.18";
            ws.Cell(igvRow, 6).Style.NumberFormat.Format  = currencyFmt;
            ws.Cell(igvRow, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // TOTAL
            SummaryLabel(totalRow, "TOTAL");
            ws.Cell(totalRow, 6).FormulaA1      = $"=F{subtotalRow}+F{igvRow}";
            ws.Cell(totalRow, 6).Style.NumberFormat.Format  = currencyFmt;
            ws.Cell(totalRow, 6).Style.Font.Bold            = true;
            ws.Cell(totalRow, 6).Style.Font.FontColor       = XLColor.FromHtml("#E26B0A");
            ws.Cell(totalRow, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // ── Outer border around the whole table ───────────────────────────
            ws.Range($"B10:F{totalRow}").Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        }

        private static void BuildSummarySheet(IXLWorksheet ws, AdjudicacionSummarySheetDataDto data)
        {
            // ── Column widths ──────────────────────────────────────────────────
            ws.Column("A").Width = 2;
            ws.Column("B").Width = 28;
            ws.Column("C").Width = 8;
            ws.Column("D").Width = 12;
            ws.Column("E").Width = 17;
            ws.Column("F").Width = 18;
            ws.Column("G").Width = 16;
            ws.Column("H").Width = 11;
            ws.Column("I").Width = 17;
            ws.Column("J").Width = 17;
            ws.Column("K").Width = 12;
            ws.Column("L").Width = 12;
            ws.Column("M").Width = 24;
            ws.Column("N").Width = 2;

            // ── Row heights ────────────────────────────────────────────────────
            ws.Row(2).Height  = 32;
            ws.Row(12).Height = 42;
            ws.Row(13).Height = 38;
            ws.Row(14).Height = 18;
            ws.Row(15).Height = 20;

            // ── Calculations ──────────────────────────────────────────────────
            var advance = (data.AdvancePercentage ?? 0) / 100m * data.Amount;
            var saldo   = data.Amount - advance;
            var plazo   = (data.StartDate.HasValue && data.EndDate.HasValue)
                ? (int)(data.EndDate.Value.ToDateTime(TimeOnly.MinValue)
                      - data.StartDate.Value.ToDateTime(TimeOnly.MinValue)).TotalDays
                : 0;
            var currencySymbol = data.CurrencyCode == "USD" ? "US$" : "S/";

            // ── Row 2: Title ───────────────────────────────────────────────────
            ws.Range("B2:N2").Merge();
            ws.Cell("B2").Value = $"RESUMEN DEL CONTRATO N°{data.ProjectSubContractorId:D4} " +
                                  $"{data.ContractTypeDescription.ToUpper()} POR EL SERVICIO DE " +
                                  $"{data.WorkItemDescription.ToUpper()}";
            ws.Range("B2:N2").Style.Font.Bold       = true;
            ws.Range("B2:N2").Style.Font.FontSize   = 11;
            ws.Range("B2:N2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range("B2:N2").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            ws.Range("B2:N2").Style.Alignment.WrapText   = true;
            ws.Range("B2:N2").Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            // ── Rows 5–8: Info block ───────────────────────────────────────────
            ws.Cell("B5").Value = "Proyecto :";  ws.Cell("B5").Style.Font.Bold = true;
            ws.Cell("C5").Value = data.ProjectDescription;

            ws.Cell("B6").Value = "Contratista:"; ws.Cell("B6").Style.Font.Bold = true;
            ws.Cell("C6").Value = data.ContributorName;

            ws.Cell("B8").Value = "Fecha:"; ws.Cell("B8").Style.Font.Bold = true;
            if (data.SigningDate.HasValue)
            {
                ws.Cell("C8").Value = data.SigningDate.Value.ToDateTime(TimeOnly.MinValue);
                ws.Cell("C8").Style.DateFormat.Format = "dd/MM/yyyy";
            }

            // ── Row 11: Sub-header ─────────────────────────────────────────────
            ws.Range("B11:D11").Merge();
            ws.Cell("B11").Value = $"EDIFICIO RESIDENCIAL \"{data.ProjectDescription.ToUpper()}\"";
            ws.Range("B11:D11").Style.Font.Bold      = true;
            ws.Range("B11:D11").Style.Font.FontColor = XLColor.FromHtml("#0070C0");
            ws.Range("B11:D11").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range("B11:D11").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            ws.Range("K11:L11").Merge();
            ws.Cell("K11").Value = "PLAZO";
            ws.Range("K11:L11").Style.Font.Bold = true;
            ws.Range("K11:L11").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range("K11:L11").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // ── Row 12: Column headers ─────────────────────────────────────────
            void SetHeader(string address, string text)
            {
                var r = ws.Range(address);
                if (address.Contains(':')) r.Merge();
                r.Style.Font.Bold = true;
                r.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
                r.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                r.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                r.Style.Alignment.WrapText   = true;
                r.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                r.FirstCell().Value = text;
            }

            SetHeader("B12:C12", "DESCRIPCIÓN");
            SetHeader("D12:D12", "FECHA DE\nCONTRATO");
            SetHeader("E12:E12", "MONTO CONTRATADO\n(inc IGV)");
            SetHeader("F12:F12", "N° DOC");
            SetHeader("G12:G12", "CHEQUE / RECIBO");
            SetHeader("H12:H12", "% ADELANTO");
            SetHeader("I12:I12", "IMPORTE ADELANTO\nS/");
            SetHeader("J12:J12", "SALDO");
            SetHeader("K12:K12", "INICIO");
            SetHeader("L12:L12", "FIN");
            SetHeader("M12:M12", "OBSERVACION");

            // ── Rows 13–14: Data row ───────────────────────────────────────────
            ws.Range("B13:C14").Merge();
            ws.Cell("B13").Value = $"{data.WorkItemDescription.ToUpper()} (MONTO CONTRACTUAL)";
            ws.Range("B13:C14").Style.Font.Bold = true;
            ws.Range("B13:C14").Style.Alignment.WrapText   = true;
            ws.Range("B13:C14").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            ws.Range("B13:C14").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range("B13:C14").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // D – Fecha contrato
            ws.Range("D13:D14").Merge();
            if (data.SigningDate.HasValue)
            {
                ws.Cell("D13").Value = data.SigningDate.Value.ToDateTime(TimeOnly.MinValue);
                ws.Cell("D13").Style.DateFormat.Format = "dd/MM/yyyy";
            }
            ws.Cell("D13").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell("D13").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            ws.Range("D13:D14").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // E – Monto contratado
            ws.Range("E13:E14").Merge();
            ws.Cell("E13").Value = data.Amount;
            ws.Cell("E13").Style.NumberFormat.Format  = $"\"{currencySymbol}\" #,##0.00";
            ws.Cell("E13").Style.Font.FontColor       = XLColor.FromHtml("#E26B0A");
            ws.Cell("E13").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            ws.Range("E13:E14").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // F – N° DOC
            ws.Range("F13:F14").Merge();
            ws.Cell("F13").Value = data.ContractDescription;
            ws.Cell("F13").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell("F13").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            ws.Range("F13:F14").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // G – Cheque / Recibo (vacío)
            ws.Range("G13:G14").Merge();
            ws.Range("G13:G14").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // H – % Adelanto
            ws.Range("H13:H14").Merge();
            ws.Cell("H13").Value = (data.AdvancePercentage ?? 0) / 100m;
            ws.Cell("H13").Style.NumberFormat.Format  = "0.00%";
            ws.Cell("H13").Style.Font.Bold            = true;
            ws.Cell("H13").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell("H13").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            ws.Range("H13:H14").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // I – Importe adelanto
            ws.Range("I13:I14").Merge();
            ws.Cell("I13").Value = advance;
            ws.Cell("I13").Style.NumberFormat.Format = "\"S/\" #,##0.00";
            ws.Cell("I13").Style.Alignment.Vertical  = XLAlignmentVerticalValues.Center;
            ws.Range("I13:I14").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // J – Saldo
            ws.Range("J13:J14").Merge();
            ws.Cell("J13").Value = saldo;
            ws.Cell("J13").Style.NumberFormat.Format  = $"\"{currencySymbol}\" #,##0.00";
            ws.Cell("J13").Style.Font.FontColor       = XLColor.FromHtml("#E26B0A");
            ws.Cell("J13").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            ws.Range("J13:J14").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // K – Inicio
            ws.Range("K13:K14").Merge();
            if (data.StartDate.HasValue)
            {
                ws.Cell("K13").Value = data.StartDate.Value.ToDateTime(TimeOnly.MinValue);
                ws.Cell("K13").Style.DateFormat.Format = "dd/MM/yyyy";
            }
            ws.Cell("K13").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell("K13").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            ws.Range("K13:K14").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // L – Fin
            ws.Range("L13:L14").Merge();
            if (data.EndDate.HasValue)
            {
                ws.Cell("L13").Value = data.EndDate.Value.ToDateTime(TimeOnly.MinValue);
                ws.Cell("L13").Style.DateFormat.Format = "dd/MM/yyyy";
            }
            ws.Cell("L13").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell("L13").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            ws.Range("L13:L14").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // M – Observación
            ws.Range("M13:M14").Merge();
            ws.Cell("M13").Value = data.PaymentMethodDescription;
            ws.Cell("M13").Style.Alignment.WrapText = true;
            ws.Cell("M13").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Range("M13:M14").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // ── Row 15: Totales ────────────────────────────────────────────────
            ws.Range("B15:D15").Merge();
            ws.Cell("B15").Value = "MONTO TOTAL CONTRATADO";
            ws.Range("B15:D15").Style.Font.Bold         = true;
            ws.Range("B15:D15").Style.Font.Underline    = XLFontUnderlineValues.Single;
            ws.Range("B15:D15").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range("B15:D15").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            ws.Range("B15:D15").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            ws.Cell("E15").Value = data.Amount;
            ws.Cell("E15").Style.NumberFormat.Format  = $"\"{currencySymbol}\" #,##0.00";
            ws.Cell("E15").Style.Font.Bold            = true;
            ws.Cell("E15").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            ws.Cell("E15").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            foreach (var col in new[] { "F", "G", "H", "I", "J" })
                ws.Cell($"{col}15").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            ws.Range("K15:L15").Merge();
            if (plazo > 0) ws.Cell("K15").Value = $"{plazo} días";
            ws.Range("K15:L15").Style.Font.Bold = true;
            ws.Range("K15:L15").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range("K15:L15").Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            ws.Range("K15:L15").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Cell("M15").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // ── Rows 17–18: Pie de garantías ──────────────────────────────────
            ws.Cell("B17").Value = "% DE RETENCIÓN FONDO DE GARANTIA:";
            ws.Cell("B17").Style.Font.Bold = true;
            ws.Cell("D17").Value = "5%";

            ws.Cell("B18").Value = "DEVOLUCIÓN DE FONDO DE GARANTÍA";
            ws.Cell("B18").Style.Font.Bold = true;
            ws.Range("D18:M18").Merge();
            ws.Cell("D18").Value =
                "360 días después de entregada la obra con acta Recepción Definitiva suscrita por el contratante y el cliente";
            ws.Range("D18:M18").Style.Alignment.WrapText = true;

            // ── Borde exterior general ─────────────────────────────────────────
            ws.Range("B2:N18").Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        }

        // ── Helpers de ruta SharePoint ───────────────────────────────────────

        private static string BuildSharePointPath(AdjudicacionPathDataDto data, AdjudicacionDocumentType documentType)
        {
            var project      = Sanitize(data.ProjectDescription);
            var company      = Sanitize($"{data.ContributorRuc} - {data.ContributorName}");
            var workItem  = Sanitize($"{data.ProjectSubContractorId} - {data.WorkItemDescription}");
            var subfolder = GetSubfolderName(documentType);
            return $"{project}/{company}/{workItem}/{subfolder}";
        }

        private static string GetSubfolderName(AdjudicacionDocumentType documentType) => documentType switch
        {
            AdjudicacionDocumentType.Contract           => "Contrato",
            AdjudicacionDocumentType.SummarySheet       => "Hoja Resumen",
            AdjudicacionDocumentType.Budget             => "Presupuesto",
            AdjudicacionDocumentType.Schedule           => "Cronograma",
            AdjudicacionDocumentType.AttachedQuotation  => "Cotizacion Adjunta",
            AdjudicacionDocumentType.ServiceOrder       => "Orden de Servicio",
            AdjudicacionDocumentType.InitialQuotation   => "Cotizaciones",
            AdjudicacionDocumentType.InitialComparative => "Comparativo",
            AdjudicacionDocumentType.PromissoryNote     => "Pagaré",
            AdjudicacionDocumentType.ScPackage          => "Paquete SC",
            AdjudicacionDocumentType.ScannedDoc1        => "Escaneados",
            AdjudicacionDocumentType.ScannedDoc2        => "Escaneados",
            AdjudicacionDocumentType.ScannedDoc3        => "Escaneados",
            _ => throw new ArgumentOutOfRangeException(nameof(documentType))
        };

        /// <summary>Elimina caracteres que SharePoint no acepta en nombres de carpeta.</summary>
        private static string Sanitize(string name)
        {
            var invalid = new HashSet<char> { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '#', '%' };
            var result = string.Concat(name.Select(c => invalid.Contains(c) ? '-' : c)).Trim();
            return result.Length > 60 ? result[..60].TrimEnd() : result;
        }

        private async Task<List<MailAttachmentDto>> DownloadAttachmentsAsync(
            List<ProjectSubContractorFileDto> files)
        {
            if (files == null || files.Count == 0)
                return new List<MailAttachmentDto>();

            var downloadTasks = files.Select(async file =>
            {
                try
                {
                    var bytes = await _sharePointService.DownloadFromSharePointAsync(file.FileUrl);
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
