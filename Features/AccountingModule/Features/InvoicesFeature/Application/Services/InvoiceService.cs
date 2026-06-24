using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Interfaces;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Sunat.Dtos;
using Abril_Backend.Shared.Services.Sunat.Interfaces;

namespace Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Services
{
    public class InvoiceService : IInvoiceService
    {
        private static readonly string[] AllowedExtensions = { ".pdf", ".png", ".jpg", ".jpeg", ".webp" };
        private const long MaxBytes = 25 * 1024 * 1024; // 25 MB

        private readonly IInvoiceRepository _repository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;
        private readonly ISunatService _sunatService;

        public InvoiceService(
            IInvoiceRepository repository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver,
            ISunatService sunatService)
        {
            _repository = repository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
            _sunatService = sunatService;
        }

        public async Task<InvoiceInitDto> GetInit(InvoiceFilterDto filter)
        {
            // Un solo punto de carga: desplegables + primera página de la tabla.
            // Se ejecutan secuencialmente (cada repo abre su propio contexto) para no
            // multiplicar conexiones a la BD.
            var suppliers = await _repository.GetSuppliers();
            var paymentForms = await _repository.GetPaymentForms();
            var invoices = await _repository.GetPaged(filter);

            return new InvoiceInitDto
            {
                Suppliers = suppliers,
                PaymentForms = paymentForms,
                Invoices = invoices
            };
        }

        public Task<PagedResult<InvoiceDto>> GetPaged(InvoiceFilterDto filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return _repository.GetPaged(filter);
        }

        public async Task Create(InvoiceCreateDto dto, int userId)
        {
            if (string.IsNullOrWhiteSpace(dto.InvoiceNumber))
                throw new AbrilException("El número de factura es obligatorio.");
            if (dto.ContributorId <= 0)
                throw new AbrilException("Debe seleccionar un proveedor.");
            if (dto.InvoicePaymentFormId <= 0)
                throw new AbrilException("Debe seleccionar una forma de pago.");
            if (string.IsNullOrWhiteSpace(dto.Description))
                throw new AbrilException("La descripción del bien o servicio es obligatoria.");
            if (dto.Total <= 0)
                throw new AbrilException("El total de la factura debe ser mayor a cero.");

            string? documentUrl = null;
            if (dto.DocumentFile != null && dto.DocumentFile.Length > 0)
            {
                if (dto.DocumentFile.Length > MaxBytes)
                    throw new AbrilException("El documento supera el tamaño máximo permitido (25 MB).");

                var extension = Path.GetExtension(dto.DocumentFile.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                    throw new AbrilException("Formato de documento no válido. Use PDF, PNG, JPG o WEBP.");

                var container = _containerResolver.GetInvoicesContainerName();
                using var stream = dto.DocumentFile.OpenReadStream();
                var uploaded = await _fileStorageService.UploadFilesAsync(
                    new[] { (stream, $"{Guid.NewGuid()}{extension}") },
                    container);
                documentUrl = uploaded.First();
            }

            await _repository.Create(dto, documentUrl, userId);
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
