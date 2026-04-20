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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IGraphUserService _graphUserService;
        private readonly IGraphSharePointService _sharePointService;

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
            IGraphUserService graphUserService,
            IGraphSharePointService sharePointService)
        {
            _projectSubContractorRepository = projectSubContractorRepository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
            _projectRepository = projectRepository;
            _delegatedMailService = delegatedMailService;
            _httpClientFactory = httpClientFactory;
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

        public async Task SaveDates(int projectSubContractorId, UpdateDatesDTO dto, int userId)
        {
            await _projectSubContractorRepository.SaveDates(projectSubContractorId, dto, userId);
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
                CompanyRuc             = data.CompanyRuc,
                CompanyName            = data.CompanyName,
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
                { "{{EMPRESA}}",        data.CompanyName },
                { "{{PROYECTO}}",       data.ProjectDescription },
                { "{{MONTO}}",          $"{currencySymbol} {data.Amount:N2}" },
                { "{{FECHA_INICIO}}",   data.StartDate?.ToString("dd/MM/yyyy") ?? "" },
                { "{{FECHA_FIN}}",      data.EndDate?.ToString("dd/MM/yyyy")   ?? "" },
                { "{{RUC}}",            data.CompanyRuc },
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
                CompanyRuc             = data.CompanyRuc,
                CompanyName            = data.CompanyName,
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
            ws.Cell("C6").Value = data.CompanyName;

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
            var project   = Sanitize(data.ProjectDescription);
            var company   = Sanitize($"{data.CompanyRuc} - {data.CompanyName}");
            var workItem  = Sanitize($"{data.ProjectSubContractorId} - {data.WorkItemDescription}");
            var subfolder = GetSubfolderName(documentType);
            return $"{project}/{company}/{workItem}/{subfolder}";
        }

        private static string GetSubfolderName(AdjudicacionDocumentType documentType) => documentType switch
        {
            AdjudicacionDocumentType.Contract          => "Contrato",
            AdjudicacionDocumentType.SummarySheet      => "Hoja Resumen",
            AdjudicacionDocumentType.Budget            => "Presupuesto",
            AdjudicacionDocumentType.Schedule          => "Cronograma",
            AdjudicacionDocumentType.AttachedQuotation => "Cotizacion Adjunta",
            AdjudicacionDocumentType.ServiceOrder      => "Orden de Servicio",
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
