using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.CursoModule.Application.Dtos;
using Abril_Backend.Features.CursoModule.Application.Interfaces;
using Abril_Backend.Features.CursoModule.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.CursoModule.Presentation.Controllers
{
    [ApiController]
    [Route("api/v1/curso")]
    [Authorize]
    public class CursoController : ControllerBase
    {
        private readonly ICursoRepository _repo;
        private readonly ILogger<CursoController> _logger;

        public CursoController(ICursoRepository repo, ILogger<CursoController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var roleIds = User.FindAll(ClaimTypes.Role)
                    .Select(c => int.TryParse(c.Value, out var id) ? id : (int?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToArray();

                var cursos = await _repo.GetActivosPorRolAsync(roleIds);
                return Ok(cursos.Select(MapToDto));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoController.GetAll"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("{id:int}/slides")]
        public async Task<IActionResult> GetSlides(int id)
        {
            try
            {
                var curso = await _repo.GetByIdAsync(id)
                    ?? throw new AbrilException("Curso no encontrado.", 404);

                var slides = await _repo.GetSlidesOrdenadasAsync(id);
                return Ok(slides.Select(MapSlideDtoSinRespuesta));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoController.GetSlides"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        private static CursoDto MapToDto(Curso c) => new()
        {
            Id = c.Id,
            Titulo = c.Titulo,
            Descripcion = c.Descripcion,
            CategoriaNombre = c.CategoriaNombre,
            RolDestino = c.RolDestino,
            NotaMinimaAprobacion = c.NotaMinimaAprobacion,
            Activo = c.Activo,
        };

        /// <summary>
        /// Mapea la slide quitando "respuestaCorrecta" (y cualquier dato de solución) de
        /// ConfiguracionJson antes de exponerla al frontend, para no filtrar la respuesta.
        /// </summary>
        private static CursoSlideDto MapSlideDtoSinRespuesta(CursoSlide s)
        {
            string configuracionLimpia = s.ConfiguracionJson;
            try
            {
                var node = JsonNode.Parse(s.ConfiguracionJson) as JsonObject;
                if (node != null && node.ContainsKey("respuestaCorrecta"))
                {
                    node.Remove("respuestaCorrecta");
                    configuracionLimpia = node.ToJsonString();
                }
            }
            catch (JsonException)
            {
                // Configuración corrupta: se expone tal cual está, la validación de integridad
                // del contenido del curso es responsabilidad del alta/edición de slides.
            }

            return new CursoSlideDto
            {
                Id = s.Id,
                CursoId = s.CursoId,
                Orden = s.Orden,
                TipoCodigo = s.TipoCodigo,
                EsEvaluable = s.EsEvaluable,
                Puntaje = s.Puntaje,
                ModoCorreccion = s.ModoCorreccion,
                ConfiguracionJson = configuracionLimpia,
            };
        }
    }
}
