using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.CursoModule.Application.Dtos;
using Abril_Backend.Features.CursoModule.Application.Interfaces;
using Abril_Backend.Features.CursoModule.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.CursoModule.Presentation.Controllers
{
    [ApiController]
    [Route("api/v1/curso")]
    [Authorize]
    public class CursoController : ControllerBase
    {
        private readonly ICursoRepository _repo;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;
        private readonly ILogger<CursoController> _logger;

        public CursoController(
            ICursoRepository repo,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver,
            ILogger<CursoController> logger)
        {
            _repo = repo;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
            _logger = logger;
        }

        /// <summary>Sube una imagen para usar en el contenido de una slide (portada, tarjetas, galería, etc.)
        /// y devuelve su URL pública. No queda asociada a ningún curso/slide todavía: el frontend pega la
        /// URL devuelta dentro del ConfiguracionJson de la slide que corresponda.</summary>
        [HttpPost("imagenes")]
        public async Task<IActionResult> SubirImagen(IFormFile archivo)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                    throw new AbrilException("El archivo está vacío.", 400);

                var container = _containerResolver.GetCursoImagenesContainerName();
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";

                await using var stream = archivo.OpenReadStream();
                var urls = await _fileStorageService.UploadFilesAsync(
                    new[] { (Stream: (Stream)stream, FileName: fileName) }, container);

                return Ok(new { url = urls.First() });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoController.SubirImagen"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("admin")]
        public async Task<IActionResult> GetTodos()
        {
            try
            {
                var cursos = await _repo.GetTodosAsync();
                return Ok(cursos.Select(MapToDto));
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoController.GetTodos"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost]
        public async Task<IActionResult> CrearCurso([FromBody] CursoUpsertDto dto)
        {
            try
            {
                var curso = await _repo.CreateCursoAsync(new Curso
                {
                    Titulo = dto.Titulo,
                    Descripcion = dto.Descripcion,
                    CategoriaNombre = dto.CategoriaNombre,
                    RolDestino = dto.RolDestino,
                    NotaMinimaAprobacion = dto.NotaMinimaAprobacion,
                    Activo = dto.Activo,
                    ColorTema = dto.ColorTema,
                });
                return Ok(MapToDto(curso));
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoController.CrearCurso"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarCurso(int id, [FromBody] CursoUpsertDto dto)
        {
            try
            {
                await _repo.UpdateCursoAsync(id, new Curso
                {
                    Titulo = dto.Titulo,
                    Descripcion = dto.Descripcion,
                    CategoriaNombre = dto.CategoriaNombre,
                    RolDestino = dto.RolDestino,
                    NotaMinimaAprobacion = dto.NotaMinimaAprobacion,
                    Activo = dto.Activo,
                    ColorTema = dto.ColorTema,
                });
                return Ok();
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoController.ActualizarCurso"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("{id:int}/slides")]
        public async Task<IActionResult> CrearSlide(int id, [FromBody] CursoSlideUpsertDto dto)
        {
            try
            {
                var slide = await _repo.CreateSlideAsync(new CursoSlide
                {
                    CursoId = id,
                    Orden = dto.Orden,
                    TipoCodigo = dto.TipoCodigo,
                    EsEvaluable = dto.EsEvaluable,
                    Puntaje = dto.Puntaje,
                    ModoCorreccion = dto.ModoCorreccion,
                    ConfiguracionJson = dto.ConfiguracionJson,
                });
                return Ok(MapSlideDtoCompleto(slide));
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoController.CrearSlide"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("slides/{slideId:int}")]
        public async Task<IActionResult> ActualizarSlide(int slideId, [FromBody] CursoSlideUpsertDto dto)
        {
            try
            {
                await _repo.UpdateSlideAsync(slideId, new CursoSlide
                {
                    Orden = dto.Orden,
                    TipoCodigo = dto.TipoCodigo,
                    EsEvaluable = dto.EsEvaluable,
                    Puntaje = dto.Puntaje,
                    ModoCorreccion = dto.ModoCorreccion,
                    ConfiguracionJson = dto.ConfiguracionJson,
                });
                return Ok();
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoController.ActualizarSlide"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("slides/{slideId:int}/duplicar")]
        public async Task<IActionResult> DuplicarSlide(int slideId, [FromQuery] int? cursoDestinoId)
        {
            try
            {
                var copia = await _repo.DuplicarSlideAsync(slideId, cursoDestinoId);
                return Ok(MapSlideDtoCompleto(copia));
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoController.DuplicarSlide"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpDelete("slides/{slideId:int}")]
        public async Task<IActionResult> EliminarSlide(int slideId)
        {
            try
            {
                await _repo.DeleteSlideAsync(slideId);
                return Ok();
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoController.EliminarSlide"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
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

        [HttpGet("{id:int}/slides/admin")]
        public async Task<IActionResult> GetSlidesAdmin(int id)
        {
            try
            {
                var curso = await _repo.GetByIdAsync(id)
                    ?? throw new AbrilException("Curso no encontrado.", 404);

                var slides = await _repo.GetSlidesOrdenadasAsync(id);
                return Ok(slides.Select(MapSlideDtoCompleto));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoController.GetSlidesAdmin"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
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
            ColorTema = c.ColorTema,
        };

        /// <summary>Slide completa (con "respuestaCorrecta" incluida) para el editor administrativo —
        /// a diferencia de MapSlideDtoSinRespuesta, este mapeo es solo para pantallas de administración,
        /// nunca para el player que rinde el curso a un usuario evaluándose.</summary>
        private static CursoSlideDto MapSlideDtoCompleto(CursoSlide s) => new()
        {
            Id = s.Id,
            CursoId = s.CursoId,
            Orden = s.Orden,
            TipoCodigo = s.TipoCodigo,
            EsEvaluable = s.EsEvaluable,
            Puntaje = s.Puntaje,
            ModoCorreccion = s.ModoCorreccion,
            ConfiguracionJson = s.ConfiguracionJson,
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
