using System.Text.Json;
using Abril_Backend.Application.DTOs.ArquitecturaComercial;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Controllers
{
    [Authorize]
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

        [HttpPatch("actividades/{id:int}")]
        public async Task<IActionResult> PatchActividad(int id, [FromBody] Dictionary<string, JsonElement>? body)
        {
            try
            {
                if (body == null || body.Count == 0)
                    return BadRequest(new { message = "El body está vacío." });

                var result = await _service.PatchActividad(id, body);
                if (result == null)
                    return NotFound(new { message = "Actividad no encontrada." });

                return Ok(result);
            }
            catch (FormatException)
            {
                return BadRequest(new { message = "Formato de fecha inválido. Use YYYY-MM-DD." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost("actividades/reasignar-encargado")]
        public async Task<IActionResult> ReasignarEncargado([FromBody] ReasignarEncargadoDTO? body)
        {
            try
            {
                if (body == null || body.ProyectoId <= 0)
                    return BadRequest(new { message = "proyectoId es requerido." });

                var result = await _service.ReasignarEncargado(body.ProyectoId);
                if (result == null)
                    return NotFound(new { message = "Proyecto no encontrado." });

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("plantilla")]
        public async Task<IActionResult> GetPlantilla()
        {
            try
            {
                var result = await _service.GetPlantilla();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost("plantilla")]
        public async Task<IActionResult> CreatePlantilla([FromBody] CreatePlantillaDTO? body)
        {
            try
            {
                if (body == null)
                    return BadRequest(new { message = "El body está vacío." });

                var result = await _service.CreatePlantilla(body);
                return CreatedAtAction(nameof(GetPlantilla), new { id = result.Id }, result);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("plantilla/{id:int}")]
        public async Task<IActionResult> PatchPlantilla(int id, [FromBody] Dictionary<string, JsonElement>? body)
        {
            try
            {
                if (body == null || body.Count == 0)
                    return BadRequest(new { message = "El body está vacío." });

                var result = await _service.PatchPlantilla(id, body);
                if (result == null)
                    return NotFound(new { message = "Plantilla no encontrada." });

                return Ok(result);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("categorias")]
        public async Task<IActionResult> GetCategorias()
        {
            try { return Ok(await _service.GetCategorias()); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("especialidades")]
        public async Task<IActionResult> GetEspecialidades()
        {
            try { return Ok(await _service.GetEspecialidades()); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("etapas")]
        public async Task<IActionResult> GetEtapas()
        {
            try { return Ok(await _service.GetEtapas()); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("gantt")]
        public async Task<IActionResult> GetGantt(
            [FromQuery] int? proyectoId,
            [FromQuery] string? tipo,
            [FromQuery] string? etapa,
            [FromQuery] bool? soloActivas)
        {
            try
            {
                var result = await _service.GetGantt(proyectoId, tipo, etapa, soloActivas);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("proyectos/{id:int}")]
        public async Task<IActionResult> PatchProyecto(int id, [FromBody] PatchProyectoDTO? body)
        {
            try
            {
                if (body == null)
                    return BadRequest(new { message = "El body está vacío." });

                var result = await _service.PatchProyecto(id, body);
                if (result == null)
                    return NotFound(new { message = "Proyecto no encontrado." });

                return Ok(result);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost("actividades/generar")]
        public async Task<IActionResult> GenerarActividades([FromBody] GenerarActividadesDTO? body)
        {
            try
            {
                if (body == null || body.ProyectoId <= 0)
                    return BadRequest(new { message = "proyectoId es requerido." });

                var result = await _service.GenerarActividades(body.ProyectoId);
                if (result == null)
                    return NotFound(new { message = "Proyecto no encontrado." });

                return Ok(result);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
