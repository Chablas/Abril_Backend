using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Shared;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Email.Configuration;
using Layout = Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Shared.ReclutamientoEmailLayout;
using Textos = Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Shared.ReclutamientoEmailTextos;

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
            // El consentimiento de protección de datos es la condición para tratar todo lo demás:
            // sin él no hay formulario que guardar, así que se corta antes de tocar la base.
            if (respuestas.ConsentimientoDatosPersonales != true)
                throw new AbrilException("Debes autorizar el tratamiento de tus datos personales para enviar el formulario.", 400);

            // Nadie sale de una empresa antes de entrar. El formulario ya lo bloquea en pantalla,
            // pero es un endpoint anónimo: la regla se vuelve a exigir acá.
            if (respuestas.FechaInicio.HasValue && respuestas.FechaTermino.HasValue
                && respuestas.FechaTermino.Value < respuestas.FechaInicio.Value)
                throw new AbrilException(
                    "La fecha de término de la experiencia laboral no puede ser anterior a la fecha de inicio.", 400);

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

        /// <summary>
        /// Aviso a GTH de que un postulante terminó (o corrigió) su formulario. El botón abre el
        /// formulario de ESE postulante, que es donde se aprueba o se rechaza; si por lo que sea no
        /// se pudo resolver el requerimiento, se cae a la indicación de buscarlo en la bandeja en
        /// vez de mandar un enlace roto.
        /// </summary>
        private string ConstruirCuerpoFormularioCompletado(FormularioCompletadoContextoDto ctx)
        {
            var l = Layout.Desde(_configuration);

            var datos = new List<Layout.Fila>
            {
                new("req-codigo", "Requerimiento", Textos.OGuion(ctx.Codigo)),
                new("req-puesto", "Puesto", Textos.OGuion(ctx.Puesto)),
                new("req-area", "Área solicitante", Textos.OGuion(ctx.Area)),
                new("req-proyecto", "Proyecto / Obra", Textos.OGuion(ctx.ProyectoObra)),
                new("req-candidato", "Postulante", Textos.OGuion(ctx.CandidatoNombre)),
            };
            if (!string.IsNullOrWhiteSpace(ctx.CorreoPostulante))
                datos.Add(new("req-correo", "Correo", Layout.Esc(ctx.CorreoPostulante)));
            if (!string.IsNullOrWhiteSpace(ctx.NumeroCelular))
                datos.Add(new("req-celular", "Celular", Layout.Esc(ctx.NumeroCelular)));
            datos.Add(new("req-fecha", "Enviado el", ctx.CompletadoEn.ToString("dd/MM/yyyy HH:mm")));

            var hayEnlace = ctx.RequerimientoId > 0 && ctx.CandidatoId > 0;
            var link = hayEnlace ? ConstruirLinkRevisionFormulario(ctx.RequerimientoId, ctx.CandidatoId) : "";

            var nombre = string.IsNullOrWhiteSpace(ctx.CandidatoNombre) ? "El postulante" : Layout.Esc(ctx.CandidatoNombre);
            var accion = ctx.EsCorreccion ? "corrigió" : "completó";

            return l.Documento(
                new Layout.Cabecera(
                    "req-formulario",
                    ctx.EsCorreccion ? "Formulario Corregido" : "Formulario Completado",
                    $"<b>{nombre}</b> {accion} su formulario de postulante."),
                l.Tarjeta(datos),
                hayEnlace ? l.Boton("Revisar formulario", link) : "",
                hayEnlace
                    ? l.EnlaceDirecto(link)
                    : l.Franja("req-aviso", Layout.Tono.Info,
                        "Revísalo en <b>Gestión GTH → Reclutamiento</b>, con «Ver formulario»."));
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
                var dest = await ResolverDestinatariosFormularioAsync(ctx.EsRechazo);
                await EnviarCorreoFormularioAsync(ctx, dest);
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

                // Destinatarios configurados: se resuelven una sola vez para todo el lote y solo si
                // el lote los usa. Un lote puede mezclar invitaciones y correcciones (cada una es un
                // tipo de correo distinto), así que se guardan por separado.
                SolicitudDestinatariosDto? destEnvio = null, destCorreccion = null;

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
                        if (p.Contexto.EsRechazo)
                            destCorreccion ??= await ResolverDestinatariosFormularioAsync(esRechazo: true);
                        else
                            destEnvio ??= await ResolverDestinatariosFormularioAsync(esRechazo: false);

                        await EnviarCorreoFormularioAsync(
                            p.Contexto, p.Contexto.EsRechazo ? destCorreccion! : destEnvio!);

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
        /// <param name="dest">
        /// Destinatarios configurados del tipo que corresponde (FORMULARIO_ENVIO o
        /// FORMULARIO_CORRECCION). Se recibe ya resuelto y no se resuelve acá adrede: el envío
        /// masivo llama a este método una vez por postulante y resolverlo adentro sería un
        /// roundtrip a la BD por cada correo del lote.
        /// </param>
        private Task EnviarCorreoFormularioAsync(
            EnviarFormularioContextoDto ctx, SolicitudDestinatariosDto dest)
        {
            var link = ConstruirLink(ctx.Token);

            // El principal es SIEMPRE el postulante; la configuración solo suma principales extra
            // y copias.
            var (principales, copias) = CorreoDestinatariosCombinador.Combinar(ctx.Correo, dest);

            return _email.SendAsync(
                to:      principales,
                subject: ctx.EsRechazo
                    ? $"Correcciones en tu formulario de postulante — {ctx.Puesto} · Abril Grupo Inmobiliario"
                    : $"Formulario de postulante — {ctx.Puesto} · Abril Grupo Inmobiliario",
                body:    ctx.EsRechazo
                    ? ConstruirCuerpoRechazo(ctx.CandidatoNombre, ctx.Puesto, ctx.Motivo, link)
                    : ConstruirCuerpoEnvio(ctx.CandidatoNombre, ctx.Puesto, link),
                isHtml:  true,
                cc:      copias.Count > 0 ? copias : null,
                sender:  EmailSenders.Gth);
        }

        /// <summary>
        /// Destinatarios configurados del correo que le toca al contexto: el de correcciones
        /// cuando el formulario viene rechazado y el de invitación en el resto de los casos.
        /// </summary>
        private Task<SolicitudDestinatariosDto> ResolverDestinatariosFormularioAsync(bool esRechazo) =>
            _destinatarios.ResolverAsync(esRechazo
                ? CorreoTipoReclutamiento.FormularioCorreccion
                : CorreoTipoReclutamiento.FormularioEnvio);

        public Task<FormularioRevisionDto> GetRevision(int candidatoId) => _repo.GetRevision(candidatoId);

        public async Task<FormularioAccionResultDto> Decision(int candidatoId, FormularioDecisionDto dto, int? userId)
        {
            if (dto == null)
                throw new AbrilException("No se recibió la decisión del formulario.", 400);

            var ctx = await _repo.RegistrarDecision(candidatoId, dto.Aprobado, dto.Motivo, userId);

            // Aprobar además copia los datos validados a `person` (la data maestra). Si esa ficha
            // quedó incompleta se dice acá mismo, porque de su correo personal depende que Onboarding
            // pueda enviarle la carta oferta.
            if (dto.Aprobado)
                return new FormularioAccionResultDto
                {
                    Message = ctx.PersonAviso == null
                        ? "Formulario aprobado. Los datos validados quedaron registrados en la base maestra."
                        : $"Formulario aprobado. {ctx.PersonAviso}",
                    Formulario = ctx.Resumen,
                };

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
                var dest = await ResolverDestinatariosFormularioAsync(esRechazo: true);
                var (principales, copias) = CorreoDestinatariosCombinador.Combinar(ctx.Correo, dest);

                await _email.SendAsync(
                    to:      principales,
                    subject: $"Correcciones en tu formulario de postulante — {ctx.Puesto} · Abril Grupo Inmobiliario",
                    body:    ConstruirCuerpoRechazo(ctx.CandidatoNombre, ctx.Puesto, ctx.Motivo, ConstruirLink(ctx.Token)),
                    isHtml:  true,
                    cc:      copias.Count > 0 ? copias : null,
                    sender:  EmailSenders.Gth);
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
        /// Correo de rechazo del formulario: qué observó GTH y el mismo enlace del envío original,
        /// que le abre el formulario con sus respuestas precargadas. Lo usan tanto el rechazo en sí
        /// como el reenvío de un formulario que sigue rechazado.
        /// </summary>
        private string ConstruirCuerpoRechazo(string? candidatoNombre, string puesto, string? motivo, string link)
        {
            var l = Layout.Desde(_configuration);
            var nombre = string.IsNullOrWhiteSpace(candidatoNombre) ? "postulante" : candidatoNombre;

            // Las observaciones se escriben en un textarea: los saltos de línea se conservan.
            var observaciones = string.IsNullOrWhiteSpace(motivo)
                ? ""
                : l.Franja("req-observaciones", Layout.Tono.Ambar,
                    $"<b>Observaciones:</b><br />{Layout.EscMultilinea(motivo)}");

            return l.Documento(
                new Layout.Cabecera(
                    "req-correccion", "Formulario por Corregir",
                    $"Estimado(a) {Layout.Esc(nombre)}: hay puntos por corregir en tu formulario "
                    + $"para <b>{Layout.Esc(puesto)}</b>."),
                observaciones,
                l.Parrafo(
                    "El formulario se abre con toda la información que ya registraste: corrige solo lo "
                    + "observado y vuelve a enviarlo."),
                l.Boton("Corregir formulario", link),
                l.EnlaceDirecto(link));
        }

        /// <summary>Correo de invitación: el primer envío del formulario (o el reenvío de uno no rechazado).</summary>
        private string ConstruirCuerpoEnvio(string? candidatoNombre, string puesto, string link)
        {
            var l = Layout.Desde(_configuration);
            var nombre = string.IsNullOrWhiteSpace(candidatoNombre) ? "postulante" : candidatoNombre;

            return l.Documento(
                new Layout.Cabecera(
                    "req-formulario", "Formulario de Postulante",
                    $"Estimado(a) {Layout.Esc(nombre)}: completa tu formulario para la posición "
                    + $"<b>{Layout.Esc(puesto)}</b>."),
                l.Boton("Completar formulario", link),
                l.EnlaceDirecto(link));
        }
    }
}
