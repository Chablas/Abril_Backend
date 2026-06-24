using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Presentation
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class InvoiceController : ControllerBase
    {
        private readonly IInvoiceService _service;
        private readonly ILogger<InvoiceController> _logger;

        public InvoiceController(IInvoiceService service, ILogger<InvoiceController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>Carga inicial: desplegables (proveedores, formas de pago) + primera página de facturas.</summary>
        [HttpGet("init")]
        public async Task<IActionResult> GetInit([FromQuery] string? search, [FromQuery] int? contributorId, [FromQuery] int page = 1)
        {
            try
            {
                var result = await _service.GetInit(new InvoiceFilterDto { Search = search, ContributorId = contributorId, Page = page < 1 ? 1 : page });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR INVOICE INIT: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] string? search, [FromQuery] int? contributorId, [FromQuery] int page = 1)
        {
            try
            {
                var result = await _service.GetPaged(new InvoiceFilterDto { Search = search, ContributorId = contributorId, Page = page });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR INVOICE PAGED: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] InvoiceCreateDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null) return Unauthorized(new { message = "Inicie sesión." });

                await _service.Create(dto, userId.Value);
                return Ok(new { message = "Factura registrada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR INVOICE CREATE: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Consulta de RUC a SUNAT para el modal de alta de proveedor.</summary>
        [HttpGet("ruc/{ruc}")]
        public async Task<IActionResult> GetByRuc(string ruc)
        {
            try
            {
                var result = await _service.GetByRuc(ruc);
                if (result is null)
                    return NotFound(new { message = "No se encontró información para el RUC proporcionado." });

                return Ok(result);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR INVOICE RUC: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Alta de un proveedor (contribuyente) desde el modal de consulta RUC.</summary>
        [HttpPost("supplier")]
        public async Task<IActionResult> CreateSupplier([FromBody] InvoiceSupplierCreateDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null) return Unauthorized(new { message = "Inicie sesión." });

                var supplier = await _service.CreateSupplier(dto, userId.Value);
                return Ok(supplier);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR INVOICE SUPPLIER CREATE: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : null;
        }
    }
}
