using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Dtos;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Graph.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Services
{
    public class ControlVencimientosService : IControlVencimientosService
    {
        private const long MaxBytes = 15 * 1024 * 1024;
        private static readonly string[] AllowedExtensions =
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".webp" };
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly IControlVencimientosRepository _repository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;
        private readonly IEmailService _emailService;
        private readonly IEmailGroupResolver _emailGroupResolver;

        public ControlVencimientosService(
            IControlVencimientosRepository repository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver,
            IEmailService emailService,
            IEmailGroupResolver emailGroupResolver)
        {
            _repository = repository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
            _emailService = emailService;
            _emailGroupResolver = emailGroupResolver;
        }

        public Task<List<VecinoLicenciaDto>> GetLicencias() => _repository.GetLicencias();

        public async Task<VecinoLicenciaDto> CreateLicencia(VecinoLicenciaCreateDto dto, IFormFile file, int userId)
        {
            if (file == null || file.Length == 0)
                throw new AbrilException("No se adjuntó ningún archivo.", 400);
            if (file.Length > MaxBytes)
                throw new AbrilException("El archivo supera el tamaño máximo permitido (15 MB).", 400);

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                throw new AbrilException("Formato no válido. Use PDF, Word, Excel o imagen.", 400);

            if (dto.FechaRecordatorio > dto.FechaVencimiento)
                throw new AbrilException("La fecha de recordatorio no puede ser posterior a la fecha de vencimiento.", 400);

            // Correos destinatarios del recordatorio: al menos uno y con formato válido.
            dto.Emails = (dto.Emails ?? new List<string>())
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .GroupBy(e => e.ToLowerInvariant())
                .Select(g => g.First())
                .ToList();
            if (dto.Emails.Count == 0)
                throw new AbrilException("Debe ingresar al menos un correo para el recordatorio.", 400);
            var invalido = dto.Emails.FirstOrDefault(e => !EmailRegex.IsMatch(e));
            if (invalido != null)
                throw new AbrilException($"El correo '{invalido}' no tiene un formato válido.", 400);

            var dias = dto.FechaVencimiento.DayNumber - dto.FechaRecordatorio.DayNumber;
            if (dias < 0)
                throw new AbrilException("Los días de antelación no pueden ser negativos.", 400);
            // Se recalcula desde las fechas para garantizar consistencia con lo enviado.
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

            return await _repository.CreateLicencia(dto, archivoUrl, file.FileName, userId);
        }

        public async Task<RecordatoriosResultDto> ProcesarRecordatorios()
        {
            // "Hoy" en hora Perú (UTC-5), igual que los demás crons del sistema.
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
            var pendientes = await _repository.GetPendientesRecordatorio(hoy);

            var result = new RecordatoriosResultDto();

            foreach (var licencia in pendientes)
            {
                try
                {
                    if (licencia.Emails.Count == 0)
                        continue; // Licencias antiguas sin destinatarios: no hay a quién avisar.

                    // Desglosar grupos de correo en sus miembros (los individuales pasan tal cual).
                    var destinatarios = await _emailGroupResolver.ExpandAsync(licencia.Emails);
                    if (destinatarios == null || destinatarios.Count == 0)
                        destinatarios = licencia.Emails;

                    var diasRestantes = licencia.FechaVencimiento.DayNumber - hoy.DayNumber;
                    var nombre = string.IsNullOrWhiteSpace(licencia.OriginalFileName)
                        ? "licencia/permiso"
                        : licencia.OriginalFileName;

                    var subject = diasRestantes >= 0
                        ? $"Recordatorio: la licencia \"{nombre}\" vence el {licencia.FechaVencimiento:dd/MM/yyyy}"
                        : $"Alerta: la licencia \"{nombre}\" venció el {licencia.FechaVencimiento:dd/MM/yyyy}";

                    var detalleDias = diasRestantes > 1 ? $"Faltan <b>{diasRestantes} días</b> para su vencimiento."
                        : diasRestantes == 1 ? "Vence <b>mañana</b>."
                        : diasRestantes == 0 ? "Vence <b>hoy</b>."
                        : $"Venció hace <b>{-diasRestantes} día(s)</b>.";

                    var body = $"""
                        <p>Estimados,</p>
                        <p>Este es un recordatorio del <b>Control de Vencimientos</b> de Administración de Obra.</p>
                        <p>La licencia/permiso <b>{nombre}</b> vence el <b>{licencia.FechaVencimiento:dd/MM/yyyy}</b>. {detalleDias}</p>
                        <p>Puede revisarla en la intranet:
                        <a href="https://intranet.abril.pe/vecinos/control-vencimientos">Control de Vencimientos</a></p>
                        <p>Este es un mensaje automático, por favor no responder.</p>
                        """;

                    await _emailService.SendAsync(destinatarios, subject, body, isHtml: true);
                    await _repository.MarcarRecordatorioEnviado(licencia.VecinoLicenciaId);

                    result.LicenciasProcesadas++;
                    result.CorreosEnviados += destinatarios.Count;
                }
                catch (Exception ex)
                {
                    // Un fallo puntual no debe frenar el resto de recordatorios del día.
                    result.Errores.Add($"Licencia {licencia.VecinoLicenciaId}: {ex.Message}");
                }
            }

            return result;
        }
    }
}
