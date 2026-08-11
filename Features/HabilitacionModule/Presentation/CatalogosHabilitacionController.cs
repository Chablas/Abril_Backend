using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Catalogos;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Shared.Models;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
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
        private readonly IJefePersonalizadoService _jefePersonalizado;
        private readonly ILogger<CatalogosHabilitacionController> _logger;

        public CatalogosHabilitacionController(
            ICatalogosHabilitacionRepository repo,
            IJefePersonalizadoService jefePersonalizado,
            ILogger<CatalogosHabilitacionController> logger)
        {
            _repo = repo;
            _jefePersonalizado = jefePersonalizado;
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

        /// <summary>
        /// Árbol de áreas (area_scope) para los desplegables en cascada del formulario de
        /// trabajadores, con la equivalencia legacy area/subárea/jefatura y el revisor que le
        /// tocaría al trabajador ya resueltos por nodo. Una sola llamada: al cambiar de área el
        /// formulario no vuelve al servidor.
        /// </summary>
        [HttpGet("areas-arbol")]
        public async Task<IActionResult> GetAreaArbol()
        {
            try
            {
                return Ok(await _repo.GetAreaArbolAsync());
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosHabilitacionController.GetAreaArbol"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>
        /// Trabajadores que pueden ser jefe (los que tienen correo corporativo @abril.pe),
        /// para el desplegable que aparece al marcar "Jefe personalizado" en el formulario de
        /// trabajadores. No exige que tengan usuario del sistema.
        /// </summary>
        [HttpGet("jefes")]
        public async Task<IActionResult> GetJefes()
        {
            try
            {
                return Ok(await _jefePersonalizado.GetCandidatosAsync());
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosHabilitacionController.GetJefes"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>
        /// Catálogo Obra / Staff / Oficina Central, para el desplegable del formulario
        /// de trabajadores (workers.obra_oficina_staff_id).
        /// </summary>
        [HttpGet("obra-oficina-staff")]
        public async Task<IActionResult> GetObraOficinaStaff()
        {
            try
            {
                return Ok(await _repo.GetObraOficinaStaffAsync());
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosHabilitacionController.GetObraOficinaStaff"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
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
                    Id = x.CategoriaId,
                    Nombre = x.Nombre
                }).ToList();
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosHabilitacionController.GetCategorias"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>
        /// Catálogo único de puestos. Reemplaza al viejo endpoint "ocupaciones": la
        /// ocupación dejó de existir como campo aparte y su data se fusionó acá.
        /// </summary>
        [HttpGet("puestos")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPuestos()
        {
            try
            {
                var items = await _repo.GetPuestosAsync();
                var result = items.Select(x => new PuestoDto
                {
                    Id = x.PuestoId,
                    Nombre = x.Nombre,
                    CategoriaId = x.CategoriaId
                }).ToList();
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosHabilitacionController.GetPuestos"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Configuración → Categorías y Puestos ─────────────────────

        /// <summary>
        /// Carga inicial de la pantalla de configuración: categorías + puestos en una
        /// sola petición (la pantalla necesita ambas listas de entrada, porque cada
        /// puesto muestra y elige su categoría).
        /// </summary>
        [HttpGet("admin")]
        public async Task<IActionResult> GetCatalogosAdmin()
        {
            try
            {
                var categorias = await _repo.GetCategoriasTodasAsync();
                var puestos = await _repo.GetPuestosTodosAsync();
                return Ok(new CatalogosAdminDto
                {
                    Categorias = categorias.Select(MapCategoria).ToList(),
                    Puestos = MapPuestos(puestos, categorias)
                });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en GetCatalogosAdmin"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Categorías CRUD ──────────────────────────────────────────

        [HttpGet("categorias/admin")]
        public async Task<IActionResult> GetCategoriasAdmin()
        {
            try
            {
                var items = await _repo.GetCategoriasTodasAsync();
                return Ok(items.Select(MapCategoria).ToList());
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en GetCategoriasAdmin"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("categorias")]
        public async Task<IActionResult> CrearCategoria([FromBody] CatNombreRequest req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Nombre))
                    return BadRequest(new { message = "El nombre es requerido." });
                var cat = await _repo.CrearCategoriaAsync(req.Nombre.Trim());
                return Ok(new CatCategoriaAdminDto { Id = cat.CategoriaId, Nombre = cat.Nombre, Orden = cat.Orden, Activo = cat.Active });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CrearCategoria"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("categorias/{id}")]
        public async Task<IActionResult> ActualizarCategoria(int id, [FromBody] CatNombreRequest req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Nombre))
                    return BadRequest(new { message = "El nombre es requerido." });
                var cat = await _repo.ActualizarCategoriaAsync(id, req.Nombre.Trim());
                return Ok(new CatCategoriaAdminDto { Id = cat.CategoriaId, Nombre = cat.Nombre, Orden = cat.Orden, Activo = cat.Active });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ActualizarCategoria"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("categorias/{id}/toggle")]
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

        // ── Puestos CRUD ─────────────────────────────────────────────

        [HttpGet("puestos/admin")]
        public async Task<IActionResult> GetPuestosAdmin()
        {
            try
            {
                var puestos = await _repo.GetPuestosTodosAsync();
                var categorias = await _repo.GetCategoriasTodasAsync();
                return Ok(MapPuestos(puestos, categorias));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en GetPuestosAdmin"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("puestos")]
        public async Task<IActionResult> CrearPuesto([FromBody] PuestoUpsertRequest req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Nombre))
                    return BadRequest(new { message = "El nombre es requerido." });
                var puesto = await _repo.CrearPuestoAsync(req.Nombre.Trim(), req.CategoriaId);
                return Ok(new PuestoAdminDto { Id = puesto.PuestoId, Nombre = puesto.Nombre, CategoriaId = puesto.CategoriaId, Orden = puesto.Orden, Activo = puesto.Active });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CrearPuesto"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("puestos/{id}")]
        public async Task<IActionResult> ActualizarPuesto(int id, [FromBody] PuestoUpsertRequest req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Nombre))
                    return BadRequest(new { message = "El nombre es requerido." });
                var puesto = await _repo.ActualizarPuestoAsync(id, req.Nombre.Trim(), req.CategoriaId);
                return Ok(new PuestoAdminDto { Id = puesto.PuestoId, Nombre = puesto.Nombre, CategoriaId = puesto.CategoriaId, Orden = puesto.Orden, Activo = puesto.Active });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ActualizarPuesto"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("puestos/{id}/toggle")]
        public async Task<IActionResult> TogglePuesto(int id, [FromBody] CatToggleRequest req)
        {
            try
            {
                await _repo.TogglePuestoAsync(id, req.Activo);
                return Ok();
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en TogglePuesto"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Mapeos compartidos ───────────────────────────────────────

        private static CatCategoriaAdminDto MapCategoria(Categoria x) => new()
        {
            Id = x.CategoriaId, Nombre = x.Nombre, Orden = x.Orden, Activo = x.Active
        };

        /// <summary>
        /// Puestos con el nombre de su categoría resuelto en memoria (evita un join/consulta
        /// extra: las categorías ya se trajeron para la misma respuesta).
        /// </summary>
        private static List<PuestoAdminDto> MapPuestos(List<Puesto> puestos, List<Categoria> categorias)
        {
            var nombrePorId = categorias.ToDictionary(c => c.CategoriaId, c => c.Nombre);
            return puestos.Select(x => new PuestoAdminDto
            {
                Id = x.PuestoId,
                Nombre = x.Nombre,
                CategoriaId = x.CategoriaId,
                CategoriaNombre = x.CategoriaId.HasValue && nombrePorId.TryGetValue(x.CategoriaId.Value, out var n) ? n : null,
                Orden = x.Orden,
                Activo = x.Active
            }).ToList();
        }
    }
}
