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
    /// Aprobación de la solicitud de personal, en dos niveles.
    ///
    /// Flujo: el solicitante registra la solicitud → UN solo correo al Gerente General y al gerente
    /// del área del solicitante, con todas sus vacantes y un enlace a la pantalla «Aprobaciones»
    /// del módulo Gestión GTH → cada uno decide ahí (con su sesión; si no la tiene, el login lo
    /// devuelve a esa misma pantalla) → cuando la decisión es la de <b>Gerencia General</b> recién
    /// ahí se le notifica a GTH y a TI, y solo lo aprobado.
    ///
    /// Los dos niveles son independientes y sin orden impuesto. El visto bueno del gerente del área
    /// es redundante por diseño: queda registrado, se muestra en la pantalla y viaja como contexto
    /// en el correo a GTH, pero no mueve el pipeline ni dispara correos. La aprobación de Gerencia
    /// General es la obligatoria para toda solicitud.
    ///
    /// El enlace del correo NO ejecuta la decisión: abre la solicitud y la decisión se confirma
    /// dentro de la app. Es a propósito — los clientes de correo (Outlook/Safe Links) precargan
    /// los enlaces, y un enlace que aprobara al abrirse se dispararía solo.
    /// </summary>
    public class AprobacionGgService : IAprobacionGgService
    {
        private readonly IAprobacionGgRepository   _repo;
        private readonly IAprobacionScopeResolver  _scopes;
        private readonly ICorreoDestinatariosResolver _destinatarios;
        private readonly IEmailService             _email;
        private readonly INotificacionesService    _notificaciones;
        private readonly IConfiguration            _configuration;
        private readonly ILogger<AprobacionGgService> _logger;

        public AprobacionGgService(
            IAprobacionGgRepository repo,
            IAprobacionScopeResolver scopes,
            ICorreoDestinatariosResolver destinatarios,
            IEmailService email,
            INotificacionesService notificaciones,
            IConfiguration configuration,
            ILogger<AprobacionGgService> logger)
        {
            _repo           = repo;
            _scopes         = scopes;
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
                // Identificador aleatorio de la fila (columna NOT NULL con índice único). Ya no da
                // acceso a nada: la decisión se toma dentro de la app, con sesión.
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
                throw new AbrilException("No se encontró la aprobación de esta solicitud.", 404);
            // Solo cierra la decisión del GG: mientras ella siga pendiente, reenviar tiene sentido
            // aunque el gerente del área ya haya dado su visto bueno.
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
        /// Cuerpo del correo a los gerentes: la solicitud completa en una tabla + un acceso a la
        /// pantalla «Aprobaciones», donde cada uno decide vacante por vacante. Es UN solo correo
        /// con varios destinatarios (Gerente General y gerente del área), así que el texto no puede
        /// hablarle a uno solo: dentro de la pantalla cada quien ve su propia casilla.
        /// </summary>
        private string ConstruirCuerpoGerencia(AprobacionGgEnvioContextoDto ctx, bool esReenvio)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

            var link = ConstruirLink(ctx.AprobacionId);

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

            // El botón va dentro de una tabla: los clientes de correo no soportan flex/grid.
            return $"""
                <div style="font-family:Arial,sans-serif;max-width:680px">
                  <div style="background:{AzulAbril};padding:12px 16px">
                    <h2 style="color:#fff;margin:0;font-size:18px">Solicitud de personal por aprobar</h2>
                  </div>
                  <div style="padding:16px;border:1px solid #e5e7eb;border-top:none">
                    {recordatorio}
                    <p style="font-size:13px;margin-top:0">
                      El área solicitante registró la siguiente solicitud de personal y necesita tu
                      revisión. La aprobación de <b>Gerencia General</b> es la que la deja pasar a
                      Gestión de Talento Humano; el <b>gerente del área</b> registra su visto bueno
                      en la misma pantalla.
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

                    <table cellpadding="0" cellspacing="0" style="margin:18px 0 14px">
                      <tr>
                        <td>
                          <a href="{link}" style="background:{AzulAbril};color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;display:inline-block;font-size:14px;font-weight:bold">Revisar y aprobar</a>
                        </td>
                      </tr>
                    </table>
                    <p style="font-size:12px;color:#555">
                      El botón abre esta solicitud en <b>Abril One · Gestión GTH · Aprobaciones</b>, donde
                      puedes aprobar todas las vacantes, aprobar solo algunas o rechazarlas. Si aún no
                      has iniciado sesión, hazlo y te llevaremos directo a esta solicitud.
                    </p>
                    <p style="font-size:12px;color:#555">Si el botón no funciona, copia y pega este enlace en tu navegador:<br>
                      <span style="color:{AzulAbril};word-break:break-all">{Esc(link)}</span>
                    </p>
                    <p style="font-size:11px;color:#888;margin-top:16px">Correo automático de Abril One · Gestión GTH · Reclutamiento.</p>
                  </div>
                </div>
                """;
        }

        /// <summary>
        /// Enlace a la solicitud dentro de «Aprobaciones». Si el gerente no tiene sesión, el
        /// <c>authGuard</c> del frontend lo manda al login con esta URL como <c>returnUrl</c> y lo
        /// devuelve acá al entrar.
        /// </summary>
        private string ConstruirLink(int aprobacionId)
        {
            var frontendUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
            return $"{frontendUrl}/gestion-gth/aprobaciones/{aprobacionId}";
        }

        // ── Pantalla «Aprobaciones» y decisión ────────────────────────────────
        public async Task<AprobacionGgBandejaDto> GetBandeja(int? userId) =>
            await _repo.GetBandeja(await _scopes.ResolveAsync(userId));

        public async Task<AprobacionGgDetalleDto> GetDetalle(int aprobacionId, int? userId)
        {
            var scope = await _scopes.ResolveAsync(userId);

            var dto = await _repo.GetDetalle(aprobacionId, scope);
            if (dto == null)
                throw new AbrilException("La solicitud por aprobar no existe o ya no está disponible.", 404);

            // Aviso "a quién le llegará esta decisión" del modal. Son exactamente los destinatarios
            // que usan al confirmar NotificarAGthAsync (tipo SOLICITUD) y NotificarATiAsync (tipo
            // TI_VACANTES) y, como allá, sin área — ninguno de los dos depende del área del
            // solicitante. Va en la misma petición que el detalle.
            //
            // Los dos correos se muestran juntos: al gerente le importa a quién le llega su
            // decisión, no cuántos correos salen por detrás.
            //
            // Solo se consultan cuando quien abre es Gerencia General y aún no decidió: es el único
            // caso en el que esos correos van a salir. Al gerente del área no se le promete un envío
            // que su visto bueno no dispara.
            if (dto.PuedeDecidir && dto.Nivel == AprobacionNivel.GerenteGeneral)
            {
                try
                {
                    dto.Destinatarios = Fusionar(
                        await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.Solicitud),
                        await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.Ti));
                }
                catch (Exception ex)
                {
                    // El aviso es informativo: si no se puede resolver, el modal abre sin él. La
                    // decisión tiene que poder tomarse igual.
                    _logger.LogWarning(ex,
                        "No se pudieron resolver los destinatarios de la decisión para el detalle de la aprobación {AprobacionId}",
                        aprobacionId);
                }
            }

            return dto;
        }

        /// <summary>
        /// Une los destinatarios de los correos que dispara una misma decisión en una sola lista
        /// para el aviso del modal. Un buzón configurado en los dos correos aparece una sola vez, y
        /// si está como principal en uno y en copia en otro gana "Para" — el mismo criterio que usa
        /// el resolver dentro de cada correo.
        /// </summary>
        private static SolicitudDestinatariosDto Fusionar(params SolicitudDestinatariosDto[] fuentes)
        {
            var fusion = new SolicitudDestinatariosDto();
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Los principales primero, para que ganen sobre las copias.
            foreach (var d in fuentes.SelectMany(f => f.Para))
                if (vistos.Add(d.Email)) fusion.Para.Add(d);

            foreach (var d in fuentes.SelectMany(f => f.Copias))
                if (vistos.Add(d.Email)) fusion.Copias.Add(d);

            return fusion;
        }

        public async Task<AprobacionGgDecisionResultDto> RegistrarDecision(
            int aprobacionId, AprobacionGgDecisionDto dto, int? userId)
        {
            if (!userId.HasValue)
                throw new AbrilException("No se pudo identificar al usuario.", 401);
            if (dto?.Decisiones == null || dto.Decisiones.Count == 0)
                throw new AbrilException("Debes aprobar o rechazar las vacantes antes de enviar la decisión.", 400);

            // El nivel sale de la categoría del usuario, nunca del payload: así nadie puede pedir
            // que su firma cuente como la de Gerencia General.
            var scope = await _scopes.ResolveAsync(userId);

            var ctx = await _repo.RegistrarDecision(aprobacionId, dto, userId.Value, scope);
            var res = ctx.Resultado;

            if (res.Nivel == AprobacionNivel.GerenteGeneral)
            {
                // Solo las vacantes aprobadas por el GG salen, y solo si hay alguna.
                // Best-effort los dos: la decisión ya quedó registrada.
                if (ctx.Aprobadas.Count > 0)
                {
                    await NotificarAGthAsync(ctx, userId);
                    await NotificarATiAsync(ctx);
                }

                // El mensaje habla del correo a GTH y no del de TI a propósito: lo que le importa
                // al gerente es si la solicitud continúa. El aviso a TI es interno del proceso.
                res.Message = res.Aprobados == 0
                    ? "Decisión registrada: rechazaste todas las vacantes. La solicitud no continúa y no se envió a Gestión de Talento Humano."
                    : res.Rechazados == 0
                        ? $"Decisión registrada: aprobaste {res.Aprobados} vacante(s). Ya se enviaron a Gestión de Talento Humano para iniciar el reclutamiento."
                        : $"Decisión registrada: aprobaste {res.Aprobados} vacante(s) y rechazaste {res.Rechazados}. Las aprobadas ya se enviaron a Gestión de Talento Humano.";
            }
            else
            {
                // El visto bueno del área no manda nada a GTH: el mensaje lo dice para que el
                // gerente no se quede esperando un correo que no sale.
                res.Message = res.Aprobados == 0
                    ? "Visto bueno registrado: observaste todas las vacantes. Gerencia General verá tu postura al decidir."
                    : res.Rechazados == 0
                        ? $"Visto bueno registrado: aprobaste {res.Aprobados} vacante(s). La solicitud continúa a la espera de la aprobación de Gerencia General."
                        : $"Visto bueno registrado: aprobaste {res.Aprobados} vacante(s) y observaste {res.Rechazados}. Gerencia General verá tu postura al decidir.";
            }

            return res;
        }

        /// <summary>
        /// Correo + campanita a GTH con las vacantes que Gerencia General aprobó (tipo SOLICITUD).
        /// Es el correo de "nueva solicitud de personal" que antes salía al registrar la solicitud:
        /// ahora espera la aprobación del GG y solo lleva lo aprobado. No bloquea.
        /// </summary>
        private async Task NotificarAGthAsync(AprobacionGgDecisionContextoDto ctx, int? userId)
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
                    userId, // quien aprobó desde «Aprobaciones»
                    items);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo crear la notificación in-app de la solicitud de personal {SolicitudId}", ctx.SolicitudId);
            }
        }

        /// <summary>
        /// Correo a TI con las vacantes que Gerencia General aprobó (tipo TI_VACANTES). Sale en la
        /// misma decisión que el de GTH pero es otro correo y otra configuración: TI no participa
        /// del reclutamiento, lo que necesita es la anticipación para alistar equipo, usuario y
        /// accesos de cada ingreso.
        ///
        /// El buzón no está cableado acá: sale del destinatario dinámico TI_AREA, que lee
        /// <c>area_scope.email</c> del área de Tecnología de la Información. No bloquea ni lanza —
        /// la decisión ya quedó registrada y no se revierte porque un correo falle.
        /// </summary>
        private async Task NotificarATiAsync(AprobacionGgDecisionContextoDto ctx)
        {
            try
            {
                var dest = await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.Ti);
                if (dest.Para.Count == 0)
                {
                    // También entra acá cuando el correo está apagado con su interruptor maestro:
                    // en ese caso es una decisión de la Configuración, no una falla.
                    _logger.LogWarning(
                        "No hay destinatarios principales activos para el correo de vacantes aprobadas a TI " +
                        "(solicitud {SolicitudId}); no se envía.",
                        ctx.SolicitudId);
                    return;
                }

                var subject = ctx.Aprobadas.Count == 1
                    ? $"[Reclutamiento] Vacante aprobada — {ctx.Aprobadas[0].Codigo}"
                    : $"[Reclutamiento] {ctx.Aprobadas.Count} vacantes aprobadas — {ctx.Area}";

                await _email.SendAsync(
                    to:      dest.EmailsPara,
                    subject: subject,
                    body:    ConstruirCuerpoTi(ctx),
                    isHtml:  true,
                    cc:      dest.Copias.Count > 0 ? dest.EmailsCopias : null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "No se pudo enviar el correo de vacantes aprobadas a TI de la solicitud {SolicitudId}",
                    ctx.SolicitudId);
            }
        }

        /// <summary>
        /// Cuerpo del correo a TI: las vacantes aprobadas, con la fecha de ingreso requerida como
        /// dato central — es la que le dice a TI para cuándo tiene que estar listo cada puesto. No
        /// lleva justificación, comentario ni las vacantes rechazadas: eso es contexto de la
        /// decisión y del reclutamiento, no del trabajo de TI.
        /// </summary>
        private static string ConstruirCuerpoTi(AprobacionGgDecisionContextoDto ctx)
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

            return $"""
                <div style="font-family:Arial,sans-serif;max-width:680px">
                  <div style="background:{AzulAbril};padding:12px 16px">
                    <h2 style="color:#fff;margin:0;font-size:18px">Vacantes aprobadas — aviso a TI</h2>
                  </div>
                  <div style="padding:16px;border:1px solid #e5e7eb;border-top:none">
                    <p style="font-size:13px;margin-top:0">
                      <b>Gerencia General aprobó</b> las siguientes vacantes y ya pasaron a reclutamiento.
                      Les compartimos el detalle con anticipación para que puedan ir previendo lo que
                      necesitará cada puesto (equipo, usuario, correo y accesos) para su fecha de ingreso.
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
                    <p style="font-size:12px;color:#555">
                      La fecha de ingreso es la solicitada por el área: puede moverse según avance el
                      proceso de selección. Gestión de Talento Humano confirmará la fecha definitiva de
                      cada ingreso.
                    </p>
                    <p style="font-size:11px;color:#888;margin-top:16px">Correo automático de Abril One · Gestión GTH · Reclutamiento.</p>
                  </div>
                </div>
                """;
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

            // El visto bueno del gerente del área no condiciona nada, pero saber que opinó (y qué
            // opinó) le da contexto a GTH. Vacío si nunca llegó a registrarlo.
            var vistoBuenoArea = string.IsNullOrWhiteSpace(ctx.GerenteAreaResumen)
                ? ""
                : $"""<p style="font-size:13px"><b>Visto bueno del gerente del área:</b> {Esc(ctx.GerenteAreaResumen)}</p>""";

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
                    {vistoBuenoArea}
                    {sustento}
                    <p style="font-size:11px;color:#888;margin-top:16px">Correo automático de Abril One · Gestión GTH · Reclutamiento.</p>
                  </div>
                </div>
                """;
        }
    }
}
