using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Application.Dtos;
using Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Application.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Application.Services
{
    public class ControlLicenciasService : IControlLicenciasService
    {
        private const long MaxBytes = 15 * 1024 * 1024;
        private static readonly string[] AllowedExtensions =
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".webp" };
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly IControlLicenciasRepository _repository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;
        private readonly IEmailService _emailService;

        public ControlLicenciasService(
            IControlLicenciasRepository repository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver,
            IEmailService emailService)
        {
            _repository = repository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
            _emailService = emailService;
        }

        public Task<List<ProjectOptionDto>> GetProyectos() => _repository.GetProyectos();

        public Task<VecinoLicenciaPlantillaResponseDto> GetPlantilla(int projectId) => _repository.GetPlantilla(projectId);

        public async Task<VecinoLicenciaTipoDto> AddTipo(int projectId, VecinoLicenciaTipoCreateDto dto, int userId)
        {
            var descripcion = (dto.Descripcion ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(descripcion))
                throw new AbrilException("Debe ingresar una descripción para el tipo de licencia.", 400);
            ValidarDiasAntesDefault(dto.DiasAntesDefault);

            return await _repository.AddTipo(projectId, descripcion, dto.DiasAntesDefault, userId);
        }

        public async Task<List<VecinoLicenciaTipoDto>> GetCatalogoBase() => await _repository.GetCatalogoBase();

        public async Task<VecinoLicenciaTipoDto> AddTipoBase(VecinoLicenciaTipoBaseUpsertDto dto, int userId)
        {
            var descripcion = (dto.Descripcion ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(descripcion))
                throw new AbrilException("Debe ingresar una descripción para el tipo de licencia.", 400);
            ValidarDiasAntesDefault(dto.DiasAntesDefault);

            return await _repository.AddTipoBase(descripcion, dto.DiasAntesDefault, userId);
        }

        public async Task<VecinoLicenciaTipoDto> UpdateTipo(int tipoId, VecinoLicenciaTipoBaseUpsertDto dto, int userId)
        {
            var descripcion = (dto.Descripcion ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(descripcion))
                throw new AbrilException("Debe ingresar una descripción para el tipo de licencia.", 400);
            ValidarDiasAntesDefault(dto.DiasAntesDefault);

            return await _repository.UpdateTipo(tipoId, descripcion, dto.DiasAntesDefault, userId);
        }

        public Task DeleteTipo(int tipoId, int userId) => _repository.DeleteTipo(tipoId, userId);

        private static void ValidarDiasAntesDefault(int? diasAntesDefault)
        {
            if (diasAntesDefault is < 0)
                throw new AbrilException("Los días de antelación no pueden ser negativos.", 400);
        }

        public async Task UploadLicencia(int projectId, int tipoId, VecinoLicenciaUploadDto dto, IFormFile file, int userId)
        {
            if (!await _repository.TipoAplicaAProyecto(projectId, tipoId))
                throw new AbrilException("El tipo de licencia no corresponde a este proyecto.", 400);

            if (file == null || file.Length == 0)
                throw new AbrilException("No se adjuntó ningún archivo.", 400);
            if (file.Length > MaxBytes)
                throw new AbrilException("El archivo supera el tamaño máximo permitido (15 MB).", 400);

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                throw new AbrilException("Formato no válido. Use PDF, Word, Excel o imagen.", 400);

            if (dto.FechaRecordatorio > dto.FechaVencimiento)
                throw new AbrilException("La fecha de recordatorio no puede ser posterior a la fecha de vencimiento.", 400);

            var dias = dto.FechaVencimiento.DayNumber - dto.FechaRecordatorio.DayNumber;
            if (dias < 0)
                throw new AbrilException("Los días de antelación no pueden ser negativos.", 400);
            dto.DiasAntes = dias;

            var container = _containerResolver.GetVecinoEntregablesContainerName();

            string archivoUrl;
            using (var stream = file.OpenReadStream())
            {
                var uploaded = await _fileStorageService.UploadFilesAsync(
                    new[] { (stream, $"{Guid.NewGuid()}{extension}") },
                    container);
                archivoUrl = uploaded.First();
            }

            await _repository.UploadLicencia(projectId, tipoId, archivoUrl, file.FileName,
                dto.FechaVencimiento, dto.FechaRecordatorio, dto.DiasAntes, userId);
        }

        public async Task SetNoAplica(int projectId, int tipoId, bool noAplica, int userId)
        {
            if (!await _repository.TipoAplicaAProyecto(projectId, tipoId))
                throw new AbrilException("El tipo de licencia no corresponde a este proyecto.", 400);

            await _repository.SetNoAplica(projectId, tipoId, noAplica, userId);
        }

        public Task<List<VecinoLicenciaHistorialItemDto>> GetHistorial(int projectId, int tipoId) => _repository.GetHistorial(projectId, tipoId);

        public Task<List<VecinoLicenciaDestinatarioDto>> GetDestinatarios(int projectId) => _repository.GetDestinatarios(projectId);

        public async Task<VecinoLicenciaDestinatarioDto> AddDestinatario(int projectId, VecinoLicenciaDestinatarioUpsertDto dto, int userId)
        {
            var rol = (dto.Rol ?? string.Empty).Trim();
            var email = (dto.Email ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(rol))
                throw new AbrilException("Debe indicar el rol del destinatario (Residente, Administrador, etc.).", 400);
            if (rol.Contains("gth", StringComparison.OrdinalIgnoreCase))
                throw new AbrilException("GTH no debe recibir recordatorios de Control de Licencias.", 400);
            if (!EmailRegex.IsMatch(email))
                throw new AbrilException($"El correo '{email}' no tiene un formato válido.", 400);

            return await _repository.AddDestinatario(projectId, rol, email, userId);
        }

        public Task DeleteDestinatario(int destinatarioId, int userId) => _repository.DeleteDestinatario(destinatarioId, userId);

        public async Task<RecordatoriosResultDto> ProcesarRecordatorios()
        {
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
            var pendientes = await _repository.GetPendientesRecordatorio(hoy);

            var result = new RecordatoriosResultDto();
            var destinatariosPorProyecto = new Dictionary<int, List<VecinoLicenciaDestinatarioDto>>();

            foreach (var licencia in pendientes)
            {
                try
                {
                    if (!destinatariosPorProyecto.TryGetValue(licencia.ProjectId, out var destinatarios))
                    {
                        destinatarios = await _repository.GetDestinatarios(licencia.ProjectId);
                        destinatariosPorProyecto[licencia.ProjectId] = destinatarios;
                    }

                    if (destinatarios.Count == 0)
                        continue; // Proyecto sin destinatarios configurados: no hay a quién avisar.

                    var emails = destinatarios.Select(d => d.Email).Distinct().ToList();

                    var diasRestantes = licencia.FechaVencimiento.DayNumber - hoy.DayNumber;
                    var subject = diasRestantes >= 0
                        ? $"Recordatorio: la licencia \"{licencia.TipoDescripcion}\" vence el {licencia.FechaVencimiento:dd/MM/yyyy}"
                        : $"Alerta: la licencia \"{licencia.TipoDescripcion}\" venció el {licencia.FechaVencimiento:dd/MM/yyyy}";

                    var detalleDias = diasRestantes > 1 ? $"Faltan <b>{diasRestantes} días</b> para su vencimiento."
                        : diasRestantes == 1 ? "Vence <b>mañana</b>."
                        : diasRestantes == 0 ? "Vence <b>hoy</b>."
                        : $"Venció hace <b>{-diasRestantes} día(s)</b>.";

                    var body = $"""
                        <p>Estimados,</p>
                        <p>Este es un recordatorio del <b>Control de Licencias</b> de Administración de Obra.</p>
                        <p>La licencia <b>{licencia.TipoDescripcion}</b> vence el <b>{licencia.FechaVencimiento:dd/MM/yyyy}</b>. {detalleDias}</p>
                        <p>Puede revisarla en la intranet:
                        <a href="https://intranet.abril.pe/vecinos/control-licencias">Control de Licencias</a></p>
                        <p>Este es un mensaje automático, por favor no responder.</p>
                        """;

                    await _emailService.SendAsync(emails, subject, body, isHtml: true);
                    await _repository.MarcarRecordatorioEnviado(licencia.VecinoLicenciaControlId);

                    result.LicenciasProcesadas++;
                    result.CorreosEnviados += emails.Count;
                }
                catch (Exception ex)
                {
                    // Un fallo puntual no debe frenar el resto de recordatorios del día.
                    result.Errores.Add($"Licencia {licencia.VecinoLicenciaControlId} (proyecto {licencia.ProjectId}): {ex.Message}");
                }
            }

            return result;
        }
    }
}
