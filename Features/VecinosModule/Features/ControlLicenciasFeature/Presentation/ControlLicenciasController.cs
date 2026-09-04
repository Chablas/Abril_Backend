using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Application.Dtos;
using Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Application.Interfaces;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Presentation
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ControlLicenciasController : ControllerBase
    {
        private readonly IControlLicenciasService _service;
        private readonly ILogger<ControlLicenciasController> _logger;
        private readonly IConfiguration _configuration;

        public ControlLicenciasController(
            IControlLicenciasService service,
            ILogger<ControlLicenciasController> logger,
            IConfiguration configuration)
        {
            _service = service;
            _logger = logger;
            _configuration = configuration;
        }

        private int CurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        private static string EstadoFechaTexto(string? estado) => estado switch
        {
            VecinoLicenciaFechaEstado.NoSeCuenta => "No se cuenta",
            VecinoLicenciaFechaEstado.Pendiente => "Pendiente",
            VecinoLicenciaFechaEstado.Indeterminado => "Indeterminado",
            VecinoLicenciaFechaEstado.NoRegistrada => "No registrada",
            _ => string.Empty,
        };

        /// <summary>Proyectos activos disponibles para Control de Licencias.</summary>
        [HttpGet("proyectos")]
        public async Task<IActionResult> GetProyectos()
        {
            try
            {
                var result = await _service.GetProyectos();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS PROYECTOS: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Plantilla de tipos de licencia (base + propios) con el estado vigente de cada uno, para un proyecto.</summary>
        [HttpGet("proyectos/{projectId:int}")]
        public async Task<IActionResult> GetPlantilla(int projectId)
        {
            try
            {
                var result = await _service.GetPlantilla(projectId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS PLANTILLA GET: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Plantilla combinada de todos los proyectos (o los indicados por projectIds), para la vista "todos" de Plantilla.</summary>
        [HttpGet("plantilla")]
        public async Task<IActionResult> GetPlantillaTodos([FromQuery] List<int>? projectIds)
        {
            try
            {
                var result = await _service.GetPlantillaTodos(projectIds);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS PLANTILLA TODOS: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Agrega un tipo de licencia propio de un proyecto (no afecta la plantilla base ni a otros proyectos).</summary>
        [HttpPost("proyectos/{projectId:int}/tipos")]
        public async Task<IActionResult> AddTipo(int projectId, [FromBody] VecinoLicenciaTipoCreateDto dto)
        {
            try
            {
                var tipo = await _service.AddTipo(projectId, dto, CurrentUserId());
                return Ok(new { tipo, message = "Tipo de licencia agregado." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS TIPO ADD: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Sube/reemplaza el documento vigente de un tipo de licencia. Si había uno, se archiva en el historial.</summary>
        [HttpPost("proyectos/{projectId:int}/tipos/{tipoId:int}/upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadLicencia(int projectId, int tipoId, [FromForm] IFormFile file, [FromForm] VecinoLicenciaUploadDto dto)
        {
            try
            {
                await _service.UploadLicencia(projectId, tipoId, dto, file, CurrentUserId());
                return Ok(new { message = "Licencia registrada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS UPLOAD: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Agrega un recordatorio adicional (N días antes) a la licencia vigente de un tipo.</summary>
        [HttpPost("proyectos/{projectId:int}/tipos/{tipoId:int}/recordatorios")]
        public async Task<IActionResult> AddRecordatorio(int projectId, int tipoId, [FromBody] VecinoLicenciaRecordatorioCreateDto dto)
        {
            try
            {
                var recordatorio = await _service.AddRecordatorio(projectId, tipoId, dto, CurrentUserId());
                return Ok(new { recordatorio, message = "Recordatorio agregado." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS RECORDATORIO ADD: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Agrega una fecha de visita al Anexo H de la licencia vigente de un tipo. Recordatorio fijo: 2 días antes.</summary>
        [HttpPost("proyectos/{projectId:int}/tipos/{tipoId:int}/visitas")]
        public async Task<IActionResult> AddVisita(int projectId, int tipoId, [FromBody] VecinoLicenciaVisitaCreateDto dto)
        {
            try
            {
                var visita = await _service.AddVisita(projectId, tipoId, dto, CurrentUserId());
                return Ok(new { visita, message = "Visita registrada." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS VISITA ADD: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Edita las fechas ampliadas del dashboard (inscripción/inicio/renovación) y el flag Mes Activo.</summary>
        [HttpPatch("proyectos/{projectId:int}/tipos/{tipoId:int}/fechas")]
        public async Task<IActionResult> UpdateFechas(int projectId, int tipoId, [FromBody] VecinoLicenciaFechasUpdateDto dto)
        {
            try
            {
                await _service.UpdateFechas(projectId, tipoId, dto, CurrentUserId());
                return Ok(new { message = "Fechas actualizadas." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS FECHAS UPDATE: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Dashboard gerencial: todos los proyectos (o los indicados por projectIds), ordenado de más a menos crítico.</summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard([FromQuery] List<int>? projectIds)
        {
            try
            {
                var result = await _service.GetDashboard(projectIds);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS DASHBOARD: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Exporta el dashboard (todos los proyectos o los indicados) a Excel.</summary>
        [HttpGet("dashboard/export")]
        public async Task<IActionResult> ExportDashboard([FromQuery] List<int>? projectIds)
        {
            try
            {
                var dashboard = await _service.GetDashboard(projectIds);

                using var workbook = new XLWorkbook();
                var ws = workbook.AddWorksheet("Control de Licencias");

                var headers = new[]
                {
                    "Proyecto", "Tipo de documento", "Estado", "Fecha inscripción", "Fecha inicio",
                    "Fecha vencimiento", "Fecha renovación", "Mes activo", "Días para vencer", "Criticidad",
                };

                for (int col = 1; col <= headers.Length; col++)
                {
                    var cell = ws.Cell(1, col);
                    cell.Value = headers[col - 1];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F6E56");
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                int row = 2;
                foreach (var item in dashboard.Items)
                {
                    ws.Cell(row, 1).Value = item.ProjectDescription;
                    ws.Cell(row, 2).Value = item.TipoDescripcion;
                    ws.Cell(row, 3).Value = item.EstadoDescripcion;
                    ws.Cell(row, 4).Value = item.FechaInscripcion?.ToString("dd/MM/yyyy") ?? EstadoFechaTexto(item.FechaInscripcionEstado);
                    ws.Cell(row, 5).Value = item.FechaInicio?.ToString("dd/MM/yyyy") ?? EstadoFechaTexto(item.FechaInicioEstado);
                    ws.Cell(row, 6).Value = item.FechaVencimiento?.ToString("dd/MM/yyyy") ?? EstadoFechaTexto(item.FechaVencimientoEstado);
                    ws.Cell(row, 7).Value = item.FechaRenovacion?.ToString("dd/MM/yyyy") ?? EstadoFechaTexto(item.FechaRenovacionEstado);
                    ws.Cell(row, 8).Value = item.MesActivo ? "SI" : "NO";
                    ws.Cell(row, 9).Value = item.DiasParaVencer?.ToString() ?? string.Empty;
                    ws.Cell(row, 10).Value = item.Semaforo switch
                    {
                        "rojo" => "Crítico",
                        "amarillo" => "Alerta",
                        "verde" => "OK",
                        _ => "No aplica",
                    };

                    var fillColor = item.Semaforo switch
                    {
                        "rojo" => XLColor.FromHtml("#FEE2E2"),
                        "amarillo" => XLColor.FromHtml("#FEF3C7"),
                        "verde" => XLColor.FromHtml("#D1FAE5"),
                        _ => XLColor.FromHtml("#F3F4F6"),
                    };
                    ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = fillColor;

                    row++;
                }

                ws.Range(1, 1, row - 1, headers.Length).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                ws.Range(1, 1, row - 1, headers.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                ws.Columns().AdjustToContents();

                using var ms = new MemoryStream();
                workbook.SaveAs(ms);
                var bytes = ms.ToArray();

                var fileName = $"ControlLicencias_{DateTime.UtcNow.AddHours(-5):yyyyMMdd}.xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS DASHBOARD EXPORT: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpDelete("visitas/{visitaId:int}")]
        public async Task<IActionResult> DeleteVisita(int visitaId)
        {
            try
            {
                await _service.DeleteVisita(visitaId, CurrentUserId());
                return Ok(new { message = "Visita eliminada." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS VISITA DELETE: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpDelete("recordatorios/{recordatorioId:int}")]
        public async Task<IActionResult> DeleteRecordatorio(int recordatorioId)
        {
            try
            {
                await _service.DeleteRecordatorio(recordatorioId, CurrentUserId());
                return Ok(new { message = "Recordatorio eliminado." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS RECORDATORIO DELETE: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("proyectos/{projectId:int}/tipos/{tipoId:int}/no-aplica")]
        public async Task<IActionResult> SetNoAplica(int projectId, int tipoId, [FromBody] VecinoLicenciaNoAplicaDto dto)
        {
            try
            {
                await _service.SetNoAplica(projectId, tipoId, dto.NoAplica, CurrentUserId());
                return Ok(new { message = "Estado actualizado." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS NO APLICA: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        // ── Catálogo base (plantilla común a todos los proyectos) ───────────────

        /// <summary>Catálogo base: tipos visibles en todos los proyectos.</summary>
        [HttpGet("catalogo")]
        public async Task<IActionResult> GetCatalogoBase()
        {
            try
            {
                var result = await _service.GetCatalogoBase();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS CATALOGO GET: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Agrega un tipo a la plantilla base: aparece de inmediato en todos los proyectos.</summary>
        [HttpPost("catalogo")]
        public async Task<IActionResult> AddTipoBase([FromBody] VecinoLicenciaTipoBaseUpsertDto dto)
        {
            try
            {
                var tipo = await _service.AddTipoBase(dto, CurrentUserId());
                return Ok(new { tipo, message = "Tipo agregado a la plantilla base." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS CATALOGO ADD: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Edita un tipo del catálogo (base o propio de un proyecto): descripción y días de antelación por defecto.</summary>
        [HttpPut("catalogo/{tipoId:int}")]
        public async Task<IActionResult> UpdateTipo(int tipoId, [FromBody] VecinoLicenciaTipoBaseUpsertDto dto)
        {
            try
            {
                var tipo = await _service.UpdateTipo(tipoId, dto, CurrentUserId());
                return Ok(new { tipo, message = "Tipo actualizado." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS CATALOGO UPDATE: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Elimina (soft delete) un tipo del catálogo base — deja de verse en todos los proyectos.</summary>
        [HttpDelete("catalogo/{tipoId:int}")]
        public async Task<IActionResult> DeleteTipo(int tipoId)
        {
            try
            {
                await _service.DeleteTipo(tipoId, CurrentUserId());
                return Ok(new { message = "Tipo eliminado." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS CATALOGO DELETE: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Historial de versiones anteriores del documento de un tipo de licencia en un proyecto.</summary>
        [HttpGet("proyectos/{projectId:int}/tipos/{tipoId:int}/historial")]
        public async Task<IActionResult> GetHistorial(int projectId, int tipoId)
        {
            try
            {
                var result = await _service.GetHistorial(projectId, tipoId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS HISTORIAL: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Destinatarios de recordatorio configurados para un proyecto (por rol).</summary>
        [HttpGet("proyectos/{projectId:int}/destinatarios")]
        public async Task<IActionResult> GetDestinatarios(int projectId)
        {
            try
            {
                var result = await _service.GetDestinatarios(projectId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS DESTINATARIOS GET: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost("proyectos/{projectId:int}/destinatarios")]
        public async Task<IActionResult> AddDestinatario(int projectId, [FromBody] VecinoLicenciaDestinatarioUpsertDto dto)
        {
            try
            {
                var destinatario = await _service.AddDestinatario(projectId, dto, CurrentUserId());
                return Ok(new { destinatario, message = "Destinatario agregado." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS DESTINATARIOS ADD: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpDelete("destinatarios/{destinatarioId:int}")]
        public async Task<IActionResult> DeleteDestinatario(int destinatarioId)
        {
            try
            {
                await _service.DeleteDestinatario(destinatarioId, CurrentUserId());
                return Ok(new { message = "Destinatario eliminado." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS DESTINATARIOS DELETE: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Cron (cron-job.org): envía los recordatorios de licencias cuya fecha de recordatorio
        /// ya llegó, en todos los proyectos. Protegido con el CronSecret en el header Authorization.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("recordatorios/procesar")]
        public async Task<IActionResult> ProcesarRecordatorios()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader != $"Bearer {_configuration["CronSecret"]}")
                    return Unauthorized();

                var result = await _service.ProcesarRecordatorios();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL LICENCIAS RECORDATORIOS: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
