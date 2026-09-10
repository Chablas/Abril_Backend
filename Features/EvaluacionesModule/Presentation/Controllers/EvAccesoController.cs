using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.Evaluaciones.Presentation.Controllers
{
    // Expone el acceso real por PUESTO a los distintos flujos de evaluaciones, para
    // que el frontend oculte pestañas a las que el backend igual respondería 403 (ver
    // EvJefeSsomaController/EvSupervisorContratistaController/EvGestionSsomaController/
    // EvPrevencionistaController, que ya resuelven por puesto, no por featureKey).
    [ApiController]
    [Route("api/v1/evaluaciones/mi-acceso")]
    [Authorize]
    public class EvAccesoController : ControllerBase
    {
        private readonly IEvGestionSsomaRepository _repo;
        private readonly ILogger<EvAccesoController> _logger;

        public EvAccesoController(IEvGestionSsomaRepository repo, ILogger<EvAccesoController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        private int GetUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get()
        {
            try
            {
                var userId = GetUserId();
                var esJefeSsoma = await _repo.EsJefeSsomaAsync(userId);
                var categoria = await _repo.ObtenerCategoriaPuestoAsync(userId);

                return Ok(new EvAccesoDto
                {
                    EsJefeSsoma = esJefeSsoma,
                    EsCoordinadorSsoma = categoria == CategoriaIds.CoordinadorSsoma,
                    EsPrevencionista = categoria == CategoriaIds.Prevencionista,
                });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvAccesoController.Get"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
