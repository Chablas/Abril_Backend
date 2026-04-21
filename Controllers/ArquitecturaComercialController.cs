using Abril_Backend.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Controllers
{
    [ApiController]
    [Route("api/v1/arquitectura-comercial")]
    public class ArquitecturaComercialController : ControllerBase
    {
        private readonly IArquitecturaComercialService _service;

        public ArquitecturaComercialController(IArquitecturaComercialService service)
        {
            _service = service;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard(
            [FromQuery] string? semana,
            [FromQuery] string? mes,
            [FromQuery] int? proyectoId)
        {
            try
            {
                var result = await _service.GetDashboardData(semana, mes, proyectoId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters()
        {
            try
            {
                var result = await _service.GetFilters();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("proyectos-con-actividades")]
        public async Task<IActionResult> GetProyectosConActividades()
        {
            try
            {
                var result = await _service.GetProyectosConActividades();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("supervisores-ac")]
        public async Task<IActionResult> GetSupervisoresAc()
        {
            try
            {
                var result = await _service.GetSupervisoresAc();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("actividades")]
        public async Task<IActionResult> GetActividades(
            [FromQuery] int? proyectoId,
            [FromQuery] string? tipo,
            [FromQuery] int? etapaId,
            [FromQuery] string? search,
            [FromQuery] bool? soloActivas,
            [FromQuery] int pagina = 1,
            [FromQuery] int porPagina = 100)
        {
            try
            {
                var result = await _service.GetActividades(proyectoId, tipo, etapaId, search, soloActivas, pagina, porPagina);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
