using System.Security.Cryptography;
using System.Text;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Models;
using Abril_Backend.Shared.Services.Notificaciones.Dtos;
using Abril_Backend.Shared.Services.Notificaciones.Interfaces;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
{
    /// <summary>
    /// Aprobación de Gerencia General de la solicitud de personal.
    ///
    /// Flujo: el solicitante registra la solicitud → UN solo correo al Gerente General y al gerente
    /// del área del solicitante, con todas sus vacantes y tres accesos (aprobar todas / elegir
    /// algunas / rechazar todas) → el GG decide en una página pública por token (sin login) →
    /// recién ahí se le notifica a GTH, y solo las vacantes aprobadas.
    ///
    /// Los tres botones del correo NO ejecutan la decisión: abren la página con la acción
    /// preseleccionada y la decisión se confirma con un click ahí. Es a propósito — los clientes de
    /// correo (Outlook/Safe Links) precargan los enlaces, y un enlace que aprobara al abrirse se
    /// dispararía solo.
    /// </summary>
    public class AprobacionGgService : IAprobacionGgService
    {
        private readonly IAprobacionGgRepository   _repo;
        private readonly ICorreoDestinatariosResolver _destinatarios;
        private readonly IEmailService             _email;
        private readonly INotificacionesService    _notificaciones;
        private readonly IConfiguration            _configuration;
        private readonly ILogger<AprobacionGgService> _logger;

        public AprobacionGgService(
            IAprobacionGgRepository repo,
            ICorreoDestinatariosResolver destinatarios,
            IEmailService email,
            INotificacionesService notificaciones,
            IConfiguration configuration,
            ILogger<AprobacionGgService> logger)
        {
            _repo           = repo;
            _destinatarios  = destinatarios;
            _email          = email;
            _notificaciones = notificaciones;
            _configuration  = configuration;
            _logger         = logger;
        }

        /// <summary>Color de la cabecera de los correos de reclutamiento (azul del logo de Abril).</summary>
        private const string AzulAbril = "#005D9D";

        // ── Envío al Gerente General ──────────────────────────────────────────
        public async Task<bool> EnviarSolicitudAGerencia(int solicitudId, int? userId)
        {
            try
            {
                // Token de acceso público (hex, url-safe). Solo se usa si la aprobación no existía.
                var nuevoToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
                var ctx  = await _repo.PrepararEnvio(solicitudId, nuevoToken, userId);
                // Lo que esté activo en la sección "Aprobación GG" de la Configuración:
                // Gerente General, gerente del área del solicitante y correos adicionales.
                var dest = await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.AprobacionGg, ctx.AreaScopeId);

                if (dest.Para.Count == 0)
                {
                    _logger.LogWarning(
                        "No hay destinatarios para el correo de aprobación de Gerencia General " +
                        "(solicitud {SolicitudId}); no se envía. El solicitante puede reintentarlo con «Reenviar a Gerencia General».",
                        solicitudId);
                    return false;
                }

                await EnviarCorreoAsync(ctx, dest.EmailsPara, dest.EmailsCopias, esReenvio: false, userId);
                return true;
            }
            catch (Exception ex)
            {
                // La solicitud ya quedó registrada: un fallo del correo no la revierte. El
                // solicitante lo reintenta desde su panel.
                _logger.LogWarning(ex, "No se pudo enviar el correo de aprobación de Gerencia General de la solicitud {SolicitudId}", solicitudId);
                return false;
            }
        }

        public async Task<AprobacionGgReenvioResultDto> Reenviar(int requerimientoId, int? userId)
        {
            if (!userId.HasValue)
                throw new AbrilException("No se pudo identificar al usuario.", 401);

            var ctx = await _repo.GetEnvioContextoByRequerimiento(requerimientoId, userId.Value);
            if (ctx == null)
                throw new AbrilException("No se encontró la aprobación de Gerencia General de esta solicitud.", 404);
            if (ctx.Decidida)
                throw new AbrilException("Gerencia General ya decidió sobre esta solicitud.", 409);

            // Mismos destinatarios que el primer envío.
            var dest = await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.AprobacionGg, ctx.AreaScopeId);
            if (dest.Para.Count == 0)
                throw new AbrilException(
                    "No hay destinatarios activos para el correo de Gerencia General. " +
                    "Revísalos en «Configuración» (sección «Aprobación de Gerencia General») e inténtalo de nuevo.", 409);

            var principales = dest.EmailsPara;

            // Reenvío bloqueante: el usuario lo pidió explícitamente, así que si falla debe saberlo.
            try
            {
                await EnviarCorreoAsync(ctx, principales, dest.EmailsCopias, esReenvio: true, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló el reenvío del correo de aprobación de Gerencia General (requerimiento {RequerimientoId})", requerimientoId);
                throw new AbrilException("No se pudo enviar el correo a Gerencia General. Vuelve a intentarlo.", 502);
            }

            return new AprobacionGgReenvioResultDto
            {
                Message       = $"Correo reenviado a {string.Join(", ", principales)}.",
                Destinatarios = principales,
            };
        }

        /// <summary>
        /// Envía el correo del GG, deja registrado el envío (destinatarios + fecha) y crea la
        /// notificación in-app para los destinatarios que sean usuarios del sistema.
        /// </summary>
        private async Task EnviarCorreoAsync(
            AprobacionGgEnvioContextoDto ctx, List<string> principales, List<string> copias, bool esReenvio, int? userId)
        {
            var asunto = ctx.Vacantes.Count == 1
                ? $"[Reclutamiento] Aprobación de vacante — {ctx.Vacantes[0].Codigo}"
                : $"[Reclutamiento] Aprobación de {ctx.Vacantes.Count} vacantes — {ctx.Area}";
            if (esReenvio) asunto = $"[Recordatorio] {asunto}";

            await _email.SendAsync(
                to:      principales,
                subject: asunto,
                body:    ConstruirCuerpoGerencia(ctx, esReenvio),
                isHtml:  true,
                cc:      copias.Count > 0 ? copias : null);

            await _repo.RegistrarEnvio(ctx.AprobacionId, principales, copias, esReenvio, userId);

            // Campanita para los destinatarios que sí tengan usuario (los buzones grupales se ignoran).
            // Una sola notificación por solicitud: es un único pendiente de decisión, no uno por vacante.
            try
            {
                var resumen = ctx.Vacantes.Count == 1
                    ? ctx.Vacantes[0].Puesto
                    : $"{ctx.Vacantes.Count} vacantes";
                await _notificaciones.CrearPorCorreosAsync(
                    NotificacionTipoCodigo.GthAprobacionGg,
                    principales.Concat(copias).ToList(),
                    userId,
                    new List<NuevaNotificacionDto>
                    {
                        new()
                        {
                            Titulo      = "Solicitud de personal por aprobar",
                            Subtitulo   = string.IsNullOrWhiteSpace(ctx.Area) ? resumen : $"{resumen} — {ctx.Area}",
                            Descripcion = ctx.Justificacion,
                            Referencia  = string.Join(", ", ctx.Vacantes.Select(v => v.Codigo)),
                        },
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo crear la notificación in-app de la aprobación de Gerencia General (solicitud {SolicitudId})", ctx.SolicitudId);
            }
        }

        /// <summary>
        /// Línea "Reemplaza a {trabajador}" bajo el tipo, en la celda de la tabla de vacantes. Vacía
        /// en las vacantes nuevas y en los requerimientos anteriores a que se pidiera ese dato: se
        /// agrega a la misma celda para no cambiar las columnas de una tabla que ya se lee bien.
        /// </summary>
        private static string CeldaReemplazado(AprobacionGgVacanteDto v) =>
            string.IsNullOrWhiteSpace(v.TrabajadorReemplazado)
                ? ""
                : $"""<br><span style="font-size:11px;color:#6b7280">Reemplaza a {System.Net.WebUtility.HtmlEncode(v.TrabajadorReemplazado)}</span>""";

        /// <summary>
        /// Cuerpo del correo al Gerente General: la solicitud completa en una tabla + tres accesos
        /// (aprobar todas / elegir algunas / rechazar todas) que abren la página de decisión.
        /// </summary>
        private string ConstruirCuerpoGerencia(AprobacionGgEnvioContextoDto ctx, bool esReenvio)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

            var baseUrl = ConstruirLink(ctx.Token, null);
            var linkAprobar  = ConstruirLink(ctx.Token, "aprobar-todo");
            var linkRevisar  = ConstruirLink(ctx.Token, "revisar");
            var linkRechazar = ConstruirLink(ctx.Token, "rechazar-todo");

            var filas = new StringBuilder();
            foreach (var v in ctx.Vacantes)
            {
                filas.Append($"""
                    <tr>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb;font-weight:bold">{Esc(v.Codigo)}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb">{Esc(v.Puesto)}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb">{Esc(v.TipoRequerimiento)}{CeldaReemplazado(v)}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb">{Esc(v.ProyectoObra)}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb;text-align:center">{v.FechaRequeridaIngreso:dd/MM/yyyy}</td>
                    </tr>
                    """);
            }

            var solicitante = string.IsNullOrWhiteSpace(ctx.SolicitanteNombre)
                ? ""
                : $"""<p style="font-size:13px"><b>Solicitante:</b> {Esc(ctx.SolicitanteNombre)}</p>""";

            var justificacion = string.IsNullOrWhiteSpace(ctx.Justificacion)
                ? ""
                : $"""<p style="font-size:13px"><b>Justificación:</b><br>{Esc(ctx.Justificacion)}</p>""";

            var sustento = string.IsNullOrWhiteSpace(ctx.SustentoUrl)
                ? ""
                : $"""<p style="font-size:13px"><b>Sustento adjunto:</b> <a href="{Esc(ctx.SustentoUrl)}">{Esc(ctx.SustentoNombre ?? "ver documento")}</a></p>""";

            var recordatorio = esReenvio
                ? """<p style="font-size:13px;color:#B45309"><b>Recordatorio:</b> esta solicitud sigue pendiente de tu aprobación.</p>"""
                : "";

            // Un solo botón por acción, en una tabla: los clientes de correo no soportan flex/grid.
            return $"""
                <div style="font-family:Arial,sans-serif;max-width:680px">
                  <div style="background:{AzulAbril};padding:12px 16px">
                    <h2 style="color:#fff;margin:0;font-size:18px">Solicitud de personal por aprobar</h2>
                  </div>
                  <div style="padding:16px;border:1px solid #e5e7eb;border-top:none">
                    {recordatorio}
                    <p style="font-size:13px;margin-top:0">
                      El área solicitante registró la siguiente solicitud de personal y necesita tu
                      aprobación antes de que pase a Gestión de Talento Humano.
                    </p>
                    <p style="font-size:13px"><b>Área solicitante:</b> {Esc(ctx.Area)}</p>
                    {solicitante}
                    <p style="font-size:13px"><b>Vacantes solicitadas:</b> {ctx.Vacantes.Count}</p>
                    <table cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;font-size:13px;margin:8px 0">
                      <thead>
                        <tr style="background:#f3f4f6">
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Código</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Puesto</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Tipo</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Proyecto / Obra</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:center">Ingreso requerido</th>
                        </tr>
                      </thead>
                      <tbody>{filas}</tbody>
                    </table>
                    {justificacion}
                    {sustento}

                    <p style="font-size:13px;margin-top:18px"><b>¿Qué quieres hacer?</b></p>
                    <table cellpadding="0" cellspacing="0" style="margin:6px 0 14px">
                      <tr>
                        <td style="padding:0 8px 8px 0">
                          <a href="{linkAprobar}" style="background:#15803D;color:#fff;padding:11px 20px;border-radius:8px;text-decoration:none;display:inline-block;font-size:14px;font-weight:bold">Aprobar todas</a>
                        </td>
                        <td style="padding:0 8px 8px 0">
                          <a href="{linkRevisar}" style="background:{AzulAbril};color:#fff;padding:11px 20px;border-radius:8px;text-decoration:none;display:inline-block;font-size:14px;font-weight:bold">Aprobar solo algunas</a>
                        </td>
                        <td style="padding:0 0 8px 0">
                          <a href="{linkRechazar}" style="background:#B91C1C;color:#fff;padding:11px 20px;border-radius:8px;text-decoration:none;display:inline-block;font-size:14px;font-weight:bold">Rechazar todas</a>
                        </td>
                      </tr>
                    </table>
                    <p style="font-size:12px;color:#555">
                      Cualquiera de las tres opciones abre la solicitud para que confirmes la decisión
                      con un click. No necesitas iniciar sesión.
                    </p>
                    <p style="font-size:12px;color:#555">Si los botones no funcionan, copia y pega este enlace en tu navegador:<br>
                      <span style="color:{AzulAbril};word-break:break-all">{Esc(baseUrl)}</span>
                    </p>
                    <p style="font-size:11px;color:#888;margin-top:16px">Correo automático de Abril One · Gestión GTH · Reclutamiento.</p>
                  </div>
                </div>
                """;
        }

        /// <summary>Enlace a la página pública de decisión, con la acción preseleccionada (opcional).</summary>
        private string ConstruirLink(string token, string? accion)
        {
            var frontendUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
            var link = $"{frontendUrl}/aprobacion-gerencia/solicitud?token={Uri.EscapeDataString(token)}";
            return accion == null ? link : $"{link}&accion={accion}";
        }

        // ── Página pública y decisión ─────────────────────────────────────────
        public async Task<AprobacionGgPublicoDto> GetPublico(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new AbrilException("Enlace de aprobación no válido.", 400);

            var dto = await _repo.GetPublicoByToken(token.Trim());
            if (dto == null)
                throw new AbrilException("El enlace de aprobación no es válido o ya no está disponible.", 404);
            return dto;
        }

        public async Task<AprobacionGgDecisionResultDto> RegistrarDecision(string token, AprobacionGgDecisionDto dto)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new AbrilException("Enlace de aprobación no válido.", 400);
            if (dto?.Decisiones == null || dto.Decisiones.Count == 0)
                throw new AbrilException("Debes aprobar o rechazar las vacantes antes de enviar la decisión.", 400);

            var ctx = await _repo.RegistrarDecision(token.Trim(), dto);
            var res = ctx.Resultado;

            // Solo las vacantes aprobadas llegan a GTH. Best-effort: la decisión ya quedó registrada.
            if (ctx.Aprobadas.Count > 0)
                await NotificarAGthAsync(ctx);

            res.Message = res.Aprobados == 0
                ? "Decisión registrada: rechazaste todas las vacantes. La solicitud no continúa y no se envió a Gestión de Talento Humano."
                : res.Rechazados == 0
                    ? $"Decisión registrada: aprobaste {res.Aprobados} vacante(s). Ya se enviaron a Gestión de Talento Humano para iniciar el reclutamiento."
                    : $"Decisión registrada: aprobaste {res.Aprobados} vacante(s) y rechazaste {res.Rechazados}. Las aprobadas ya se enviaron a Gestión de Talento Humano.";

            return res;
        }

        /// <summary>
        /// Correo + campanita a GTH con las vacantes que Gerencia General aprobó (tipo SOLICITUD).
        /// Es el correo de "nueva solicitud de personal" que antes salía al registrar la solicitud:
        /// ahora espera la aprobación del GG y solo lleva lo aprobado. No bloquea.
        /// </summary>
        private async Task NotificarAGthAsync(AprobacionGgDecisionContextoDto ctx)
        {
            SolicitudDestinatariosDto dest;
            try
            {
                dest = await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.Solicitud);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo resolver a los destinatarios de GTH de la solicitud {SolicitudId}", ctx.SolicitudId);
                return;
            }

            // 1) Correo.
            try
            {
                if (dest.Para.Count > 0) // sin destinatario principal → no se envía
                {
                    var subject = ctx.Aprobadas.Count == 1
                        ? $"[Reclutamiento] Nueva solicitud de personal aprobada — {ctx.Aprobadas[0].Codigo}"
                        : $"[Reclutamiento] Nueva solicitud de personal aprobada — {ctx.Aprobadas.Count} vacantes";

                    await _email.SendAsync(
                        to:      dest.EmailsPara,
                        subject: subject,
                        body:    ConstruirCuerpoGth(ctx),
                        isHtml:  true,
                        cc:      dest.Copias.Count > 0 ? dest.EmailsCopias : null);
                }
                else
                {
                    _logger.LogWarning(
                        "No hay destinatarios principales activos para el correo de nueva solicitud (solicitud {SolicitudId}); no se envía.",
                        ctx.SolicitudId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar el correo de la solicitud de personal {SolicitudId} a GTH", ctx.SolicitudId);
            }

            // 2) Notificación in-app (campanita) — una por vacante aprobada, mismos destinatarios.
            try
            {
                var items = ctx.Aprobadas.Select(v => new NuevaNotificacionDto
                {
                    Titulo      = "Nuevo requerimiento de personal",
                    Subtitulo   = string.IsNullOrWhiteSpace(ctx.Area) ? v.Puesto : $"{v.Puesto} — {ctx.Area}",
                    Descripcion = ctx.Justificacion,
                    Referencia  = v.Codigo,
                }).ToList();

                await _notificaciones.CrearPorCorreosAsync(
                    NotificacionTipoCodigo.GthSolicitudPersonal,
                    dest.EmailsPara.Concat(dest.EmailsCopias).ToList(),
                    null, // el evento lo dispara Gerencia General, que no está autenticada
                    items);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo crear la notificación in-app de la solicitud de personal {SolicitudId}", ctx.SolicitudId);
            }
        }

        /// <summary>Cuerpo del correo a GTH: las vacantes aprobadas por el GG (y las rechazadas como contexto).</summary>
        private static string ConstruirCuerpoGth(AprobacionGgDecisionContextoDto ctx)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

            var filas = new StringBuilder();
            foreach (var v in ctx.Aprobadas)
            {
                filas.Append($"""
                    <tr>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb;font-weight:bold">{Esc(v.Codigo)}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb">{Esc(v.Puesto)}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb">{Esc(v.TipoRequerimiento)}{CeldaReemplazado(v)}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb">{Esc(v.ProyectoObra)}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb;text-align:center">{v.FechaRequeridaIngreso:dd/MM/yyyy}</td>
                    </tr>
                    """);
            }

            var solicitante = string.IsNullOrWhiteSpace(ctx.SolicitanteNombre)
                ? ""
                : $"""<p style="font-size:13px"><b>Solicitante:</b> {Esc(ctx.SolicitanteNombre)}</p>""";

            var justificacion = string.IsNullOrWhiteSpace(ctx.Justificacion)
                ? ""
                : $"""<p style="font-size:13px"><b>Justificación:</b><br>{Esc(ctx.Justificacion)}</p>""";

            var sustento = string.IsNullOrWhiteSpace(ctx.SustentoUrl)
                ? ""
                : $"""<p style="font-size:13px"><b>Sustento adjunto:</b> <a href="{Esc(ctx.SustentoUrl)}">{Esc(ctx.SustentoNombre ?? "ver documento")}</a></p>""";

            var comentario = string.IsNullOrWhiteSpace(ctx.Comentario)
                ? ""
                : $"""<p style="font-size:13px"><b>Comentario de Gerencia General:</b><br>{Esc(ctx.Comentario)}</p>""";

            // Las rechazadas se listan solo como contexto: no generan trabajo para GTH.
            var rechazadas = ctx.Rechazadas.Count == 0
                ? ""
                : $"""
                    <p style="font-size:13px;color:#B91C1C">
                      <b>No aprobadas ({ctx.Rechazadas.Count}):</b>
                      {Esc(string.Join(", ", ctx.Rechazadas.Select(v => $"{v.Codigo} ({v.Puesto})")))}.
                      Estas vacantes no continúan en el proceso.
                    </p>
                    """;

            return $"""
                <div style="font-family:Arial,sans-serif;max-width:680px">
                  <div style="background:{AzulAbril};padding:12px 16px">
                    <h2 style="color:#fff;margin:0;font-size:18px">Nueva solicitud de personal aprobada</h2>
                  </div>
                  <div style="padding:16px;border:1px solid #e5e7eb;border-top:none">
                    <p style="font-size:13px;margin-top:0">
                      <b>Gerencia General aprobó</b> las siguientes vacantes. Ya están en la bandeja de
                      reclutamiento para iniciar el proceso.
                    </p>
                    <p style="font-size:13px"><b>Área solicitante:</b> {Esc(ctx.Area)}</p>
                    {solicitante}
                    <p style="font-size:13px"><b>Vacantes aprobadas:</b> {ctx.Aprobadas.Count}</p>
                    <table cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;font-size:13px;margin:8px 0">
                      <thead>
                        <tr style="background:#f3f4f6">
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Código</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Puesto</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Tipo</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Proyecto / Obra</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:center">Ingreso requerido</th>
                        </tr>
                      </thead>
                      <tbody>{filas}</tbody>
                    </table>
                    {rechazadas}
                    {justificacion}
                    {comentario}
                    {sustento}
                    <p style="font-size:11px;color:#888;margin-top:16px">Correo automático de Abril One · Gestión GTH · Reclutamiento.</p>
                  </div>
                </div>
                """;
        }
    }
}
