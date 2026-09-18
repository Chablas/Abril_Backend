using System.Security.Claims;
using Abril_Backend.Features.SsomaModule.EppFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.EppFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.EppFeature.Presentation
{
    // Pedidos de EPP: snapshot congelado del catálogo al momento de generarlo, con código
    // correlativo, proyecto y quién lo generó — mismo permiso que el catálogo.
    [ApiController]
    [Route("api/v1/ssoma/epp/pedidos")]
    [Authorize]
    [RequireFeature("ssoma.gestion.epp")]
    public class EppPedidoController : ControllerBase
    {
        private readonly IEppPedidoService _service;

        public EppPedidoController(IEppPedidoService service)
        {
            _service = service;
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
        }

        [HttpGet]
        public async Task<IActionResult> GetPedidos()
        {
            try { return Ok(await _service.GetPedidosAsync()); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpGet("{pedidoId:int}")]
        public async Task<IActionResult> GetPedidoDetalle(int pedidoId)
        {
            try
            {
                var result = await _service.GetPedidoDetalleAsync(pedidoId);
                if (result == null) return NotFound(new { message = "Pedido no encontrado." });
                return Ok(result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost]
        public async Task<IActionResult> CreatePedido([FromBody] EppPedidoCreateDto dto)
        {
            try
            {
                var result = await _service.CreatePedidoAsync(dto, GetUserId());
                return CreatedAtAction(nameof(GetPedidoDetalle), new { pedidoId = result.Id }, result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }
    }
}
