using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Interfaces;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Interfaces;
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

        public InvoiceService(
            IInvoiceRepository repository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver,
            ISunatService sunatService,
            IGraphSharePointService sharePointService)
        {
            _repository = repository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
            _sunatService = sunatService;
            _sharePointService = sharePointService;
        }

        public async Task<InvoiceInitDto> GetInit(InvoiceFilterDto filter)
        {
            // Un solo punto de carga: desplegables + primera página de la tabla.
            var suppliers = await _repository.GetSuppliers();
            var paymentForms = await _repository.GetPaymentForms();
            var abrilCompanies = await _repository.GetAbrilCompanies();
            var folders = await _repository.GetFolderOptions();
            var currencies = await _repository.GetCurrencies();
            var invoices = await _repository.GetPaged(filter);

            return new InvoiceInitDto
            {
                Suppliers = suppliers,
                PaymentForms = paymentForms,
                AbrilCompanies = abrilCompanies,
                Folders = folders,
                Currencies = currencies,
                Invoices = invoices
            };
        }

        public Task<PagedResult<InvoiceDto>> GetPaged(InvoiceFilterDto filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return _repository.GetPaged(filter);
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
            if (dto.InvoiceFolderId <= 0)
                throw new AbrilException("Debe seleccionar la carpeta donde se guardará la factura.");
            if (dto.CurrencyId <= 0)
                throw new AbrilException("Debe seleccionar la moneda.");

            var documentUrl = await ResolveDocumentUrlAsync(
                dto.DocumentFile, dto.InvoiceFolderId, dto.ContributorId, dto.AbrilContributorId,
                dto.Serie, dto.Correlativo, dto.IssueDate);

            await _repository.Create(dto, documentUrl, userId);
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
            if (dto.InvoiceFolderId <= 0)
                throw new AbrilException("Debe seleccionar la carpeta donde se guardará la factura.");
            if (dto.CurrencyId <= 0)
                throw new AbrilException("Debe seleccionar la moneda.");

            // Solo se vuelve a subir si se adjunta un documento nuevo; si no, se conserva el actual.
            var documentUrl = await ResolveDocumentUrlAsync(
                dto.DocumentFile, dto.InvoiceFolderId, dto.ContributorId, dto.AbrilContributorId,
                dto.Serie, dto.Correlativo, dto.IssueDate);

            await _repository.Update(dto, documentUrl, userId);
        }

        /// <summary>
        /// Si hay documento adjunto, valida y lo sube a OneDrive en la ruta anidada
        /// AÑO / MES / dd-MM-yyyy / RAZÓN SOCIAL ABRIL / PROVEEDOR / N° FACTURA / archivo,
        /// devolviendo su webUrl. Si no hay documento, devuelve null.
        /// </summary>
        private async Task<string?> ResolveDocumentUrlAsync(
            IFormFile? file, int invoiceFolderId, int contributorId, int abrilContributorId,
            string serie, string correlativo, DateOnly issueDate)
        {
            // Validaciones de dependencias (también para mensajes claros aunque no haya archivo).
            var abrilName = await _repository.GetAbrilContributorName(abrilContributorId)
                ?? throw new AbrilException("La razón social de Abril seleccionada no es válida.");
            var supplierName = await _repository.GetContributorName(contributorId)
                ?? throw new AbrilException("El proveedor seleccionado no es válido.");
            var destination = await _repository.GetFolderDestination(invoiceFolderId)
                ?? throw new AbrilException("La carpeta seleccionada no existe o está inactiva.");

            if (file == null || file.Length == 0)
                return null;

            if (file.Length > MaxBytes)
                throw new AbrilException("El documento supera el tamaño máximo permitido (25 MB).");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                throw new AbrilException("Formato de documento no válido. Use PDF, PNG, JPG o WEBP.");

            var invoiceNumber = $"{serie.Trim()}-{correlativo.Trim()}";
            var segments = new[]
            {
                issueDate.Year.ToString(),
                SpanishMonths[issueDate.Month - 1],
                issueDate.ToString("dd-MM-yyyy"),
                Sanitize(abrilName),
                Sanitize(supplierName),
                Sanitize(invoiceNumber),
            };

            var currentFolderId = destination.FolderId;
            foreach (var segment in segments)
                currentFolderId = await _sharePointService.EnsureChildFolderAsync(destination.DriveId, currentFolderId, segment);

            var fileName = $"{Sanitize(invoiceNumber)}{extension}";
            using var stream = file.OpenReadStream();
            var uploaded = await _sharePointService.UploadToOneDriveFolderAsync(
                destination.DriveId, currentFolderId, fileName, stream,
                contentType: file.ContentType ?? "application/octet-stream",
                autoRenameOnLock: true);

            return uploaded?.WebUrl;
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
