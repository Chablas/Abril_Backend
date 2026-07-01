using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Helpers;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Interfaces;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.Sunat.Dtos;
using Abril_Backend.Shared.Services.Sunat.Interfaces;

namespace Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Services
{
    public class InvoiceService : IInvoiceService
    {
        private static readonly string[] AllowedExtensions = { ".pdf", ".png", ".jpg", ".jpeg", ".webp" };
        private const long MaxBytes = 25 * 1024 * 1024; // 25 MB

        private static readonly string[] SpanishMonths =
        {
            "ENERO", "FEBRERO", "MARZO", "ABRIL", "MAYO", "JUNIO",
            "JULIO", "AGOSTO", "SEPTIEMBRE", "OCTUBRE", "NOVIEMBRE", "DICIEMBRE"
        };

        private readonly IInvoiceRepository _repository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;
        private readonly ISunatService _sunatService;
        private readonly IGraphSharePointService _sharePointService;
        private readonly IManagerSignatureRepository _signatureRepository;
        private readonly IHttpClientFactory _httpClientFactory;

        public InvoiceService(
            IInvoiceRepository repository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver,
            ISunatService sunatService,
            IGraphSharePointService sharePointService,
            IManagerSignatureRepository signatureRepository,
            IHttpClientFactory httpClientFactory)
        {
            _repository = repository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
            _sunatService = sunatService;
            _sharePointService = sharePointService;
            _signatureRepository = signatureRepository;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<InvoiceInitDto> GetInit(InvoiceFilterDto filter)
        {
            // Un solo punto de carga: desplegables + primera página de la tabla.
            var suppliers = await _repository.GetSuppliers();
            var paymentForms = await _repository.GetPaymentForms();
            var abrilCompanies = await _repository.GetAbrilCompanies();
            var currencies = await _repository.GetCurrencies();
            var invoices = await _repository.GetPaged(filter);

            return new InvoiceInitDto
            {
                Suppliers = suppliers,
                PaymentForms = paymentForms,
                AbrilCompanies = abrilCompanies,
                Currencies = currencies,
                Invoices = invoices
            };
        }

        public Task<PagedResult<InvoiceDto>> GetPaged(InvoiceFilterDto filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return _repository.GetPaged(filter);
        }

        public Task<InvoiceDashboardDto> GetDashboard(InvoiceFilterDto filter)
            => _repository.GetDashboard(filter);

        public Task<List<InvoiceBlockGroupDto>> GetBlocks(InvoiceFilterDto filter)
            => _repository.GetBlocks(filter);

        public async Task<InvoiceDashboardInitDto> GetDashboardInit(InvoiceFilterDto filter)
        {
            var suppliers = await _repository.GetSuppliers();
            var paymentForms = await _repository.GetPaymentForms();
            var abrilCompanies = await _repository.GetAbrilCompanies();
            var currencies = await _repository.GetCurrencies();
            var dashboard = await _repository.GetDashboard(filter);

            return new InvoiceDashboardInitDto
            {
                Suppliers = suppliers,
                PaymentForms = paymentForms,
                AbrilCompanies = abrilCompanies,
                Currencies = currencies,
                Dashboard = dashboard
            };
        }

        public async Task<InvoiceDetailDto> GetDetail(int invoiceId)
        {
            return await _repository.GetDetail(invoiceId)
                ?? throw new AbrilException("La factura no existe.", 404);
        }

        public async Task Create(InvoiceCreateDto dto, int userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Serie))
                throw new AbrilException("La serie de la factura es obligatoria.");
            if (string.IsNullOrWhiteSpace(dto.Correlativo))
                throw new AbrilException("El correlativo de la factura es obligatorio.");
            if (!dto.Correlativo.Trim().All(char.IsDigit))
                throw new AbrilException("El correlativo debe ser numérico.");
            if (dto.ContributorId <= 0)
                throw new AbrilException("Debe seleccionar un proveedor.");
            if (dto.InvoicePaymentFormId <= 0)
                throw new AbrilException("Debe seleccionar una forma de pago.");
            if (string.IsNullOrWhiteSpace(dto.Description))
                throw new AbrilException("La descripción del bien o servicio es obligatoria.");
            if (dto.Total <= 0)
                throw new AbrilException("El total de la factura debe ser mayor a cero.");
            if (dto.AbrilContributorId <= 0)
                throw new AbrilException("Debe seleccionar la razón social de Abril.");
            if (dto.CurrencyId <= 0)
                throw new AbrilException("Debe seleccionar la moneda.");

