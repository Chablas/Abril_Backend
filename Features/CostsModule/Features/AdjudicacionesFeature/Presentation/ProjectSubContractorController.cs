using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Interfaces;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos;

namespace Abril_Backend.Features.Adjudicaciones.Presentation
{

    [ApiController]
    [Route("api/v1/[controller]")]
    public class ProjectSubContractorController : ControllerBase
    {
        IProjectSubContractorService _projectSubContractorService;
        public ProjectSubContractorController(IProjectSubContractorService projectSubContractorService)
        {
            _projectSubContractorService = projectSubContractorService;
        }

        [Authorize]
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int? projectId,
            [FromQuery] string? contributorName,
            [FromQuery] string? contributorRuc,
            [FromQuery] int? createdUserId,
            [FromQuery] int page = 1)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var filter = new ProjectSubContractorFilterDTO
                {
                    ProjectId = projectId,
                    ContributorName = contributorName,
                    ContributorRuc = contributorRuc,
                    CreatedUserId = createdUserId,
                    Page = page
                };

                var result = await _projectSubContractorService.GetPaged(filter);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] ProjectSubContractorCreateDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                await _projectSubContractorService.Create(dto, userId);
                return Ok(new { message = "Adjudicación creada exitosamente" });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("form-data")]
        public async Task<IActionResult> GetFormData()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                var data = await _projectSubContractorService.GetFormData();

                return Ok(data);
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPatch("{id}/dates")]
        public async Task<IActionResult> SaveDates(int id, [FromBody] UpdateDatesDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                await _projectSubContractorService.SaveDates(id, dto, userId);
                return Ok(new { message = "Fechas guardadas exitosamente." });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost("{id}/documents/{documentType}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadDocument(int id, string documentType, IFormFile file)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                if (!Enum.TryParse<AdjudicacionDocumentType>(documentType, ignoreCase: true, out var docType))
                    return BadRequest(new { message = $"Tipo de documento inválido: '{documentType}'. Valores válidos: {string.Join(", ", Enum.GetNames<AdjudicacionDocumentType>())}" });

                var result = await _projectSubContractorService.UploadDocumentAsync(id, docType, file, userId);
                return Ok(result);
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPatch("{id}/documents/{documentType}/status")]
        public async Task<IActionResult> UpdateDocumentStatus(int id, string documentType, [FromBody] UpdateDocumentStatusDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                if (!Enum.TryParse<AdjudicacionDocumentType>(documentType, ignoreCase: true, out var docType))
                    return BadRequest(new { message = $"Tipo de documento inválido: '{documentType}'." });

                await _projectSubContractorService.UpdateDocumentStatusAsync(id, docType, dto.StatusId, dto.Observation, userId);
                return Ok(new { message = "Estado actualizado exitosamente." });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost("{id}/generate/{documentType}")]
        public async Task<IActionResult> GenerateDocument(int id, string documentType)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                if (!Enum.TryParse<AdjudicacionDocumentType>(documentType, ignoreCase: true, out var docType))
                    return BadRequest(new { message = $"Tipo de documento inválido: '{documentType}'. Valores válidos: {string.Join(", ", Enum.GetNames<AdjudicacionDocumentType>())}" });

                var result = await _projectSubContractorService.GenerateDocumentAsync(id, docType, userId);
                return Ok(result);
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                await _projectSubContractorService.UpdateStatusAsync(id, dto.ProjectSubContractorStatusId, userId);
                return Ok(new { message = "Estado actualizado exitosamente." });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPatch("{id}/arrival-option")]
        public async Task<IActionResult> SetArrivalOption(int id, [FromBody] ConfirmStep5DTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                await _projectSubContractorService.SetArrivalOptionAsync(id, dto.ArrivedWithObservations, userId);
                return Ok(new { message = "Opción de llegada guardada." });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost("{id}/confirm-step5")]
        public async Task<IActionResult> ConfirmStep5(int id, [FromBody] ConfirmStep5DTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                await _projectSubContractorService.ConfirmStep5Async(id, dto.ArrivedWithObservations, userId);
                return Ok(new { message = "Recepción confirmada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost("{id}/send-sc-notification")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SendScNotification(int id, IFormFile? file, [FromForm] string graphAccessToken)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                await _projectSubContractorService.SendScNotificationAsync(id, graphAccessToken, file, userId);
                return Ok(new { message = "Correo enviado al subcontratista exitosamente." });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost("{id}/send-step6-notification")]
        public async Task<IActionResult> SendStep6Notification(int id, [FromBody] SendStep6NotificationDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                await _projectSubContractorService.SendStep6NotificationAsync(id, dto.GraphAccessToken, userId);
                return Ok(new { message = "Correo de proceso de firma enviado exitosamente." });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost("{id}/send-step8-notification")]
        public async Task<IActionResult> SendStep8Notification(int id, [FromBody] SendStep8NotificationDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                await _projectSubContractorService.SendStep8NotificationAsync(id, dto.GraphAccessToken, userId);
                return Ok(new { message = "Correo enviado a Oficina Técnica exitosamente." });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost("{id}/advance-to-step4")]
        public async Task<IActionResult> AdvanceToStep4(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                await _projectSubContractorService.AdvanceToStep4Async(id, userId);
                return Ok(new { message = "Adjudicación aprobada y Oficina Técnica notificada." });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost("{id}/generate-contract-package")]
        public async Task<IActionResult> GenerateContractPackage(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                var result = await _projectSubContractorService.GenerateContractPackageAsync(id, userId);

                // Exponer la URL guardada en SharePoint para que el frontend la pueda leer
                Response.Headers["Access-Control-Expose-Headers"] = "X-Package-Url,X-Package-Filename";
                Response.Headers["X-Package-Url"]      = result.FileUrl;
                Response.Headers["X-Package-Filename"] = Uri.EscapeDataString(result.OriginalFileName);

                return File(result.Bytes, "application/pdf", result.OriginalFileName);
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // Log completo para diagnóstico
                Console.Error.WriteLine($"[GenerateContractPackage] Exception: {ex}");
                return StatusCode(500, new { message = $"Error generando paquete: {ex.Message}" });
            }
        }

        [Authorize]
        [HttpPost("send-notification")]
        public async Task<IActionResult> SendNotification([FromBody] SendAdjudicacionNotificationDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                await _projectSubContractorService.SendNotification(dto, userId);
                return Ok(new { message = "Notificación enviada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}