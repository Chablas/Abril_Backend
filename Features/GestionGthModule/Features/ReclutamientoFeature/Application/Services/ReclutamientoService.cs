using System.Text;
using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
{
    public class ReclutamientoService : IReclutamientoService
    {
        private readonly IReclutamientoRepository _repo;
        private readonly IGraphSharePointService  _sharePoint;
        private readonly IEmailService            _email;
        private readonly ILogger<ReclutamientoService> _logger;

        private const long MaxSustentoBytes = 10 * 1024 * 1024; // 10 MB
        private static readonly string[] AllowedSustentoExt = { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };

        public ReclutamientoService(
            IReclutamientoRepository repo,
            IGraphSharePointService sharePoint,
            IEmailService email,
            ILogger<ReclutamientoService> logger)
        {
            _repo       = repo;
            _sharePoint = sharePoint;
            _email      = email;
            _logger     = logger;
        }

        public Task<ReclutamientoFormDataDto> GetFormData(int? userId) => _repo.GetFormData(userId);

        public Task<List<SolicitudVacanteListItemDto>> GetMisSolicitudes(int? userId) =>
            userId.HasValue
                ? _repo.GetMisSolicitudesVacante(userId.Value)
                : Task.FromResult(new List<SolicitudVacanteListItemDto>());

        public async Task<SeguimientoDto> GetSeguimiento(int requerimientoId, int? userId)
        {
            if (!userId.HasValue)
                throw new AbrilException("No se pudo identificar al usuario.", 401);

            var seguimiento = await _repo.GetSeguimiento(requerimientoId, userId.Value);
            if (seguimiento == null)
                throw new AbrilException("Requerimiento no encontrado.", 404);

            return seguimiento;
        }

        // ── Configuración de destinatarios del correo ─────────────────────────
        public Task<CorreoDestinatariosDto> GetCorreoDestinatarios() => _repo.GetCorreoDestinatarios();

        public async Task SaveCorreoDestinatarios(CorreoDestinatariosDto dto, int? userId)
        {
            // Normaliza (trim + minúsculas), valida formato y quita duplicados. Un correo que
            // aparezca en ambas listas se toma como principal (gana Para sobre CC).
            var principales = NormalizarCorreos(dto?.Principales, "principales");
            var copias      = NormalizarCorreos(dto?.Copias, "en copia")
                                .Where(e => !principales.Contains(e))
                                .ToList();

            await _repo.ReplaceCorreoDestinatarios(principales, copias, userId);
        }

        private static readonly Regex EmailRegex =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private static List<string> NormalizarCorreos(List<string>? correos, string listaNombre)
        {
            var resultado = new List<string>();
            if (correos == null) return resultado;
            foreach (var raw in correos)
            {
                var email = raw?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(email)) continue;
                if (!EmailRegex.IsMatch(email))
                    throw new AbrilException($"El correo «{raw}» (destinatarios {listaNombre}) no es válido.", 400);
                if (!resultado.Contains(email)) resultado.Add(email);
            }
            return resultado;
        }

        public async Task<SolicitudPersonalCreateResultDto> Create(SolicitudPersonalCreateDto dto, int? userId, IFormFile? sustento)
        {
            if (dto?.Vacantes == null || dto.Vacantes.Count == 0)
                throw new AbrilException("Debe registrar al menos una vacante.", 400);
            if (dto.Vacantes.Count > 10)
                throw new AbrilException("Una solicitud permite un máximo de 10 vacantes.", 400);

            for (int i = 0; i < dto.Vacantes.Count; i++)
            {
                var v = dto.Vacantes[i];
                var pos = i + 1;
                if (v.PuestoId <= 0)              throw new AbrilException($"Vacante {pos}: debe seleccionar un puesto.", 400);
                if (v.TipoRequerimientoId <= 0)   throw new AbrilException($"Vacante {pos}: debe seleccionar el tipo de requerimiento.", 400);
                if (v.ProjectId <= 0)             throw new AbrilException($"Vacante {pos}: debe seleccionar un proyecto/obra.", 400);
                if (v.FechaRequeridaIngreso == default)
                    throw new AbrilException($"Vacante {pos}: debe indicar la fecha requerida de ingreso.", 400);
            }

            // Área del solicitante: se deriva del usuario autenticado (no se confía en el cliente).
            string? areaNombre = null;
            int? areaScopeId = null, workerId = null;
            if (userId.HasValue)
                (areaNombre, areaScopeId, workerId) = await _repo.ResolveSolicitante(userId.Value);

            var solicitud = new GthSolicitud
            {
                AreaNombre          = areaNombre,
                AreaScopeId         = areaScopeId,
                SolicitanteUserId   = userId,
                SolicitanteWorkerId = workerId,
                Justificacion       = string.IsNullOrWhiteSpace(dto.Justificacion) ? null : dto.Justificacion.Trim(),
            };

            // Sustento (opcional): validar y subir a SharePoint ANTES de persistir.
            if (sustento != null && sustento.Length > 0)
                await SubirSustentoAsync(sustento, solicitud);

            var result = await _repo.Create(solicitud, dto.Vacantes, userId);

            // Notifica a los destinatarios configurados. No bloquea la creación: si el
            // correo falla, la solicitud ya quedó registrada (solo se registra el warning).
            await EnviarNotificacionAsync(result.SolicitudId, solicitud);

            return result;
        }

        /// <summary>
        /// Envía el correo de "nueva solicitud de personal" a los destinatarios configurados
        /// (To = principales, CC = copias). Si no hay ningún principal configurado, no envía nada.
        /// </summary>
        private async Task EnviarNotificacionAsync(int solicitudId, GthSolicitud solicitud)
        {
            try
            {
                var dest = await _repo.GetCorreoDestinatarios();
                if (dest.Principales.Count == 0) return; // sin destinatario principal → no se envía

                var vacantes = await _repo.GetRequerimientosBySolicitud(solicitudId);

                var subject = vacantes.Count == 1
                    ? $"[Reclutamiento] Nueva solicitud de personal — {vacantes[0].Codigo}"
                    : $"[Reclutamiento] Nueva solicitud de personal — {vacantes.Count} vacantes";

                await _email.SendAsync(
                    to:     dest.Principales,
                    subject: subject,
                    body:    ConstruirCuerpo(solicitud, vacantes),
                    isHtml:  true,
                    cc:      dest.Copias.Count > 0 ? dest.Copias : null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar la notificación de la solicitud de personal {SolicitudId}", solicitudId);
            }
        }

        private static string ConstruirCuerpo(GthSolicitud solicitud, List<SolicitudVacanteListItemDto> vacantes)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

            var filas = new StringBuilder();
            foreach (var v in vacantes)
            {
                filas.Append($"""
                    <tr>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb;font-weight:bold">{Esc(v.Codigo)}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb">{Esc(v.Puesto)}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb">{Esc(v.ProyectoObra) }</td>
                    </tr>
                    """);
            }

            var sustento = string.IsNullOrWhiteSpace(solicitud.SustentoUrl)
                ? ""
                : $"""<p style="font-size:13px"><b>Sustento adjunto:</b> <a href="{Esc(solicitud.SustentoUrl)}">{Esc(solicitud.SustentoNombre ?? "ver documento")}</a></p>""";

            var justificacion = string.IsNullOrWhiteSpace(solicitud.Justificacion)
                ? ""
                : $"""<p style="font-size:13px"><b>Justificación:</b><br>{Esc(solicitud.Justificacion)}</p>""";

            return $"""
                <div style="font-family:Arial,sans-serif;max-width:640px">
                  <div style="background:#005D9D;padding:12px 16px">
                    <h2 style="color:#fff;margin:0;font-size:18px">Nueva solicitud de personal</h2>
                  </div>
                  <div style="padding:16px;border:1px solid #e5e7eb;border-top:none">
                    <p style="font-size:13px;margin-top:0"><b>Área solicitante:</b> {Esc(solicitud.AreaNombre) }</p>
                    <p style="font-size:13px"><b>Vacantes solicitadas:</b> {vacantes.Count}</p>
                    <table cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;font-size:13px;margin:8px 0">
                      <thead>
                        <tr style="background:#f3f4f6">
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Código</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Puesto</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Proyecto / Obra</th>
                        </tr>
                      </thead>
                      <tbody>{filas}</tbody>
                    </table>
                    {justificacion}
                    {sustento}
                    <p style="font-size:11px;color:#888;margin-top:16px">Correo automático de Abril One · Gestión GTH · Reclutamiento.</p>
                  </div>
                </div>
                """;
        }

        private async Task SubirSustentoAsync(IFormFile sustento, GthSolicitud solicitud)
        {
            var ext = Path.GetExtension(sustento.FileName).ToLowerInvariant();
            if (!AllowedSustentoExt.Contains(ext))
                throw new AbrilException("Formato de sustento no permitido. Solo PDF, DOC, DOCX, XLS y XLSX.", 400);
            if (sustento.Length > MaxSustentoBytes)
                throw new AbrilException("El sustento supera el tamaño máximo permitido (10 MB).", 400);

            // Carpeta destino: link de SharePoint definido en BD (gth_sustento_folder).
            // Se configura por base de datos: dev y prod apuntan a bibliotecas distintas.
            var folderUrl = await _repo.GetSustentoFolderUrl();
            if (string.IsNullOrWhiteSpace(folderUrl))
                throw new AbrilException("No está configurada la carpeta de sustentos de reclutamiento.", 500);

            var carpeta = await _sharePoint.ResolveSharePointFolderUrlAsync(folderUrl);
            if (carpeta == null || !carpeta.IsFolder)
                throw new AbrilException("No se pudo resolver la carpeta de sustentos en SharePoint.", 502);

            var safeName = SanitizeFilename(Path.GetFileNameWithoutExtension(sustento.FileName));
            var stamp    = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var filename = $"sustento_{stamp}_{safeName}{ext}";

            try
            {
                using var stream = sustento.OpenReadStream();
                var result = await _sharePoint.UploadToOneDriveFolderAsync(
                    carpeta.DriveId, carpeta.ItemId, filename, stream,
                    sustento.ContentType ?? "application/octet-stream",
                    autoRenameOnLock: true);

                if (result?.WebUrl is null)
                    throw new AbrilException("No se pudo subir el sustento a SharePoint.", 502);

                solicitud.SustentoNombre  = result.FileName ?? filename;
                solicitud.SustentoUrl     = result.WebUrl;
                solicitud.SustentoItemId  = result.ItemId;
                solicitud.SustentoDriveId = carpeta.DriveId;
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la subida del sustento de la solicitud de personal");
                throw new AbrilException("Error al subir el sustento a SharePoint.", 502);
            }
        }

        private static string SanitizeFilename(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "sustento";
            var invalid = Path.GetInvalidFileNameChars().Concat(new[] { ' ', '#', '%', '&', '+' }).ToHashSet();
            var clean = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return clean.Length > 60 ? clean.Substring(0, 60) : clean;
        }
    }
}
