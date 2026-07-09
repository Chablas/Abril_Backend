using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Presentation
{
    [Authorize]
    [ApiController]
    [Route("api/v1/actas-reunion")]
    public class ActasReunionController : ControllerBase
    {
        private readonly IActasReunionService _service;
        private readonly ILogger<ActasReunionController> _logger;

        public ActasReunionController(IActasReunionService service, ILogger<ActasReunionController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        /// <summary>Carga inicial de la página: filtros (proyectos, estados) + primera página de reuniones.</summary>
        [HttpGet("pagina-inicial")]
        public async Task<IActionResult> GetPaginaInicial([FromQuery] ReunionFiltroRequest filtro)
        {
            try
            {
                return Ok(await _service.GetPaginaInicial(filtro));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION PAGINA INICIAL: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Listado filtrado/paginado de reuniones (sin volver a traer los filtros).</summary>
        [HttpGet]
        public async Task<IActionResult> GetReuniones([FromQuery] ReunionFiltroRequest filtro)
        {
            try
            {
                return Ok(await _service.GetReuniones(filtro));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION LISTADO: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Detalle completo del acta: cabecera, participantes, acuerdos, archivos y reprogramaciones.</summary>
        [HttpGet("{reunionId:int}")]
        public async Task<IActionResult> GetDetalle(int reunionId)
        {
            try
            {
                return Ok(await _service.GetDetalle(reunionId));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION DETALLE: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Agenda una nueva reunión (estado PROGRAMADA) con sus participantes.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ReunionCreateRequest request)
        {
            try
            {
                var reunionId = await _service.Create(request, GetUserId());
                return Ok(new { reunionId, message = "Reunión agendada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION CREATE: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Actualiza los datos generales del acta y su lista de participantes.</summary>
        [HttpPut("{reunionId:int}")]
        public async Task<IActionResult> Update(int reunionId, [FromBody] ReunionUpdateRequest request)
        {
            try
            {
                await _service.Update(reunionId, request, GetUserId());
                return Ok(new { message = "Acta actualizada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION UPDATE: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Cambia la fecha de la reunión dejando rastro en el historial de reprogramaciones.</summary>
        [HttpPatch("{reunionId:int}/reprogramar")]
        public async Task<IActionResult> Reprogramar(int reunionId, [FromBody] ReunionReprogramarRequest request)
        {
            try
            {
                await _service.Reprogramar(reunionId, request, GetUserId());
                return Ok(new { message = "Reunión reprogramada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION REPROGRAMAR: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Cambia el estado de la reunión (PROGRAMADA, REALIZADA o CANCELADA).</summary>
        [HttpPatch("{reunionId:int}/estado")]
        public async Task<IActionResult> CambiarEstado(int reunionId, [FromBody] ReunionCambiarEstadoRequest request)
        {
            try
            {
                await _service.CambiarEstado(reunionId, request, GetUserId());
                return Ok(new { message = "Estado actualizado exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION ESTADO: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Elimina (soft delete) el acta de reunión.</summary>
        [HttpDelete("{reunionId:int}")]
        public async Task<IActionResult> Eliminar(int reunionId)
        {
            try
            {
                await _service.Eliminar(reunionId, GetUserId());
                return Ok(new { message = "Acta eliminada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION ELIMINAR: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        // ── Acuerdos ─────────────────────────────────────────────────────────

        /// <summary>Registra un acuerdo del acta con sus responsables.</summary>
        [HttpPost("{reunionId:int}/acuerdos")]
        public async Task<IActionResult> CrearAcuerdo(int reunionId, [FromBody] ReunionAcuerdoRequest request)
        {
            try
            {
                var acuerdoId = await _service.CrearAcuerdo(reunionId, request, GetUserId());
                return Ok(new { reunionAcuerdoId = acuerdoId, message = "Acuerdo registrado exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION CREAR ACUERDO: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Actualiza un acuerdo (descripción, fechas, estado, responsables).</summary>
        [HttpPut("acuerdos/{reunionAcuerdoId:int}")]
        public async Task<IActionResult> ActualizarAcuerdo(int reunionAcuerdoId, [FromBody] ReunionAcuerdoRequest request)
        {
            try
            {
                await _service.ActualizarAcuerdo(reunionAcuerdoId, request, GetUserId());
                return Ok(new { message = "Acuerdo actualizado exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION ACTUALIZAR ACUERDO: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Elimina (soft delete) un acuerdo del acta.</summary>
        [HttpDelete("acuerdos/{reunionAcuerdoId:int}")]
        public async Task<IActionResult> EliminarAcuerdo(int reunionAcuerdoId)
        {
            try
            {
                await _service.EliminarAcuerdo(reunionAcuerdoId, GetUserId());
                return Ok(new { message = "Acuerdo eliminado exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION ELIMINAR ACUERDO: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        // ── Carpeta de SharePoint para adjuntos ──────────────────────────────

        /// <summary>Carpeta única configurada para guardar los adjuntos (null si aún no se configuró).</summary>
        [HttpGet("carpeta")]
        public async Task<IActionResult> GetCarpeta()
        {
            try
            {
                return Ok(await _service.GetFolder());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION GET CARPETA: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Configura/actualiza la carpeta única: recibe el link, lo detecta y lo guarda.</summary>
        [HttpPut("carpeta")]
        public async Task<IActionResult> SaveCarpeta([FromBody] ReunionFolderSaveDto dto)
        {
            try
            {
                return Ok(await _service.SaveFolder(dto, GetUserId()));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION SAVE CARPETA: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        // ── Archivos ─────────────────────────────────────────────────────────

        /// <summary>Adjunta uno o varios archivos a la reunión (diapositivas, documentos, etc.).</summary>
        [HttpPost("{reunionId:int}/archivos")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubirArchivos(int reunionId, [FromForm] IFormFileCollection files)
        {
            try
            {
                var archivos = await _service.SubirArchivos(reunionId, files, GetUserId());
                return Ok(new { archivos, message = "Archivos subidos exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION SUBIR ARCHIVOS: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Elimina (soft delete) un archivo adjunto.</summary>
        [HttpDelete("archivos/{reunionArchivoId:int}")]
        public async Task<IActionResult> EliminarArchivo(int reunionArchivoId)
        {
            try
            {
                await _service.EliminarArchivo(reunionArchivoId, GetUserId());
                return Ok(new { message = "Archivo eliminado exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR ACTAS REUNION ELIMINAR ARCHIVO: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
