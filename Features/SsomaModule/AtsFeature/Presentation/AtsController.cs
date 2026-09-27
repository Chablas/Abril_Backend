using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/ats")]
    [Authorize]
    [RequireFeature("ssoma.gestion.ats")]
    public class AtsController : ControllerBase
    {
        private readonly IAtsService _service;
        private readonly ILogger<AtsController> _logger;

        public AtsController(IAtsService service, ILogger<AtsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private int CurrentUserId()
        {
            var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(val, out var id))
                throw new AbrilException("No se pudo identificar al usuario.", 401);
            return id;
        }

        /// <summary>
        /// El claim de rol lleva el ID (estable), no el nombre — comparar contra
        /// "ADMINISTRADOR DE SSOMA" (como estaba antes) nunca coincide con nada, porque el JWT
        /// nunca lleva ese texto (ver JWTService.cs). Roles.AdministradorSistema="1",
        /// Roles.AdministradorSsoma="9" (JEFE SSOMA).
        /// </summary>
        private bool EsAdmin()
        {
            var roleIds = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            return roleIds.Contains(Roles.AdministradorSistema) || roleIds.Contains(Roles.AdministradorSsoma);
        }

        [HttpGet("init")]
        public async Task<IActionResult> GetInit()
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                return Ok(await _service.GetInit(workerId));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetInit"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        public class CrearPasoRequest { public int CategoriaId { get; set; } public string Texto { get; set; } = string.Empty; }

        [HttpPost("pasos")]
        public async Task<IActionResult> CrearPasoPersonalizado([FromBody] CrearPasoRequest body)
        {
            try { return Ok(await _service.CrearPasoPersonalizado(body.CategoriaId, body.Texto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CrearPasoPersonalizado"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] AtsGuardarRequestDto dto)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                var id = await _service.Crear(workerId, dto);
                return Ok(new { id });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.Crear"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Editar(int id, [FromBody] AtsGuardarRequestDto dto)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                await _service.Editar(id, workerId, dto);
                return Ok(new { message = "ATS actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.Editar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPorId(int id)
        {
            try
            {
                var userId = CurrentUserId();
                var workerId = await _service.ResolverWorkerId(userId);
                return Ok(await _service.GetPorId(id, userId, workerId, EsAdmin()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPorId"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] AtsFiltroDto filtro)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                return Ok(await _service.Listar(filtro, workerId, EsAdmin()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.Listar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("{id:int}/firmar")]
        public async Task<IActionResult> Firmar(int id, [FromBody] AtsFirmarRequestDto body)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers.UserAgent.ToString();
                await _service.Firmar(id, workerId, body, ip, userAgent);
                return Ok(new { message = "ATS firmado correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.Firmar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>
        /// Firma de "Autoriza" (Residente / Ing. de Producción) — adicional a la del ejecutante,
        /// que ya debe estar firmada. Sin selfie ni geo: es una validación documental.
        /// </summary>
        [HttpPost("{id:int}/autorizar")]
        public async Task<IActionResult> FirmarAutorizacion(int id, [FromBody] AtsFirmarVistoRequestDto body)
        {
            try
            {
                await _service.FirmarAutorizacion(id, CurrentUserId(), EsAdmin(), body);
                return Ok(new { message = "Firmado como Autoriza." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.FirmarAutorizacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Visto Bueno de SSOMA (Prevencionista / Coordinador SSOMA) — verificación técnica
        /// del análisis de riesgo, obligatoria y no removible (Art. 76 Reglamento Ley 29783).</summary>
        [HttpPost("{id:int}/visto-bueno-ssoma")]
        public async Task<IActionResult> FirmarVistoSsoma(int id, [FromBody] AtsFirmarVistoRequestDto body)
        {
            try
            {
                await _service.FirmarVistoSsoma(id, CurrentUserId(), EsAdmin(), body);
                return Ok(new { message = "Visto bueno de SSOMA registrado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.FirmarVistoSsoma"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>
        /// Verificación pública del QR impreso en el PDF — sin login, para que un inspector de
        /// SUNAFIL (o cualquiera con el link) confirme que el documento corresponde exactamente
        /// a lo firmado en el sistema. [AllowAnonymous] hace que [Authorize]/[RequireFeature] de
        /// la clase no apliquen a esta acción (RequireFeatureAttribute ya no hace nada cuando el
        /// usuario no está autenticado).
        /// </summary>
        [HttpGet("{id:int}/verificar-publico")]
        [AllowAnonymous]
        public async Task<IActionResult> VerificarPublico(int id, [FromQuery] string? hash)
        {
            try { return Ok(await _service.VerificarPublico(id, hash)); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.VerificarPublico"); return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpGet("{id:int}/pdf")]
        public async Task<IActionResult> GetPdf(int id)
        {
            try
            {
                var bytes = await _service.GenerarPdf(id);
                return File(bytes, "application/pdf", $"ATS-{id}.pdf");
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPdf"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Administración de plantillas (Coordinador SSOMA en adelante) ────

        [HttpGet("puestos")]
        public async Task<IActionResult> GetPuestos()
        {
            try { return Ok(await _service.GetPuestos()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPuestos"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("pasos-puesto")]
        public async Task<IActionResult> GetPasoPuestoMapeo()
        {
            try { return Ok(await _service.GetPasoPuestoMapeo()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPasoPuestoMapeo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("pasos-puesto/{pasoId:int}")]
        public async Task<IActionResult> SetPasoPuestos(int pasoId, [FromBody] List<int> puestoIds)
        {
            try
            {
                await _service.SetPasoPuestos(pasoId, puestoIds);
                return Ok(new { message = "Actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.SetPasoPuestos"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("peligros")]
        public async Task<IActionResult> GetPeligros()
        {
            try { return Ok(await _service.GetPeligrosConRiesgos()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPeligros"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>El Coordinador SSOMA marca manualmente qué riesgos exigen PETAR — no hay un
        /// valor por defecto del sistema, es una decisión de catálogo suya.</summary>
        [HttpPut("riesgos/{id:int}/requiere-petar")]
        public async Task<IActionResult> SetRiesgoRequierePetar(int id, [FromBody] AtsRiesgoRequierePetarRequestDto dto)
        {
            try
            {
                await _service.SetRiesgoRequierePetar(id, dto.RequierePetar);
                return Ok(new { message = "Actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.SetRiesgoRequierePetar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("plantillas")]
        public async Task<IActionResult> GetPlantillas()
        {
            try { return Ok(await _service.GetPlantillas()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPlantillas"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("plantillas")]
        public async Task<IActionResult> CrearPlantilla([FromBody] AtsPlantillaGuardarRequestDto dto)
        {
            try
            {
                var id = await _service.CrearPlantilla(dto);
                return Ok(new { id });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CrearPlantilla"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("plantillas/{id:int}")]
        public async Task<IActionResult> EditarPlantilla(int id, [FromBody] AtsPlantillaGuardarRequestDto dto)
        {
            try
            {
                await _service.EditarPlantilla(id, dto);
                return Ok(new { message = "Plantilla actualizada." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.EditarPlantilla"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpDelete("plantillas/{id:int}")]
        public async Task<IActionResult> DesactivarPlantilla(int id)
        {
            try
            {
                await _service.DesactivarPlantilla(id);
                return Ok(new { message = "Plantilla desactivada." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.DesactivarPlantilla"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Autorización de uso de firma digital e imagen (firmada en físico) — gate para poder hacer ATS ──

        /// <summary>El propio trabajador consulta esto ANTES de abrir el formulario de "Nuevo
        /// ATS", para mostrarle un bloqueo claro en vez de dejarlo llenar todo el wizard y
        /// recién fallar al guardar (que además también valida esto server-side).</summary>
        [HttpGet("mi-autorizacion")]
        public async Task<IActionResult> GetMiAutorizacion()
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                var tieneAutorizacion = await _service.TieneAutorizacionPermiso(workerId);
                return Ok(new { tieneAutorizacion });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetMiAutorizacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Listado del Coordinador SSOMA: qué trabajadores ya tienen su autorización de
        /// uso de firma digital e imagen subida y cuáles faltan.</summary>
        [HttpGet("trabajadores-autorizacion")]
        public async Task<IActionResult> GetTrabajadoresParaAutorizacion()
        {
            try { return Ok(await _service.GetTrabajadoresParaAutorizacion()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetTrabajadoresParaAutorizacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Plantilla imprimible de la autorización (SSO-FO-151) para un trabajador
        /// puntual — la descarga el Coordinador SSOMA, se la da a firmar en físico, y luego sube
        /// el escaneado con el endpoint de abajo.</summary>
        [HttpGet("trabajadores/{workerId:int}/autorizacion/pdf")]
        public async Task<IActionResult> GetPlantillaAutorizacionPdf(int workerId)
        {
            try
            {
                var bytes = await _service.GenerarPlantillaAutorizacionPdf(workerId);
                return File(bytes, "application/pdf", $"Autorizacion_ATS_{workerId}.pdf");
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPlantillaAutorizacionPdf"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("trabajadores/{workerId:int}/autorizacion")]
        public async Task<IActionResult> SubirAutorizacionPermiso(int workerId, IFormFile archivo)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                    throw new AbrilException("El archivo está vacío.", 400);

                await using var stream = archivo.OpenReadStream();
                await _service.SubirAutorizacionPermiso(workerId, stream, archivo.FileName, CurrentUserId());
                return Ok(new { message = "Autorización subida correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.SubirAutorizacionPermiso"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
