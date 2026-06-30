using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/presupuesto-materiales/catalogo")]
    [Authorize]
    public class CatalogoMaterialesController : ControllerBase
    {
        private readonly ICatalogoMaterialesService _service;

        public CatalogoMaterialesController(ICatalogoMaterialesService service)
        {
            _service = service;
        }

        /// <summary>Carga inicial/incremental del catálogo maestro (tipos, familias, items, alias) desde el CSV de materiales estandarizados.</summary>
        [HttpPost("seed")]
        public async Task<IActionResult> Seed([FromBody] SeedCatalogoRequestDto request)
        {
            try
            {
                var result = await _service.SeedCatalogoAsync(request);
                return Ok(result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }
    }
}
