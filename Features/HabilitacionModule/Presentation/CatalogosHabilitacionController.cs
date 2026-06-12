using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Catalogos;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.Habilitacion.Presentation
{
    [ApiController]
    [Route("api/v1/habilitacion/catalogos")]
    [Authorize]
    public class CatalogosHabilitacionController : ControllerBase
    {
        private readonly ICatalogosHabilitacionRepository _repo;
        private readonly ILogger<CatalogosHabilitacionController> _logger;

        public CatalogosHabilitacionController(
            ICatalogosHabilitacionRepository repo,
            ILogger<CatalogosHabilitacionController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        [HttpGet("items-trabajador")]
        public async Task<IActionResult> GetItemsTrabajador()
        {
            try
            {
                var items = await _repo.GetItemsTrabajadorAsync();
                var result = items.Select(x => new SsItemTrabajadorDto
                {
                    Id = x.Id,
                    Nombre = x.Nombre,
                    AplicaA = x.AplicaA,
                    Responsable = x.Responsable,
                    RequiereVigencia = x.RequiereVigencia,
                    EsSctrVidaley = x.EsSctrVidaley,
                    Orden = x.Orden,
                    Activo = x.Activo
                }).ToList();
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosHabilitacionController.GetItemsTrabajador"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("items-empresa")]
        public async Task<IActionResult> GetItemsEmpresa()
        {
            try
            {
                var items = await _repo.GetItemsEmpresaAsync();
                var result = items.Select(x => new SsItemEmpresaDto
                {
                    Id = x.Id,
                    Nombre = x.Nombre,
                    Responsable = x.Responsable,
                    Orden = x.Orden,
                    RequiereVigencia = x.RequiereVigencia,
                    Activo = x.Activo
                }).ToList();
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosHabilitacionController.GetItemsEmpresa"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("items-equipo")]
        public async Task<IActionResult> GetItemsEquipo()
        {
            try
            {
                var items = await _repo.GetItemsEquipoAsync();
                var result = items.Select(x => new SsItemEquipoDto
                {
                    Id = x.Id,
                    Nombre = x.Nombre,
                    RequiereVigencia = x.RequiereVigencia,
                    Orden = x.Orden,
                    Activo = x.Activo
                }).ToList();
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosHabilitacionController.GetItemsEquipo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("criterios")]
        public async Task<IActionResult> GetCriterios()
        {
            try
            {
                var items = await _repo.GetCriteriosEvaluacionAsync();
                var result = items.Select(x => new SsCriterioDto
                {
                    Id = x.Id,
                    Criterio = x.Criterio,
                    Orden = x.Orden,
                    Activo = x.Activo
                }).ToList();
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosHabilitacionController.GetCriterios"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("areas")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAreas()
        {
            try
            {
                var areas = await _repo.GetAreasAsync();
                var result = areas.Select(a => new AreaSimpleDto { Area = a }).ToList();
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosHabilitacionController.GetAreas"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("subareas")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSubareas([FromQuery] string? area)
        {
            try
            {
                var items = await _repo.GetSubareasAsync(area);
                var result = items.Select(x => new CatSubareaDto
                {
                    Id = x.Id,
                    Subarea = x.Subarea,
                    Area = x.Area,
                    Jefatura = x.Jefatura
                }).ToList();
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosHabilitacionController.GetSubareas"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("categorias")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategorias()
        {
            try
            {
                var items = await _repo.GetCategoriasAsync();
                var result = items.Select(x => new CatCategoriaDto
                {
                    Id = x.Id,
                    Nombre = x.Nombre
                }).ToList();
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosHabilitacionController.GetCategorias"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("ocupaciones")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOcupaciones()
        {
            try
            {
                var items = await _repo.GetOcupacionesAsync();
                var result = items.Select(x => new CatOcupacionDto
                {
                    Id = x.Id,
                    Nombre = x.Nombre
                }).ToList();
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosHabilitacionController.GetOcupaciones"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Categorías CRUD ──────────────────────────────────────────

        [HttpGet("categorias/admin")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategoriasAdmin()
        {
            try
            {
                var items = await _repo.GetCategoriasTodasAsync();
                var result = items.Select(x => new CatCategoriaAdminDto
                {
                    Id = x.Id, Nombre = x.Nombre, Orden = x.Orden, Activo = x.Activo
                }).ToList();
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en GetCategoriasAdmin"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("categorias")]
        [AllowAnonymous]
        public async Task<IActionResult> CrearCategoria([FromBody] CatNombreRequest req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Nombre))
                    return BadRequest(new { message = "El nombre es requerido." });
                var cat = await _repo.CrearCategoriaAsync(req.Nombre.Trim());
                return Ok(new CatCategoriaAdminDto { Id = cat.Id, Nombre = cat.Nombre, Orden = cat.Orden, Activo = cat.Activo });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CrearCategoria"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("categorias/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> ActualizarCategoria(int id, [FromBody] CatNombreRequest req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Nombre))
                    return BadRequest(new { message = "El nombre es requerido." });
                var cat = await _repo.ActualizarCategoriaAsync(id, req.Nombre.Trim());
                return Ok(new CatCategoriaAdminDto { Id = cat.Id, Nombre = cat.Nombre, Orden = cat.Orden, Activo = cat.Activo });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ActualizarCategoria"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("categorias/{id}/toggle")]
        [AllowAnonymous]
        public async Task<IActionResult> ToggleCategoria(int id, [FromBody] CatToggleRequest req)
        {
            try
            {
                await _repo.ToggleCategoriaAsync(id, req.Activo);
                return Ok();
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ToggleCategoria"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Ocupaciones CRUD ─────────────────────────────────────────

        [HttpGet("ocupaciones/admin")]
        [AllowAnonymous]
        public async Task<IActionResult> GetOcupacionesAdmin()
        {
            try
            {
                var items = await _repo.GetOcupacionesTodasAsync();
                var result = items.Select(x => new CatOcupacionAdminDto
                {
                    Id = x.Id, Nombre = x.Nombre, Orden = x.Orden, Activo = x.Activo
                }).ToList();
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en GetOcupacionesAdmin"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("ocupaciones")]
        [AllowAnonymous]
        public async Task<IActionResult> CrearOcupacion([FromBody] CatNombreRequest req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Nombre))
                    return BadRequest(new { message = "El nombre es requerido." });
                var ocu = await _repo.CrearOcupacionAsync(req.Nombre.Trim());
                return Ok(new CatOcupacionAdminDto { Id = ocu.Id, Nombre = ocu.Nombre, Orden = ocu.Orden, Activo = ocu.Activo });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CrearOcupacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("ocupaciones/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> ActualizarOcupacion(int id, [FromBody] CatNombreRequest req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Nombre))
                    return BadRequest(new { message = "El nombre es requerido." });
                var ocu = await _repo.ActualizarOcupacionAsync(id, req.Nombre.Trim());
                return Ok(new CatOcupacionAdminDto { Id = ocu.Id, Nombre = ocu.Nombre, Orden = ocu.Orden, Activo = ocu.Activo });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ActualizarOcupacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("ocupaciones/{id}/toggle")]
        [AllowAnonymous]
        public async Task<IActionResult> ToggleOcupacion(int id, [FromBody] CatToggleRequest req)
        {
            try
            {
                await _repo.ToggleOcupacionAsync(id, req.Activo);
                return Ok();
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ToggleOcupacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
