using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Constants;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Presentation
{
    [ApiController]
    [Route("api/v1/[controller]")]
    // Editar implica ver: el script de roles (Migrations/Manual/20260929_ContratosFeatureRoles.sql)
    // solo asignó ".editar" a los 4 roles habilitados — si solo pidiéramos Ver acá, esos roles no
    // podrían ni abrir la pantalla. RequireFeature acepta varias claves en OR.
    [RequireFeature(ContratosFeatures.Ver, ContratosFeatures.Editar)]
    public class ProjectContractController : ControllerBase
    {
        private readonly IProjectContractService _service;
        private readonly IProjectContractFolderService _folderService;

        public ProjectContractController(IProjectContractService service, IProjectContractFolderService folderService)
        {
            _service = service;
            _folderService = folderService;
        }

        // ── Configuración → Carpeta de Contratos (por proyecto) ──────────────

        [Authorize]
        [HttpGet("carpeta")]
        public async Task<IActionResult> GetFolder([FromQuery] int projectId)
        {
            try
            {
                var result = await _folderService.GetByProjectIdAsync(projectId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [RequireFeature(ContratosFeatures.Editar)]
        [HttpPost("carpeta")]
        public async Task<IActionResult> SaveFolder([FromQuery] int projectId, [FromBody] ProjectContractFolderSaveDTO dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var result = await _folderService.SaveAsync(projectId, dto, userId);
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

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAllByProjectId([FromQuery] int projectId)
        {
            try
            {
                var result = await _service.GetAllByProjectIdAsync(projectId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("{projectContractId:int}")]
        public async Task<IActionResult> GetById(int projectContractId)
        {
            try
            {
                var result = await _service.GetByIdAsync(projectContractId);
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

        [Authorize]
        [RequireFeature(ContratosFeatures.Editar)]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProjectContractCreateDTO dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var id = await _service.CreateAsync(dto, userId);
                return Ok(new { projectContractId = id, message = "Contrato creado exitosamente." });
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

        [Authorize]
        [RequireFeature(ContratosFeatures.Editar)]
        [HttpPut("{projectContractId:int}")]
        public async Task<IActionResult> Edit(int projectContractId, [FromBody] ProjectContractEditDTO dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await _service.EditAsync(projectContractId, dto, userId);
                return Ok(new { message = "Contrato actualizado exitosamente." });
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

        [Authorize]
        [RequireFeature(ContratosFeatures.Editar)]
        [HttpPost("{projectContractId:int}/hitos")]
        public async Task<IActionResult> AddMilestone(int projectContractId, [FromBody] ProjectContractMilestoneCreateDTO dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var result = await _service.AddMilestoneAsync(projectContractId, dto, userId);
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

        [Authorize]
        [RequireFeature(ContratosFeatures.Editar)]
        [HttpDelete("hitos/{projectContractMilestoneId:int}")]
        public async Task<IActionResult> DeleteMilestone(int projectContractMilestoneId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var result = await _service.DeleteMilestoneAsync(projectContractMilestoneId, userId);
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

        /// <summary>Registra el pago (fecha/cheque-recibo/observación) de un hito ya creado.</summary>
        [Authorize]
        [RequireFeature(ContratosFeatures.Editar)]
        [HttpPatch("hitos/{projectContractMilestoneId:int}/pago")]
        public async Task<IActionResult> RegisterMilestonePayment(
            int projectContractMilestoneId, [FromBody] ProjectContractMilestonePaymentDTO dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var result = await _service.RegisterMilestonePaymentAsync(projectContractMilestoneId, dto, userId);
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

        /// <summary>Paso 3: genera el .docx del contrato mergeando la plantilla de la especialidad
        /// correspondiente con los datos del contrato y sus hitos de pago.</summary>
        [Authorize]
        [RequireFeature(ContratosFeatures.Editar)]
        [HttpPost("{projectContractId:int}/generar-contrato")]
        public async Task<IActionResult> GenerateContract(int projectContractId)
        {
            try
            {
                var (bytes, fileName) = await _service.GenerateContractAsync(projectContractId);
                const string docxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

                // Sin esto el navegador no deja leer Content-Disposition desde JS (CORS) y el
                // frontend no puede recuperar el nombre real del archivo — mismo fix que
                // GestionSalidaController.
                Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");

                return File(bytes, docxMime, fileName);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error generando el contrato: {ex.Message}" });
            }
        }

        /// <summary>Paso 4: envía el contrato al contratista (o solo registra que ya se envió
        /// fuera del sistema, si <c>skipNotification</c> viene en true).</summary>
        [Authorize]
        [RequireFeature(ContratosFeatures.Editar)]
        [HttpPost("{projectContractId:int}/paso4-enviar")]
        public async Task<IActionResult> AdvanceToStep4(int projectContractId, [FromQuery] bool skipNotification = false)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await _service.AdvanceToStep4Async(projectContractId, skipNotification, userId);
                var message = skipNotification
                    ? "Registrado: el contrato ya se envió fuera del sistema."
                    : "Contrato enviado al contratista exitosamente.";
                return Ok(new { message });
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

        /// <summary>Paso 5: registra la llegada del expediente a Oficina Central.</summary>
        [Authorize]
        [RequireFeature(ContratosFeatures.Editar)]
        [HttpPatch("{projectContractId:int}/paso5-llegada")]
        public async Task<IActionResult> RegisterStep5Arrival(int projectContractId, [FromBody] ProjectContractStep5ArrivalDTO dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await _service.RegisterStep5ArrivalAsync(projectContractId, dto, userId);
                return Ok(new { message = "Llegada registrada exitosamente." });
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

        /// <summary>Paso 6: persiste el estado de las 3 firmas (Jefe de Proyectos, Gerente
        /// Inmobiliario, Gerente General).</summary>
        [Authorize]
        [RequireFeature(ContratosFeatures.Editar)]
        [HttpPatch("{projectContractId:int}/paso6-firmas")]
        public async Task<IActionResult> UpdateStep6Signatures(int projectContractId, [FromBody] ProjectContractStep6SignaturesDTO dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await _service.UpdateStep6SignaturesAsync(projectContractId, dto, userId);
                return Ok(new { message = "Firmas actualizadas exitosamente." });
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

        /// <summary>Paso 7: sube el contrato firmado escaneado (slot 1, 2 o 3). Avanza a estado 7.</summary>
        [Authorize]
        [RequireFeature(ContratosFeatures.Editar)]
        [HttpPost("{projectContractId:int}/paso7-escaneo/{slot:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadScannedDoc(int projectContractId, int slot, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "Debe adjuntar un archivo." });

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                using var stream = file.OpenReadStream();
                var result = await _service.UploadScannedDocAsync(
                    projectContractId, slot, file.FileName, stream, file.ContentType, userId);
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

        /// <summary>Paso 8: notifica al correo de Unidad de Proyectos que el contrato ya está firmado.</summary>
        [Authorize]
        [RequireFeature(ContratosFeatures.Editar)]
        [HttpPost("{projectContractId:int}/paso8-notificar")]
        public async Task<IActionResult> NotifyStep8(int projectContractId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await _service.NotifyStep8Async(projectContractId, userId);
                return Ok(new { message = "Unidad de Proyectos notificada exitosamente." });
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

        /// <summary>Paso 9: cierra el expediente del contrato.</summary>
        [Authorize]
        [RequireFeature(ContratosFeatures.Editar)]
        [HttpPost("{projectContractId:int}/paso9-cerrar")]
        public async Task<IActionResult> CloseStep9(int projectContractId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                await _service.CloseStep9Async(projectContractId, userId);
                return Ok(new { message = "Contrato cerrado exitosamente." });
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
