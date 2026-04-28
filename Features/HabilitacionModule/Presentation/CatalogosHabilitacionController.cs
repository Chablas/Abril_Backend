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
    }
}
