using System.Text;
using System.Text.RegularExpressions;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Models;
using Abril_Backend.Shared.Services.Notificaciones.Dtos;
using Abril_Backend.Shared.Services.Notificaciones.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
{
    public class ReclutamientoService : IReclutamientoService
    {
        private readonly IReclutamientoRepository _repo;
        private readonly IGraphSharePointService  _sharePoint;
        private readonly IEmailService            _email;
        private readonly INotificacionesService   _notificaciones;
        private readonly ILogger<ReclutamientoService> _logger;

        private const long MaxSustentoBytes = 10 * 1024 * 1024; // 10 MB
        private static readonly string[] AllowedSustentoExt = { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };

        public ReclutamientoService(
            IReclutamientoRepository repo,
            IGraphSharePointService sharePoint,
            IEmailService email,
            INotificacionesService notificaciones,
            ILogger<ReclutamientoService> logger)
        {
            _repo           = repo;
            _sharePoint     = sharePoint;
            _email          = email;
            _notificaciones = notificaciones;
            _logger         = logger;
        }

        public Task<ReclutamientoFormDataDto> GetFormData(int? userId) => _repo.GetFormData(userId);

        public Task<SolicitantePanelDto> GetSolicitantePanel(int? userId) =>
            userId.HasValue
                ? _repo.GetSolicitantePanel(userId.Value)
                : Task.FromResult(new SolicitantePanelDto());

        public async Task<RevisionLongListDto> GetRevisionLongList(int requerimientoId, int? userId)
        {
            if (!userId.HasValue)
                throw new AbrilException("No se pudo identificar al usuario.", 401);

            var revision = await _repo.GetRevisionLongList(requerimientoId, userId.Value);
            if (revision == null)
                throw new AbrilException("No se encontró la long list del requerimiento.", 404);

            return revision;
        }

        public Task<BandejaReclutamientoDto> GetBandeja() => _repo.GetBandeja();

        public Task UpdatePrioridad(int requerimientoId, int prioridadId, int? userId) =>
            _repo.UpdatePrioridad(requerimientoId, prioridadId, userId);

        public async Task<DetalleRequerimientoGthDto> GetDetalleGth(int requerimientoId)
        {
            var detalle = await _repo.GetDetalleGth(requerimientoId);
            if (detalle == null)
                throw new AbrilException("Requerimiento no encontrado.", 404);
            return detalle;
        }

        public Task UpdateAsignacionGth(int requerimientoId, AsignacionGthUpdateDto dto, int? userId)
        {
            if (dto == null)
                throw new AbrilException("Datos de la asignación no recibidos.", 400);
            return _repo.UpdateAsignacionGth(requerimientoId, dto, userId);
        }

        public Task<EstadoRequerimientoResultDto> ReplacePublicaciones(int requerimientoId, PublicacionesUpdateDto dto, int? userId)
        {
            // Publicar avanza el pipeline: se exige al menos un canal (ya no hay flujo de "despublicar").
            if (dto?.CanalIds == null || dto.CanalIds.Count == 0)
                throw new AbrilException("Selecciona al menos un canal de publicación.", 400);
            return _repo.ReplacePublicaciones(requerimientoId, dto.CanalIds, userId);
        }

        public Task<EstadoRequerimientoResultDto> IniciarRevisionCv(int requerimientoId, int? userId) =>
            _repo.IniciarRevisionCv(requerimientoId, userId);

        // ── Envío de la long list al solicitante ──────────────────────────────
        // Topes de tamaño de la long list. Antes eran 20 MB (tanto el total como cada archivo); se
        // subieron a 3 GB para el total de la petición y a 3 GB para cada archivo individual. Los
        // topes de request de Kestrel/FormOptions (Program.cs) ya están en 10 GB, así que no limitan.
        // OJO: usar el sufijo L (long) — 3 * 1024^3 desborda un int.
        private const long MaxLongListTotalBytes = 3L * 1024 * 1024 * 1024; // 3 GB en total (CVs + informes)
        private const long MaxLongListFileBytes  = 3L * 1024 * 1024 * 1024; // 3 GB por archivo individual
        private static readonly string[] AllowedLongListExt = { ".pdf", ".doc", ".docx" };

        public async Task<EstadoRequerimientoResultDto> EnviarLongList(
            int requerimientoId, List<LongListCandidatoArchivoDto> candidatos, int? userId)
        {
            if (candidatos == null || candidatos.Count == 0)
                throw new AbrilException("Debes cargar al menos un candidato para enviar la long list.", 400);

            // Validar archivos: cada candidato debe traer su CV; formato e informe (opcional) permitidos.
            long total = 0;
            for (int i = 0; i < candidatos.Count; i++)
            {
                var c = candidatos[i];
                var pos = i + 1;
                if (c.CvContent == null || c.CvContent.Length == 0)
                    throw new AbrilException($"Candidato {pos}: falta adjuntar el CV.", 400);
                ValidarLongListArchivo($"CV del candidato {pos}", c.CvFileName, c.CvContent.Length);
                total += c.CvContent.Length;

                if (c.InformeContent != null && c.InformeContent.Length > 0)
                {
                    ValidarLongListArchivo($"informe del candidato {pos}", c.InformeFileName ?? "", c.InformeContent.Length);
                    total += c.InformeContent.Length;
                }
            }
            if (total > MaxLongListTotalBytes)
                throw new AbrilException("El tamaño total de los CVs e informes supera el máximo permitido (3 GB).", 400);

            // 1) Contexto (valida fase LONG_LIST) — no cambia estado todavía.
            var ctx = await _repo.GetLongListEnvioContexto(requerimientoId);

            // 2) Destinatarios del correo de long list.
            //    El destinatario PRINCIPAL (Para/To) es SIEMPRE el solicitante que registró la
            //    solicitud; la configuración (tipo LONG_LIST) solo aporta principales/copias extra.
            var dest = await _repo.GetCorreoDestinatarios(CorreoTipoReclutamiento.LongList);

            // Para = solicitante primero + principales configurados (deduplicado, sin distinguir mayúsculas).
            var principales = new List<string>();
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void AgregarPrincipal(string? email)
            {
                var e = email?.Trim();
                if (!string.IsNullOrWhiteSpace(e) && vistos.Add(e)) principales.Add(e);
            }
            AgregarPrincipal(ctx.SolicitanteEmail);
            foreach (var e in dest.Principales) AgregarPrincipal(e);

            if (principales.Count == 0)
                throw new AbrilException(
                    "No se pudo determinar el correo del solicitante de la long list y no hay " +
                    "destinatarios principales configurados. Verifica que el solicitante tenga " +
                    "un correo registrado o configúralos con el botón «Configuración».", 409);

            // CC = copias configuradas que no estén ya en Para.
            var copias = dest.Copias.Where(e => !vistos.Contains(e.Trim())).ToList();

            // 3) Enviar el correo con los CVs/informes adjuntos. Es BLOQUEANTE y va ANTES de avanzar
            //    el estado: si el correo falla, el requerimiento sigue en LONG_LIST y GTH puede reintentar.
            var adjuntos = new List<EmailAttachment>();
            foreach (var c in candidatos)
            {
                adjuntos.Add(new EmailAttachment
                {
                    FileName    = string.IsNullOrWhiteSpace(c.CvFileName) ? "cv.pdf" : c.CvFileName,
                    ContentType = string.IsNullOrWhiteSpace(c.CvContentType) ? "application/octet-stream" : c.CvContentType,
                    Content     = c.CvContent!,
                });
                if (c.InformeContent != null && c.InformeContent.Length > 0)
                {
                    adjuntos.Add(new EmailAttachment
                    {
                        FileName    = string.IsNullOrWhiteSpace(c.InformeFileName) ? "informe.pdf" : c.InformeFileName!,
                        ContentType = string.IsNullOrWhiteSpace(c.InformeContentType) ? "application/octet-stream" : c.InformeContentType!,
                        Content     = c.InformeContent!,
                    });
                }
            }

            try
            {
                await _email.SendAsync(
                    to:      principales,
                    subject: $"[Reclutamiento] Long list de CVs — {ctx.Codigo} · {ctx.Puesto}",
                    body:    ConstruirCuerpoLongList(ctx, candidatos),
                    isHtml:  true,
                    cc:      copias.Count > 0 ? copias : null,
                    attachments: adjuntos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló el correo de long list del requerimiento {RequerimientoId}", requerimientoId);
                throw new AbrilException(
                    "No se pudo enviar el correo de la long list. El requerimiento no cambió de estado; reintenta.", 502);
            }

            // 4) Correo enviado: subir los CVs/informes a SharePoint y persistir la long list para
            //    que el solicitante pueda revisarla. Se reutiliza la carpeta de reclutamiento
            //    (gth_sustento_folder), organizada en una subcarpeta por requerimiento.
            var carpeta = await ResolverCarpetaLongListAsync(ctx.Codigo);

            var persist = new List<LongListCandidatoPersistDto>(candidatos.Count);
            var indice = 0;
            foreach (var c in candidatos)
            {
                indice++;
                var cvSubida = await SubirLongListArchivoAsync(
                    carpeta, "cv", ctx.Codigo, indice, c.CvFileName, c.CvContent!, c.CvContentType);

                var item = new LongListCandidatoPersistDto
                {
                    Nombre           = c.Nombre,
                    Puesto           = c.Puesto,
                    ExperienciaAnios = c.ExperienciaAnios,
                    Disponibilidad   = c.Disponibilidad,
                    FuenteCanalId    = c.FuenteCanalId,
                    Comentario       = c.Comentario,
                    CvNombre         = cvSubida.FileName,
                    CvUrl            = cvSubida.WebUrl,
                    CvItemId         = cvSubida.ItemId,
                    CvDriveId        = carpeta.DriveId,
                };

                if (c.InformeContent != null && c.InformeContent.Length > 0)
                {
                    var infSubida = await SubirLongListArchivoAsync(
                        carpeta, "informe", ctx.Codigo, indice, c.InformeFileName ?? "informe.pdf",
                        c.InformeContent, c.InformeContentType ?? "application/octet-stream");
                    item.InformeNombre  = infSubida.FileName;
                    item.InformeUrl     = infSubida.WebUrl;
                    item.InformeItemId  = infSubida.ItemId;
                    item.InformeDriveId = carpeta.DriveId;
                }

                persist.Add(item);
            }

            // 5) Persistir los candidatos (reemplazando la long list previa) y avanzar a LONG_LIST_ENVIADA.
            return await _repo.GuardarLongListCandidatos(requerimientoId, persist, userId);
        }

        /// <summary>Resuelve la carpeta de reclutamiento (gth_sustento_folder) y la subcarpeta del requerimiento.</summary>
        private async Task<ShareLinkResolveDto> ResolverCarpetaLongListAsync(string codigo)
        {
            var folderUrl = await _repo.GetSustentoFolderUrl();
            if (string.IsNullOrWhiteSpace(folderUrl))
                throw new AbrilException("No está configurada la carpeta de archivos de reclutamiento.", 500);

            var raiz = await _sharePoint.ResolveSharePointFolderUrlAsync(folderUrl);
            if (raiz == null || !raiz.IsFolder)
                throw new AbrilException("No se pudo resolver la carpeta de reclutamiento en SharePoint.", 502);

            // Subcarpeta por requerimiento para agrupar los CVs de su long list.
            try
            {
                var subItemId = await _sharePoint.EnsureChildFolderAsync(
                    raiz.DriveId, raiz.ItemId, $"Long list {SanitizeFilename(codigo)}");
                return new ShareLinkResolveDto { DriveId = raiz.DriveId, ItemId = subItemId, IsFolder = true };
            }
            catch (Exception ex)
            {
                // Si no se pudo crear la subcarpeta, se cae a la carpeta raíz (los nombres de archivo
                // ya incluyen el código del requerimiento, así que no colisionan).
                _logger.LogWarning(ex, "No se pudo crear la subcarpeta de long list de {Codigo}; se usa la carpeta raíz", codigo);
                return raiz;
            }
        }

        /// <summary>Sube un archivo (CV o informe) de la long list a la carpeta indicada y devuelve el resultado.</summary>
        private async Task<SharePointUploadResultDto> SubirLongListArchivoAsync(
            ShareLinkResolveDto carpeta, string prefijo, string codigo, int pos,
            string origFileName, byte[] content, string contentType)
        {
            var ext      = Path.GetExtension(origFileName);
            var stamp    = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var filename = $"{prefijo}_{SanitizeFilename(codigo)}_{pos}_{stamp}{ext}";

            try
            {
                using var stream = new MemoryStream(content);
                var result = await _sharePoint.UploadToOneDriveFolderAsync(
                    carpeta.DriveId, carpeta.ItemId, filename,
                    stream, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                    autoRenameOnLock: true);

                if (result?.WebUrl is null)
                    throw new AbrilException("No se pudo subir un archivo de la long list a SharePoint.", 502);

                return result;
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la subida de un archivo de la long list ({Prefijo}) del requerimiento {Codigo}", prefijo, codigo);
                throw new AbrilException("Error al subir los archivos de la long list a SharePoint.", 502);
            }
        }

        private static void ValidarLongListArchivo(string etiqueta, string fileName, long length)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (!AllowedLongListExt.Contains(ext))
                throw new AbrilException($"El {etiqueta} tiene un formato no permitido. Solo PDF, DOC o DOCX.", 400);
            if (length > MaxLongListFileBytes)
                throw new AbrilException($"El {etiqueta} supera el tamaño máximo permitido (3 GB).", 400);
        }

        private static string ConstruirCuerpoLongList(LongListEnvioContextoDto ctx, List<LongListCandidatoArchivoDto> candidatos)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

            var filas = new StringBuilder();
            for (int i = 0; i < candidatos.Count; i++)
            {
                var c = candidatos[i];
                var comentario = string.IsNullOrWhiteSpace(c.Comentario) ? "—" : Esc(c.Comentario);
                var fuente     = string.IsNullOrWhiteSpace(c.FuenteNombre) ? "—" : Esc(c.FuenteNombre);
                var nombre     = string.IsNullOrWhiteSpace(c.Nombre) ? $"Candidato {i + 1}" : Esc(c.Nombre);
                var informe    = c.InformeContent != null && c.InformeContent.Length > 0 ? "Sí" : "No";
                filas.Append($"""
                    <tr>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb;text-align:center">{i + 1}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb;font-weight:bold">{nombre}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb">{fuente}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb">{comentario}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb;text-align:center">{informe}</td>
                    </tr>
                    """);
            }

            var sla = ctx.SlaDias.HasValue
                ? $"""<p style="font-size:13px"><b>Plazo estimado del proceso:</b> {ctx.SlaDias} días</p>"""
                : "";

            return $"""
                <div style="font-family:Arial,sans-serif;max-width:680px">
                  <div style="background:#005D9D;padding:12px 16px">
                    <h2 style="color:#fff;margin:0;font-size:18px">Long list de CVs para revisión</h2>
                  </div>
                  <div style="padding:16px;border:1px solid #e5e7eb;border-top:none">
                    <p style="font-size:13px;margin-top:0">
                      GTH culminó el filtro de CVs y comparte la <b>long list</b> del siguiente requerimiento
                      para tu revisión. Los CVs (e informes, si los hay) van adjuntos a este correo.
                    </p>
                    <p style="font-size:13px"><b>Requerimiento:</b> {Esc(ctx.Codigo)}</p>
                    <p style="font-size:13px"><b>Puesto:</b> {Esc(ctx.Puesto)}</p>
                    <p style="font-size:13px"><b>Área solicitante:</b> {Esc(ctx.Area) }</p>
                    <p style="font-size:13px"><b>Proyecto / Obra:</b> {Esc(ctx.ProyectoObra) }</p>
                    <p style="font-size:13px"><b>Fecha requerida de ingreso:</b> {ctx.FechaRequeridaIngreso:dd/MM/yyyy}</p>
                    {sla}
                    <table cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;font-size:13px;margin:10px 0">
                      <thead>
                        <tr style="background:#f3f4f6">
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:center">#</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Candidato</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Fuente</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Comentario de GTH</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:center">Informe</th>
                        </tr>
                      </thead>
                      <tbody>{filas}</tbody>
                    </table>
                    <p style="font-size:13px">Total de candidatos en la long list: <b>{candidatos.Count}</b>.</p>
                    <p style="font-size:11px;color:#888;margin-top:16px">Correo automático de Abril One · Gestión GTH · Reclutamiento.</p>
                  </div>
                </div>
                """;
        }

        public async Task<SeguimientoDto> GetSeguimiento(int requerimientoId, int? userId)
        {
            if (!userId.HasValue)
                throw new AbrilException("No se pudo identificar al usuario.", 401);

            var seguimiento = await _repo.GetSeguimiento(requerimientoId, userId.Value);
            if (seguimiento == null)
                throw new AbrilException("Requerimiento no encontrado.", 404);

            return seguimiento;
        }

        // ── Configuración de destinatarios del correo (por tipo: SOLICITUD / LONG_LIST) ─
        public Task<CorreoDestinatariosDto> GetCorreoDestinatarios(string tipoCodigo) =>
            _repo.GetCorreoDestinatarios(tipoCodigo);

        public async Task SaveCorreoDestinatarios(string tipoCodigo, CorreoDestinatariosDto dto, int? userId)
        {
            // Normaliza (trim + minúsculas), valida formato y quita duplicados. Un correo que
            // aparezca en ambas listas se toma como principal (gana Para sobre CC).
            var principales = NormalizarCorreos(dto?.Principales, "principales");
            var copias      = NormalizarCorreos(dto?.Copias, "en copia")
                                .Where(e => !principales.Contains(e))
                                .ToList();

            await _repo.ReplaceCorreoDestinatarios(tipoCodigo, principales, copias, userId);
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
        /// Notifica la nueva solicitud a los destinatarios configurados (gth_correo_destinatario):
        /// correo (To = principales, CC = copias; sin principal no se envía) + notificación in-app
        /// de la campanita (una por requerimiento, para principales y copias que tengan usuario).
        /// Ninguna de las dos bloquea la creación: si fallan solo se registra el warning.
        /// </summary>
        private async Task EnviarNotificacionAsync(int solicitudId, GthSolicitud solicitud)
        {
            CorreoDestinatariosDto dest;
            List<SolicitudVacanteListItemDto> vacantes;
            try
            {
                dest     = await _repo.GetCorreoDestinatarios(CorreoTipoReclutamiento.Solicitud);
                vacantes = await _repo.GetRequerimientosBySolicitud(solicitudId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo notificar la solicitud de personal {SolicitudId}", solicitudId);
                return;
            }

            // 1) Correo.
            try
            {
                if (dest.Principales.Count > 0) // sin destinatario principal → no se envía
                {
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
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar el correo de la solicitud de personal {SolicitudId}", solicitudId);
            }

            // 2) Notificación in-app (campanita) — mismos destinatarios (principales + copias).
            try
            {
                var items = vacantes.Select(v => new NuevaNotificacionDto
                {
                    Titulo      = "Nuevo requerimiento de personal",
                    Subtitulo   = string.IsNullOrWhiteSpace(v.Area) ? v.Puesto : $"{v.Puesto} — {v.Area}",
                    Descripcion = solicitud.Justificacion,
                    Referencia  = v.Codigo,
                }).ToList();

                await _notificaciones.CrearPorCorreosAsync(
                    NotificacionTipoCodigo.GthSolicitudPersonal,
                    dest.Principales.Concat(dest.Copias).ToList(),
                    solicitud.SolicitanteUserId,
                    items);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo crear la notificación in-app de la solicitud de personal {SolicitudId}", solicitudId);
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