            var destination = await _repository.GetActiveFolderDestination()
                ?? throw new AbrilException("No hay una carpeta de facturas configurada. Configúrela en Contabilidad → Configuración.");

            var documentUrl = await ResolveDocumentUrlAsync(
                dto.DocumentFile, destination.DriveId, destination.FolderId,
                dto.ContributorId, dto.AbrilContributorId,
                dto.Serie, dto.Correlativo, dto.IssueDate);

            await _repository.Create(dto, documentUrl, destination.Id, userId);
        }

        public async Task Update(InvoiceUpdateDto dto, int userId)
        {
            if (dto.InvoiceId <= 0)
                throw new AbrilException("Factura inválida.");
            if (string.IsNullOrWhiteSpace(dto.Serie))
                throw new AbrilException("La serie de la factura es obligatoria.");
            if (string.IsNullOrWhiteSpace(dto.Correlativo))
                throw new AbrilException("El correlativo de la factura es obligatorio.");
            if (!dto.Correlativo.Trim().All(char.IsDigit))
                throw new AbrilException("El correlativo debe ser numérico.");
            if (dto.ContributorId <= 0)
                throw new AbrilException("Debe seleccionar un proveedor.");
            if (dto.InvoicePaymentFormId <= 0)
                throw new AbrilException("Debe seleccionar una forma de pago.");
            if (string.IsNullOrWhiteSpace(dto.Description))
                throw new AbrilException("La descripción del bien o servicio es obligatoria.");
            if (dto.Total <= 0)
                throw new AbrilException("El total de la factura debe ser mayor a cero.");
            if (dto.AbrilContributorId <= 0)
                throw new AbrilException("Debe seleccionar la razón social de Abril.");
            if (dto.CurrencyId <= 0)
                throw new AbrilException("Debe seleccionar la moneda.");

            var destination = await _repository.GetActiveFolderDestination()
                ?? throw new AbrilException("No hay una carpeta de facturas configurada. Configúrela en Contabilidad → Configuración.");

            // Solo se vuelve a subir si se adjunta un documento nuevo; si no, se conserva el actual.
            var documentUrl = await ResolveDocumentUrlAsync(
                dto.DocumentFile, destination.DriveId, destination.FolderId,
                dto.ContributorId, dto.AbrilContributorId,
                dto.Serie, dto.Correlativo, dto.IssueDate);

            await _repository.Update(dto, documentUrl, destination.Id, userId);
        }

        /// <summary>
        /// Si hay documento adjunto, valida y lo sube a OneDrive en la ruta anidada
        /// AÑO / MES / dd-MM-yyyy / RAZÓN SOCIAL ABRIL / PROVEEDOR / N° FACTURA / archivo,
        /// devolviendo su webUrl. Si no hay documento, devuelve null.
        /// </summary>
        private async Task<string?> ResolveDocumentUrlAsync(
            IFormFile? file, string driveId, string folderId, int contributorId, int abrilContributorId,
            string serie, string correlativo, DateOnly issueDate)
        {
            // Validaciones de dependencias (también para mensajes claros aunque no haya archivo).
            var abrilName = await _repository.GetAbrilContributorName(abrilContributorId)
                ?? throw new AbrilException("La razón social de Abril seleccionada no es válida.");
            var supplierName = await _repository.GetContributorName(contributorId)
                ?? throw new AbrilException("El proveedor seleccionado no es válido.");

            if (file == null || file.Length == 0)
                return null;

            if (file.Length > MaxBytes)
                throw new AbrilException("El documento supera el tamaño máximo permitido (25 MB).");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                throw new AbrilException("Formato de documento no válido. Use PDF, PNG, JPG o WEBP.");

            var invoiceNumber = $"{serie.Trim()}-{correlativo.Trim()}";
            var currentFolderId = await EnsureInvoiceFolderPathAsync(
                driveId, folderId, abrilName, supplierName, invoiceNumber, issueDate);

            var fileName = $"{Sanitize(invoiceNumber)}{extension}";
            using var stream = file.OpenReadStream();
            var uploaded = await _sharePointService.UploadToOneDriveFolderAsync(
                driveId, currentFolderId, fileName, stream,
                contentType: file.ContentType ?? "application/octet-stream",
                autoRenameOnLock: true);

            return uploaded?.WebUrl;
        }

