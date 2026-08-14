using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
{
    public class PostulanteFormularioService : IPostulanteFormularioService
    {
        private readonly IPostulanteFormularioRepository _repo;
        private readonly ICorreoDestinatariosResolver _destinatarios;
        private readonly IEmailService _email;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PostulanteFormularioService> _logger;

        public PostulanteFormularioService(
            IPostulanteFormularioRepository repo,
            ICorreoDestinatariosResolver destinatarios,
            IEmailService email,
            IConfiguration configuration,
            ILogger<PostulanteFormularioService> logger)
        {
            _repo          = repo;
            _destinatarios = destinatarios;
            _email         = email;
            _configuration = configuration;
            _logger        = logger;
        }

        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public async Task<PostulanteFormularioPublicoDto> GetPublico(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new AbrilException("Enlace del formulario no válido.", 400);

            var dto = await _repo.GetByToken(token.Trim());
            if (dto == null)
                throw new AbrilException("El enlace del formulario no es válido o ya no está disponible.", 404);
            return dto;
        }

        public async Task GuardarPublico(string token, PostulanteFormularioRespuestasDto respuestas)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new AbrilException("Enlace del formulario no válido.", 400);
            if (respuestas == null)
                throw new AbrilException("No se recibieron los datos del formulario.", 400);

            var ctx = await _repo.GuardarRespuestasByToken(token.Trim(), respuestas);

            // Aviso a GTH de que el formulario ya se puede revisar. Best-effort a propósito: el
            // postulante ya envió sus datos y no tiene por qué ver un error (ni reintentar) si el
            // correo interno falla. Sus destinatarios se administran en Reclutamiento → Configuración.
            try
            {
                await EnviarAvisoFormularioCompletadoAsync(ctx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "No se pudo enviar el aviso a GTH del formulario completado del requerimiento {Codigo}",
                    ctx.Codigo);
            }
        }

        /// <summary>
        /// Correo configurable que avisa a GTH que un postulante terminó de llenar su formulario.
        /// Si el correo está apagado o no tiene destinatarios activos, no se envía nada.
        /// </summary>
        private async Task EnviarAvisoFormularioCompletadoAsync(FormularioCompletadoContextoDto ctx)
        {
            var dest = await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.FormularioCompletado);
            var para = dest.EmailsPara;
            if (para.Count == 0) return;

            var copias = dest.EmailsCopias;
            var accion = ctx.EsCorreccion ? "corrigió" : "completó";

            await _email.SendAsync(
                to:      para,
                subject: $"[Reclutamiento] Formulario de postulante {accion} — {ctx.Codigo} · {ctx.Puesto}",
                body:    ConstruirCuerpoFormularioCompletado(ctx),
                isHtml:  true,
                cc:      copias.Count > 0 ? copias : null);
        }

        /// <summary>
        /// Enlace al requerimiento dentro de la bandeja de GTH («Reclutamiento») con el modal
        /// «Ver formulario» de este postulante ya abierto encima, que es donde se aprueba o rechaza.
        /// Mismo mecanismo que el resto de correos del módulo: sin sesión, el <c>authGuard</c> del
        /// frontend manda al login con esta URL como <c>returnUrl</c> y lo devuelve acá al entrar.
        /// </summary>
        private string ConstruirLinkRevisionFormulario(int requerimientoId, int candidatoId)
        {
            var frontendUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
            return $"{frontendUrl}/gestion-gth/reclutamiento/requerimiento/{requerimientoId}?formulario={candidatoId}";
        }

        private string ConstruirCuerpoFormularioCompletado(FormularioCompletadoContextoDto ctx)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            static string Fila(string etiqueta, string? valor) =>
                string.IsNullOrWhiteSpace(valor)
                    ? string.Empty
                    : $"""
                        <tr>
                          <td style="padding:6px 10px;font-size:12px;color:#6b7280;white-space:nowrap">{Esc(etiqueta)}</td>
                          <td style="padding:6px 10px;font-size:13px;color:#1f2937"><b>{Esc(valor)}</b></td>
                        </tr>
                        """;

            var titulo = ctx.EsCorreccion
                ? "Un postulante corrigió su formulario"
                : "Un postulante completó su formulario";

            var intro = ctx.EsCorreccion
                ? "envió las correcciones de su formulario de información del postulante."
                : "terminó de llenar su formulario de información del postulante.";

            // El botón abre el formulario de ESTE postulante, así que se nombra por la acción que a
            // GTH le toca hacer allí. Si por lo que sea no se pudo resolver el requerimiento, se cae
            // a la indicación de siempre (buscarlo en la bandeja) en vez de mandar un enlace roto.
            var hayEnlace = ctx.RequerimientoId > 0 && ctx.CandidatoId > 0;
            var link = hayEnlace ? ConstruirLinkRevisionFormulario(ctx.RequerimientoId, ctx.CandidatoId) : string.Empty;

            var cierre = hayEnlace
                ? $"""
                    <table cellpadding="0" cellspacing="0" style="margin:18px 0 14px">
                      <tr>
                        <td>
                          <a href="{link}" style="background:#005D9D;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;display:inline-block;font-size:14px;font-weight:bold">Revisar formulario</a>
                        </td>
                      </tr>
                    </table>
                    <p style="font-size:12px;color:#555">
                      El botón abre este requerimiento en <b>Abril One · Gestión GTH · Reclutamiento</b>
                      con el formulario del postulante ya abierto, para que lo revises y lo apruebes o
                      lo rechaces. Si aún no has iniciado sesión, hazlo y te llevaremos directo a él.
                    </p>
                    <p style="font-size:12px;color:#555">Si el botón no funciona, copia y pega este enlace en tu navegador:<br>
                      <span style="color:#005D9D;word-break:break-all">{Esc(link)}</span>
                    </p>
                    """
                : """
                    <p style="font-size:12.5px;color:#555;margin-top:16px">
                      Ya lo puedes revisar en <b>Gestión GTH → Reclutamiento</b>, en el detalle del
                      requerimiento, con «Ver formulario», para aprobarlo o rechazarlo.
                    </p>
                    """;

            return $"""
                <div style="font-family:Arial,sans-serif;max-width:640px">
                  <div style="background:#005D9D;padding:14px 18px">
                    <h2 style="color:#fff;margin:0;font-size:18px">{titulo}</h2>
                  </div>
                  <div style="padding:18px;border:1px solid #e5e7eb;border-top:none">
                    <p style="font-size:13px;margin-top:0">
                      <b>{Esc(ctx.CandidatoNombre)}</b> {intro}
                    </p>
                    <table style="border-collapse:collapse;margin:14px 0;background:#f9fafb;border:1px solid #e5e7eb;border-radius:8px">
                      {Fila("Requerimiento", ctx.Codigo)}
                      {Fila("Puesto", ctx.Puesto)}
                      {Fila("Área", ctx.Area)}
                      {Fila("Proyecto / obra", ctx.ProyectoObra)}
                      {Fila("Postulante", ctx.CandidatoNombre)}
                      {Fila("Correo", ctx.CorreoPostulante)}
                      {Fila("Celular", ctx.NumeroCelular)}
                      {Fila("Enviado el", ctx.CompletadoEn.ToString("dd/MM/yyyy HH:mm"))}
                    </table>
                    {cierre}
                    <p style="font-size:11px;color:#888;margin-top:18px">Correo automático de Abril One · Gestión GTH · Reclutamiento.</p>
                  </div>
                </div>
                """;
        }

        public async Task<FormularioAccionResultDto> Enviar(int candidatoId, EnviarFormularioDto dto, int? userId)
        {
            var correo = dto?.Correo?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(correo) || !EmailRegex.IsMatch(correo))
                throw new AbrilException("Ingresa un correo electrónico válido para enviar el formulario.", 400);

            // Token de acceso público (hex, url-safe). Se usa solo si el formulario aún no existía.
            var nuevoToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

            var ctx = await _repo.PrepararEnvio(candidatoId, correo, nuevoToken, userId);

            // Enviar el correo con el enlace del formulario. Bloqueante: si falla, se informa (el
            // formulario ya quedó registrado y GTH puede reintentar el envío).
            try
            {
                await EnviarCorreoFormularioAsync(ctx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló el correo del formulario del postulante (candidato {CandidatoId})", candidatoId);
                throw new AbrilException(
                    "El formulario quedó registrado, pero no se pudo enviar el correo al postulante. Reintenta el envío.", 502);
            }

            return new FormularioAccionResultDto
            {
                Message = ctx.EsRechazo
                    ? $"Observaciones reenviadas a {ctx.Correo}."
                    : $"Formulario enviado a {ctx.Correo}.",
                Formulario = ctx.Resumen,
            };
        }

        public async Task<FormularioEnvioMasivoResultDto> EnviarMasivo(EnviarFormularioMasivoDto dto, int? userId)
        {
            var items = dto?.Candidatos ?? new List<EnviarFormularioMasivoItemDto>();
            if (items.Count == 0)
                throw new AbrilException("Selecciona al menos un candidato para enviarle el formulario.", 400);

            // Un candidato con el correo mal escrito no cancela el lote: se reporta como fallido y el
            // resto se envía igual. Es la diferencia con el envío individual, donde el 400 es la única
            // respuesta posible porque no hay nadie más a quien enviarle.
            var orden      = new List<int>();
            var resultados = new Dictionary<int, FormularioEnvioMasivoResultadoDto>();
            var solicitudes = new List<EnvioMasivoSolicitudDto>();

            foreach (var item in items)
            {
                // El mismo candidato repetido en el lote sería un doble envío: se queda el primero.
                if (resultados.ContainsKey(item.CandidatoId)) continue;
                orden.Add(item.CandidatoId);

                var correo = item.Correo?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(correo) || !EmailRegex.IsMatch(correo))
                {
                    resultados[item.CandidatoId] = new FormularioEnvioMasivoResultadoDto
                    {
                        CandidatoId = item.CandidatoId,
                        Enviado     = false,
                        Error       = "El correo del postulante no es válido.",
                    };
                    continue;
                }

                // Marca de posición: se reemplaza con el resultado real tras preparar el envío.
                resultados[item.CandidatoId] = new FormularioEnvioMasivoResultadoDto
                {
                    CandidatoId = item.CandidatoId,
                    Enviado     = false,
                    Error       = "No se pudo preparar el envío.",
                };

                solicitudes.Add(new EnvioMasivoSolicitudDto
                {
                    CandidatoId = item.CandidatoId,
                    Correo      = correo,
                    // Token de acceso público (hex, url-safe). Se usa solo si el formulario aún no existía.
                    NuevoToken  = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(),
                });
            }

            if (solicitudes.Count > 0)
            {
                var preparados = await _repo.PrepararEnvioMasivo(solicitudes, userId);

                // Los correos se envían uno por uno a propósito: el proveedor de correo es externo
                // (SendGrid / PowerAutomate / SMTP) y dispararlos en paralelo arriesga throttling por un
                // ahorro de segundos sobre una long list que suele ser de pocos candidatos.
                foreach (var p in preparados)
                {
                    if (p.Contexto == null)
                    {
                        resultados[p.CandidatoId] = new FormularioEnvioMasivoResultadoDto
                        {
                            CandidatoId = p.CandidatoId,
                            Enviado     = false,
                            Error       = p.Error,
                        };
                        continue;
                    }

                    try
                    {
                        await EnviarCorreoFormularioAsync(p.Contexto);
                        resultados[p.CandidatoId] = new FormularioEnvioMasivoResultadoDto
                        {
                            CandidatoId = p.CandidatoId,
                            Enviado     = true,
                            Formulario  = p.Contexto.Resumen,
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Falló el correo del formulario del postulante en el envío masivo (candidato {CandidatoId})",
                            p.CandidatoId);

                        // El formulario ya quedó registrado como enviado, así que se devuelve su resumen
                        // igual: la bandeja debe mostrar el estado real de la base de datos, con el aviso
                        // de que a ese postulante hay que reintentarle el correo.
                        resultados[p.CandidatoId] = new FormularioEnvioMasivoResultadoDto
                        {
                            CandidatoId = p.CandidatoId,
                            Enviado     = false,
                            Error       = "No se pudo enviar el correo al postulante. Reintenta el envío.",
                            Formulario  = p.Contexto.Resumen,
                        };
                    }
                }
            }

            var lista     = orden.Select(id => resultados[id]).ToList();
            var enviados  = lista.Count(r => r.Enviado);
            var fallidos  = lista.Count - enviados;

            return new FormularioEnvioMasivoResultDto
            {
                Enviados   = enviados,
                Fallidos   = fallidos,
                Resultados = lista,
                Message = fallidos == 0
                    ? $"Formulario enviado a {enviados} postulante(s)."
                    : enviados == 0
                        ? "No se pudo enviar el formulario a ningún postulante."
                        : $"Se envió el formulario a {enviados} de {lista.Count} postulantes.",
            };
        }

        /// <summary>
        /// Envía el correo del formulario para un contexto ya preparado. Reenviar un formulario
        /// rechazado repite el correo de correcciones —con las observaciones—, no el de invitación:
        /// para el postulante el rechazo sigue vigente. Lo comparten el envío individual y el masivo.
        /// </summary>
        private Task EnviarCorreoFormularioAsync(EnviarFormularioContextoDto ctx)
        {
            var link = ConstruirLink(ctx.Token);

            return _email.SendAsync(
                to:      new List<string> { ctx.Correo },
                subject: ctx.EsRechazo
                    ? $"Correcciones en tu formulario de postulante — {ctx.Puesto} · Abril Grupo Inmobiliario"
                    : $"Formulario de postulante — {ctx.Puesto} · Abril Grupo Inmobiliario",
                body:    ctx.EsRechazo
                    ? ConstruirCuerpoRechazo(ctx.CandidatoNombre, ctx.Puesto, ctx.Motivo, link)
                    : ConstruirCuerpoEnvio(ctx.CandidatoNombre, ctx.Puesto, link),
                isHtml:  true);
        }

        public Task<FormularioRevisionDto> GetRevision(int candidatoId) => _repo.GetRevision(candidatoId);

        public async Task<FormularioAccionResultDto> Decision(int candidatoId, FormularioDecisionDto dto, int? userId)
        {
            if (dto == null)
                throw new AbrilException("No se recibió la decisión del formulario.", 400);

            var ctx = await _repo.RegistrarDecision(candidatoId, dto.Aprobado, dto.Motivo, userId);

            if (dto.Aprobado)
                return new FormularioAccionResultDto { Message = "Formulario aprobado.", Formulario = ctx.Resumen };

            // Rechazo de un formulario que el postulante nunca llegó a llenar: es una decisión
            // interna para que el proceso siga sin él, así que no se le escribe nada. Su enlace
            // queda vigente por si lo completa más adelante.
            if (!ctx.AvisarAlPostulante)
                return new FormularioAccionResultDto
                {
                    Message    = "Formulario rechazado. Su enlace sigue vigente por si el postulante lo completa después.",
                    Formulario = ctx.Resumen,
                };

            // Rechazo: se le avisa al postulante con el MISMO enlace del envío original, así que el
            // formulario se le abre con todo lo que ya llenó y solo tiene que corregir lo observado.
            // Mismo criterio que el resto de correos del módulo: la decisión ya quedó registrada, así
            // que un fallo del correo se informa en el mensaje en vez de tumbar la operación.
            var message = $"Formulario rechazado. Se envió el detalle a {ctx.Correo} para que el postulante lo corrija.";
            try
            {
                await _email.SendAsync(
                    to:      new List<string> { ctx.Correo },
                    subject: $"Correcciones en tu formulario de postulante — {ctx.Puesto} · Abril Grupo Inmobiliario",
                    body:    ConstruirCuerpoRechazo(ctx.CandidatoNombre, ctx.Puesto, ctx.Motivo, ConstruirLink(ctx.Token)),
                    isHtml:  true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló el correo de rechazo del formulario del postulante (candidato {CandidatoId})", candidatoId);
                message = "El formulario quedó rechazado, pero no se pudo enviar el correo al postulante. " +
                          "Vuelve a enviarle el formulario desde la bandeja para que lo corrija.";
            }

            return new FormularioAccionResultDto { Message = message, Formulario = ctx.Resumen };
        }

        /// <summary>Enlace público del formulario para el token indicado (el que va en los correos).</summary>
        private string ConstruirLink(string token)
        {
            var frontendUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
            return $"{frontendUrl}/postulante/formulario?token={Uri.EscapeDataString(token)}";
        }

        /// <summary>
        /// Correo de rechazo del formulario: le indica al postulante qué observó GTH y lo invita a
        /// corregirlo con el mismo enlace, que le abre el formulario con sus respuestas precargadas.
        /// Lo usan tanto el rechazo en sí como el reenvío de un formulario que sigue rechazado.
        /// </summary>
        private static string ConstruirCuerpoRechazo(string? candidatoNombre, string puesto, string? motivo, string link)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            var nombre = string.IsNullOrWhiteSpace(candidatoNombre) ? "postulante" : Esc(candidatoNombre);

            // Las observaciones se escriben en un textarea: los saltos de línea se conservan.
            var observaciones = string.IsNullOrWhiteSpace(motivo)
                ? string.Empty
                : $"""
                    <div style="margin:16px 0;border-left:4px solid #B91C1C;background:#FEF2F2;padding:12px 14px;border-radius:0 8px 8px 0">
                      <p style="font-size:12px;font-weight:bold;color:#B91C1C;margin:0 0 4px;text-transform:uppercase;letter-spacing:.4px">Observaciones</p>
                      <p style="font-size:13px;color:#7f1d1d;margin:0;line-height:1.5">{Esc(motivo).Replace("\n", "<br>")}</p>
                    </div>
                    """;

            return $"""
                <div style="font-family:Arial,sans-serif;max-width:640px">
                  <div style="background:#005D9D;padding:14px 18px">
                    <h2 style="color:#fff;margin:0;font-size:18px">Tu formulario necesita correcciones</h2>
                  </div>
                  <div style="padding:18px;border:1px solid #e5e7eb;border-top:none">
                    <p style="font-size:13px;margin-top:0">Estimado(a) {nombre},</p>
                    <p style="font-size:13px">
                      Gracias por completar el formulario de postulante de <b>Abril Grupo Inmobiliario</b>
                      para la posición <b>{Esc(puesto)}</b>. Al revisarlo encontramos algunos puntos que
                      necesitamos que corrijas o completes antes de continuar con el proceso.
                    </p>
                    {observaciones}
                    <p style="font-size:13px">
                      Ingresa al siguiente enlace para actualizarlo. <b>No tienes que llenarlo desde cero</b>:
                      el formulario se abre con toda la información que ya registraste y las observaciones se
                      muestran en cada página, para que solo corrijas lo indicado y lo vuelvas a enviar.
                    </p>
                    <p style="margin:18px 0">
                      <a href="{link}" style="background:#005D9D;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;display:inline-block;font-size:14px">Corregir formulario</a>
                    </p>
                    <p style="font-size:12px;color:#555">Si el botón no funciona, copia y pega este enlace en tu navegador:<br>
                      <span style="color:#005D9D;word-break:break-all">{Esc(link)}</span>
                    </p>
                    <p style="font-size:11px;color:#888;margin-top:18px">Correo automático de Abril One · Gestión GTH · Reclutamiento.</p>
                  </div>
                </div>
                """;
        }

        /// <summary>Correo de invitación: el primer envío del formulario (o el reenvío de uno no rechazado).</summary>
        private static string ConstruirCuerpoEnvio(string? candidatoNombre, string puesto, string link)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            var nombre = string.IsNullOrWhiteSpace(candidatoNombre) ? "postulante" : Esc(candidatoNombre);

            return $"""
                <div style="font-family:Arial,sans-serif;max-width:640px">
                  <div style="background:#005D9D;padding:14px 18px">
                    <h2 style="color:#fff;margin:0;font-size:18px">Formulario del postulante</h2>
                  </div>
                  <div style="padding:18px;border:1px solid #e5e7eb;border-top:none">
                    <p style="font-size:13px;margin-top:0">Estimado(a) {nombre},</p>
                    <p style="font-size:13px">
                      Gracias por participar del proceso de selección en <b>Abril Grupo Inmobiliario</b>
                      para la posición <b>{Esc(puesto)}</b>. Para continuar, completa el formulario de
                      información del postulante en el siguiente enlace:
                    </p>
                    <p style="margin:18px 0">
                      <a href="{link}" style="background:#005D9D;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;display:inline-block;font-size:14px">Completar formulario</a>
                    </p>
                    <p style="font-size:12px;color:#555">Si el botón no funciona, copia y pega este enlace en tu navegador:<br>
                      <span style="color:#005D9D;word-break:break-all">{Esc(link)}</span>
                    </p>
                    <p style="font-size:11px;color:#888;margin-top:18px">Correo automático de Abril One · Gestión GTH · Reclutamiento.</p>
                  </div>
                </div>
                """;
        }
    }
}