        /// <summary>
        /// Asegura la cadena de subcarpetas AÑO / MES / dd-MM-yyyy / RAZÓN SOCIAL ABRIL / PROVEEDOR /
        /// N° FACTURA bajo la carpeta raíz indicada y devuelve el itemId de la última (donde se sube
        /// el archivo). Reutilizado por el alta de facturas y por la firma.
        /// </summary>
        private async Task<string> EnsureInvoiceFolderPathAsync(
            string driveId, string rootFolderId, string abrilName, string supplierName,
            string invoiceNumber, DateOnly issueDate)
        {
            var segments = new[]
            {
                issueDate.Year.ToString(),
                SpanishMonths[issueDate.Month - 1],
                issueDate.ToString("dd-MM-yyyy"),
                Sanitize(abrilName),
                Sanitize(supplierName),
                Sanitize(invoiceNumber),
            };

            var currentFolderId = rootFolderId;
            foreach (var segment in segments)
                currentFolderId = await _sharePointService.EnsureChildFolderAsync(driveId, currentFolderId, segment);

            return currentFolderId;
        }

        public async Task<string> Sign(int invoiceId, int userId)
        {
            var detail = await _repository.GetDetail(invoiceId)
                ?? throw new AbrilException("La factura no existe.", 404);

            if (string.IsNullOrWhiteSpace(detail.DocumentUrl))
                throw new AbrilException("La factura no tiene un documento para firmar.");

            var signature = await _signatureRepository.GetActiveBytes()
                ?? throw new AbrilException("No hay una firma configurada. Configúrela en Contabilidad → Configuración → Firma de Gerente General.");

            var destination = await _repository.GetActiveFolderDestination()
                ?? throw new AbrilException("No hay una carpeta de facturas configurada.");

            byte[] original;
            try
            {
                original = await DownloadOriginalAsync(detail.DocumentUrl!);
            }
            catch (Exception)
            {
                throw new AbrilException("No se pudo descargar el documento original de la factura para firmarlo.");
            }

            byte[] signedPdf;
            try
            {
                signedPdf = SignaturePdfStamper.Stamp(original, signature.Bytes);
            }
            catch (Exception)
            {
                throw new AbrilException("No se pudo generar el documento firmado a partir del documento original.");
            }

            var folderId = await EnsureInvoiceFolderPathAsync(
                destination.DriveId, destination.FolderId,
                detail.AbrilContributorName ?? "SIN RAZON SOCIAL",
                detail.ContributorName, detail.InvoiceNumber, detail.IssueDate);

            var fileName = $"{Sanitize(detail.InvoiceNumber)}-FIRMADO.pdf";
            using var stream = new MemoryStream(signedPdf);
            var uploaded = await _sharePointService.UploadToOneDriveFolderAsync(
                destination.DriveId, folderId, fileName, stream,
                contentType: "application/pdf", autoRenameOnLock: true)
                ?? throw new AbrilException("No se pudo subir el documento firmado.", 500);

            if (string.IsNullOrWhiteSpace(uploaded.WebUrl))
                throw new AbrilException("No se pudo obtener la URL del documento firmado.", 500);

            await _repository.AttachSignedDocument(invoiceId, uploaded.WebUrl!, userId);
            return uploaded.WebUrl!;
        }

        /// <summary>
        /// Descarga el documento original de una factura a partir de su URL. Si la URL es de
        /// SharePoint/OneDrive se usa Graph (permisos de aplicación); en otro caso (p. ej. Azure Blob
        /// público) se descarga por HTTP directo.
        /// </summary>
        private async Task<byte[]> DownloadOriginalAsync(string documentUrl)
        {
            if (Uri.TryCreate(documentUrl, UriKind.Absolute, out var uri) &&
                uri.Host.Contains("sharepoint.com", StringComparison.OrdinalIgnoreCase))
            {
                return await _sharePointService.DownloadOneDriveFileByWebUrlAsync(documentUrl);
            }

            var client = _httpClientFactory.CreateClient();
            return await client.GetByteArrayAsync(documentUrl);
        }

        /// <summary>
        /// Reemplaza caracteres no permitidos por OneDrive en nombres de carpeta/archivo.
        /// OneDrive tampoco permite nombres que terminen en '.' (ni en espacio), así que se
        /// recortan los puntos/espacios finales (p. ej. un proveedor "ABRIL S.A." → "ABRIL S.A").
        /// </summary>
        private static string Sanitize(string name)
        {
            var invalid = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
            var cleaned = new string(name.Select(c => invalid.Contains(c) ? ' ' : c).ToArray())
                .Trim()
                .TrimEnd('.', ' ');
            return string.IsNullOrWhiteSpace(cleaned) ? "SIN NOMBRE" : cleaned;
        }

        public async Task<InvoiceImportResultDto> Import(List<InvoiceImportRowDto> rows, IFormFileCollection files, int userId)
        {
            if (rows == null || rows.Count == 0)
                throw new AbrilException("El Excel no contiene registros.");

            // Indexar archivos por nombre (case-insensitive).
            var fileByName = new Dictionary<string, IFormFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
                if (!fileByName.ContainsKey(f.FileName)) fileByName[f.FileName] = f;

            var container = _containerResolver.GetInvoicesContainerName();
            var docUrlByIndex = new Dictionary<int, string?>();

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (string.IsNullOrWhiteSpace(r.MatchedFileName) || !fileByName.TryGetValue(r.MatchedFileName, out var file) || file.Length == 0)
                    continue;

                if (file.Length > MaxBytes)
                    throw new AbrilException($"El archivo '{file.FileName}' supera el tamaño máximo permitido (25 MB).");

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                    throw new AbrilException($"Formato no válido en '{file.FileName}'. Use PDF, PNG, JPG o WEBP.");

                // Renombrado: nombrerazonsocial-numerofactura
                var serie = (r.Serie ?? "").Trim();
                var correlativo = (r.Correlativo ?? "").Trim();
                var number = serie.Length > 0 ? $"{serie}-{correlativo}" : correlativo;
                var baseName = Sanitize($"{(r.ProveedorName ?? "SIN PROVEEDOR").Trim()}-{number}");

                using var stream = file.OpenReadStream();
                var uploaded = await _fileStorageService.UploadFilesAsync(
                    new[] { (stream, $"{baseName}{extension}") },
                    container);
                docUrlByIndex[i] = uploaded.FirstOrDefault();
            }

            return await _repository.ImportInvoices(rows, docUrlByIndex, userId);
        }

        public async Task<string?> UploadDocument(int invoiceId, IFormFile file, int userId)
        {
            if (file == null || file.Length == 0)
                throw new AbrilException("Debe adjuntar un archivo.");
            if (file.Length > MaxBytes)
                throw new AbrilException("El documento supera el tamaño máximo permitido (25 MB).");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                throw new AbrilException("Formato de documento no válido. Use PDF, PNG, JPG o WEBP.");

            var detail = await _repository.GetDetail(invoiceId)
                ?? throw new AbrilException("La factura no existe.", 404);

            // Se sube a la misma Carpeta facturas configurada (OneDrive) que el alta/edición,
            // respetando la estructura AÑO / MES / dd-MM-yyyy / RAZÓN SOCIAL ABRIL / PROVEEDOR / N° FACTURA.
            var destination = await _repository.GetActiveFolderDestination()
                ?? throw new AbrilException("No hay una carpeta de facturas configurada. Configúrela en Contabilidad → Configuración.");

            var folderId = await EnsureInvoiceFolderPathAsync(
                destination.DriveId, destination.FolderId,
                detail.AbrilContributorName ?? "SIN RAZON SOCIAL",
                detail.ContributorName, detail.InvoiceNumber, detail.IssueDate);

            var fileName = $"{Sanitize(detail.InvoiceNumber)}{extension}";
            using var stream = file.OpenReadStream();
            var uploaded = await _sharePointService.UploadToOneDriveFolderAsync(
                destination.DriveId, folderId, fileName, stream,
                contentType: file.ContentType ?? "application/octet-stream",
                autoRenameOnLock: true);

            var url = uploaded?.WebUrl;
            if (!string.IsNullOrWhiteSpace(url))
                await _repository.AttachDocument(invoiceId, url!, userId);

            return url;
        }

        public Task<InvoiceSupplierDto> CreateSupplier(InvoiceSupplierCreateDto dto, int userId)
        {
            if (string.IsNullOrWhiteSpace(dto.ContributorRuc) || dto.ContributorRuc.Trim().Length != 11)
                throw new AbrilException("El RUC debe tener 11 dígitos.");
            if (string.IsNullOrWhiteSpace(dto.ContributorName))
                throw new AbrilException("La razón social es obligatoria.");

            return _repository.CreateSupplier(dto, userId);
        }

        public Task<SunatContributorDto?> GetByRuc(string ruc) => _sunatService.GetByRucAsync(ruc);
    }
}
