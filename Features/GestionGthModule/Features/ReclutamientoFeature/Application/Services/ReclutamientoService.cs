using System.Text.RegularExpressions;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Shared;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Email.Configuration;
using Abril_Backend.Shared.Services.SharePoint.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Layout = Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Shared.ReclutamientoEmailLayout;
using Textos = Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Shared.ReclutamientoEmailTextos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
{
    public class ReclutamientoService : IReclutamientoService
    {
        private readonly IReclutamientoRepository _repo;
        private readonly IAprobacionGgRepository  _aprobacionGgRepo;
        private readonly IAprobacionGgService     _aprobacionGg;
        private readonly ICorreoDestinatariosResolver _destinatarios;
        private readonly IGraphSharePointService  _sharePoint;
        private readonly IEmailService            _email;
        private readonly IConfiguration           _configuration;
        private readonly ILogger<ReclutamientoService> _logger;

        private const long MaxSustentoBytes = 10 * 1024 * 1024; // 10 MB
        private static readonly string[] AllowedSustentoExt = { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };

        /// <summary>
        /// Tope de la justificación general. La columna <c>gth_solicitud.justificacion</c> es text
        /// (sin límite), pero el texto va completo en el cuerpo de los correos a los gerentes y a
        /// GTH: el corte evita que un pegado accidental se convierta en un correo ilegible.
        /// </summary>
        private const int MaxJustificacionLength = 4000;

        /// <summary>
        /// Tope del salario bruto mensual declarado por vacante. Es el que aguanta la columna
        /// (<c>numeric(12,2)</c>) sin desbordar y, sobre todo, ataja el dedazo típico de escribir
        /// el sueldo con los céntimos pegados (3500 00 → 350000).
        /// </summary>
        private const decimal MaxSalarioBrutoMensual = 1_000_000m;

        public ReclutamientoService(
            IReclutamientoRepository repo,
            IAprobacionGgRepository aprobacionGgRepo,
            IAprobacionGgService aprobacionGg,
            ICorreoDestinatariosResolver destinatarios,
            IGraphSharePointService sharePoint,
            IEmailService email,
            IConfiguration configuration,
            ILogger<ReclutamientoService> logger)
        {
            _repo             = repo;
            _aprobacionGgRepo = aprobacionGgRepo;
            _aprobacionGg     = aprobacionGg;
            _destinatarios    = destinatarios;
            _sharePoint       = sharePoint;
            _email            = email;
            _configuration    = configuration;
            _logger           = logger;
        }

        public async Task<ReclutamientoFormDataDto> GetFormData(int? userId)
        {
            var dto = await _repo.GetFormData(userId);

            // Aviso "a quién le llegará esta solicitud" del modal. Va en la misma petición que los
            // catálogos (una sola llamada al abrir el formulario) y sale del mismo resolver que usa
            // el envío, así que lo que se muestra es exactamente lo que se va a enviar.
            dto.Destinatarios = await _destinatarios.ResolverAsync(
                CorreoTipoReclutamiento.AprobacionGg, dto.AreaScopeId);

            return dto;
        }

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

        public async Task<LongListDecisionResultDto> RegistrarDecisionLongList(
            int requerimientoId, LongListDecisionDto dto, int? userId)
        {
            if (!userId.HasValue)
                throw new AbrilException("No se pudo identificar al usuario.", 401);
            if (dto?.Decisiones == null || dto.Decisiones.Count == 0)
                throw new AbrilException("Debes aprobar o rechazar a los candidatos antes de enviar la decisión.", 400);

            // 1) Persistir la decisión y avanzar el requerimiento (LONG_LIST_APROBADA o vuelta a LONG_LIST).
            var ctx = await _repo.RegistrarDecisionLongList(requerimientoId, dto.Decisiones, userId.Value);

            // 2) Notificar a GTH por correo (tipo LONG_LIST_DECISION). Best-effort: la decisión ya quedó
            //    registrada; si el correo falla solo se registra el warning (no se revierte el estado).
            await NotificarDecisionAGthAsync(requerimientoId, ctx);

            return ctx.Resultado;
        }

        /// <summary>
        /// Envía a GTH el correo con la decisión del solicitante sobre la long list (tipo
        /// LONG_LIST_DECISION). To = principales configurados, CC = copias. Sin principales no se
        /// envía. No bloquea: cualquier fallo solo se registra como warning.
        /// </summary>
        private async Task NotificarDecisionAGthAsync(int requerimientoId, LongListDecisionContextoDto ctx)
        {
            try
            {
                var dest = await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.LongListDecision);
                if (dest.Para.Count == 0)
                {
                    _logger.LogWarning(
                        "No hay destinatarios principales activos para el correo de decisión de long list ({Codigo}); no se envía.",
                        ctx.Codigo);
                    return;
                }

                var subject = $"[Reclutamiento] Decisión de long list — {ctx.Codigo} · {ctx.Puesto}";

                await _email.SendAsync(
                    to:      dest.EmailsPara,
                    subject: subject,
                    body:    ConstruirCuerpoDecision(requerimientoId, ctx),
                    isHtml:  true,
                    cc:      dest.Copias.Count > 0 ? dest.EmailsCopias : null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar el correo de decisión de long list del requerimiento {Codigo}", ctx.Codigo);
            }
        }

        /// <summary>
        /// Enlace al requerimiento dentro de la bandeja de GTH («Reclutamiento»). Abre el modal de
        /// detalle, que ya se acomoda solo a la fase en la que quedó el requerimiento tras la
        /// decisión: con candidatos aprobados muestra el envío del formulario del postulante, y si
        /// el solicitante rechazó a todos, la carga de una nueva long list. Sin sesión, el
        /// <c>authGuard</c> del frontend manda al login con esta URL como <c>returnUrl</c>.
        /// </summary>
        private string ConstruirLinkDetalleRequerimiento(int requerimientoId)
        {
            var frontendUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
            return $"{frontendUrl}/gestion-gth/reclutamiento/requerimiento/{requerimientoId}";
        }

        /// <summary>
        /// Cuerpo del correo a GTH con la decisión del solicitante sobre la long list. El botón
        /// lleva siempre al detalle del requerimiento, pero se nombra por la acción que a GTH le
        /// toca hacer allí, que depende del resultado.
        /// </summary>
        private string ConstruirCuerpoDecision(int requerimientoId, LongListDecisionContextoDto ctx)
        {
            var l    = Layout.Desde(_configuration);
            var link = ConstruirLinkDetalleRequerimiento(requerimientoId);
            var r    = ctx.Resultado;

            var datos = new List<Layout.Fila>
            {
                new("req-codigo", "Requerimiento", Layout.Esc(ctx.Codigo)),
                new("req-puesto", "Puesto", Textos.OGuion(ctx.Puesto)),
                new("req-area", "Área solicitante", Textos.OGuion(ctx.Area)),
                new("req-proyecto", "Proyecto / Obra", Textos.OGuion(ctx.ProyectoObra)),
            };
            if (!string.IsNullOrWhiteSpace(ctx.SolicitanteNombre))
                datos.Add(new("req-solicitante", "Solicitante", Layout.Esc(ctx.SolicitanteNombre)));

            var filas = new List<IReadOnlyList<Layout.Celda>>(ctx.Candidatos.Count);
            for (var i = 0; i < ctx.Candidatos.Count; i++)
            {
                var c = ctx.Candidatos[i];
                filas.Add(new List<Layout.Celda>
                {
                    new((i + 1).ToString()),
                    new(string.IsNullOrWhiteSpace(c.Nombre) ? $"Candidato {i + 1}" : Layout.Esc(c.Nombre), Negrita: true),
                    new(Textos.OGuion(c.Puesto)),
                    new(c.Aprobado ? "Aprobado" : "Rechazado", Negrita: true,
                        Color: c.Aprobado ? Layout.VerdeOk : Layout.RojoNo),
                });
            }

            return l.Documento(
                new Layout.Cabecera(
                    "req-decision", "Decisión de Long List", "Decisión del solicitante sobre la long list:"),
                l.Tarjeta(datos),
                r.TodosRechazados
                    ? l.Franja("req-rechazadas", Layout.Tono.Rojo,
                        "<b>Rechazó a todos los candidatos.</b> El requerimiento vuelve a la etapa de long list.")
                    : l.Franja("req-check", Layout.Tono.Verde,
                        $"<b>Aprobó {r.Aprobados} candidato(s)</b> de {ctx.Candidatos.Count}."),
                l.Seccion("req-candidatos", $"Candidatos revisados ({ctx.Candidatos.Count})"),
                l.Tabla(ColumnasDecisionCandidatos, filas),
                l.Boton(r.TodosRechazados ? "Preparar nueva long list" : "Continuar el proceso", link),
                l.EnlaceDirecto(link));
        }

        /// <summary>Columnas de la tabla de candidatos decididos (suman los 580px de la tarjeta).</summary>
        private static readonly IReadOnlyList<Layout.Columna> ColumnasDecisionCandidatos = new List<Layout.Columna>
        {
            new("#", 46, Layout.Alineacion.Centro),
            new("Candidato", 200),
            new("Puesto", 190),
            new("Decisión", 144, Layout.Alineacion.Centro),
        };

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

        // ── Multitest y programación de entrevistas ───────────────────────────
        public Task SetMultitest(int candidatoId, MultitestUpdateDto dto, int? userId) =>
            _repo.SetMultitest(candidatoId, dto?.Realizado ?? false, userId);

        public Task<EstadoRequerimientoResultDto> ContinuarAEntrevistas(int requerimientoId, int? userId) =>
            _repo.ContinuarAEntrevistas(requerimientoId, userId);

        public async Task<EntrevistaAccionResultDto> GuardarEntrevista(
            int candidatoId, EntrevistaGuardarDto dto, int? userId)
        {
            if (dto == null || dto.Fecha == default)
                throw new AbrilException("Selecciona la fecha de la entrevista.", 400);
            if (!TimeOnly.TryParseExact(dto.Hora ?? "", "HH:mm", out var hora))
                throw new AbrilException("Selecciona la hora de la entrevista.", 400);
            if (dto.LugarId <= 0)
                throw new AbrilException("Selecciona el lugar de la entrevista.", 400);

            var ctx = await _repo.GuardarEntrevista(candidatoId, dto.Fecha, hora, dto.LugarId, userId);

            // Destinatarios: el principal (Para) es SIEMPRE el postulante citado; la configuración
            // del correo de ENTREVISTA solo aporta principales adicionales y copias.
            var dest = await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.Entrevista);
            var (principales, copias) = CorreoDestinatariosCombinador.Combinar(ctx.Resumen.CorreoEnvio, dest);

            // El correo es best-effort: la entrevista ya quedó programada, así que un fallo del
            // envío se informa en el mensaje en vez de tumbar la operación.
            var message = $"Invitación enviada a {ctx.Resumen.CorreoEnvio}.";
            try
            {
                await _email.SendAsync(
                    to:      principales,
                    subject: $"Entrevista — {ctx.Puesto} · Abril Grupo Inmobiliario",
                    body:    ConstruirCuerpoEntrevista(ctx),
                    isHtml:  true,
                    cc:      copias.Count > 0 ? copias : null,
                    sender:  EmailSenders.Gth);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar la invitación a la entrevista del candidato {CandidatoId}", candidatoId);
                message = "La entrevista quedó programada, pero no se pudo enviar la invitación por correo. Vuelve a intentarlo.";
            }

            return new EntrevistaAccionResultDto { Message = message, Entrevista = ctx.Resumen };
        }

        /// <summary>
        /// Citación a entrevista para el postulante: la fecha, la hora y el lugar, y nada más. Las
        /// dos indicaciones que sí tiene que accionar (llegar antes, avisar si no puede) van en una
        /// sola línea; el resto del texto que había acá era relleno.
        /// </summary>
        private string ConstruirCuerpoEntrevista(EntrevistaEnvioContextoDto ctx)
        {
            var l = Layout.Desde(_configuration);
            var nombre = string.IsNullOrWhiteSpace(ctx.CandidatoNombre) ? "postulante" : ctx.CandidatoNombre;

            return l.Documento(
                new Layout.Cabecera(
                    "req-entrevista", "Invitación a Entrevista",
                    $"Estimado(a) {Layout.Esc(nombre)}: te esperamos para la posición <b>{Layout.Esc(ctx.Puesto)}</b>.",
                    ctx.Codigo),
                l.Tarjeta(new List<Layout.Fila>
                {
                    new("req-fecha", "Fecha", ctx.Resumen.Fecha.ToString("dd/MM/yyyy")),
                    new("req-hora", "Hora", Layout.Esc(ctx.Resumen.Hora)),
                    new("req-lugar", "Lugar", Textos.OGuion(ctx.Resumen.LugarNombre)),
                }),
                l.Franja("req-aviso", Layout.Tono.Info,
                    "Llega 10 minutos antes con tu documento de identidad. Si no puedes asistir, responde este correo."));
        }

        // ── Evaluación de la entrevista y no continuidad ──────────────────────
        public async Task<EvaluacionAccionResultDto> GuardarEvaluacion(
            int candidatoId, EvaluacionGuardarDto dto, int? userId)
        {
            if (dto == null)
                throw new AbrilException("Datos de la evaluación no recibidos.", 400);

            // Los tres comentarios son obligatorios: guardar el informe es enviarlo como finalista,
            // y el área solicitante decide con ese informe completo.
            if (string.IsNullOrWhiteSpace(dto.ComentarioEntrevista) ||
                string.IsNullOrWhiteSpace(dto.ComentarioPsicotecnico) ||
                string.IsNullOrWhiteSpace(dto.ComentarioRecomendacion))
                throw new AbrilException(
                    "El resultado de entrevista, el informe psicotécnico y la recomendación GTH son obligatorios.", 400);

            var guardada = await _repo.GuardarEvaluacion(candidatoId, dto, userId);
            return new EvaluacionAccionResultDto
            {
                Message      = "Evaluación guardada.",
                Evaluacion   = guardada.Evaluacion,
                EstadoCodigo = guardada.EstadoCodigo,
                EstadoNombre = guardada.EstadoNombre,
            };
        }

        public async Task<EvaluacionAccionResultDto> EnviarAgradecimiento(int candidatoId, int? userId)
        {
            var ctx = await _repo.RegistrarAgradecimiento(candidatoId, userId);

            // Mismo criterio que la invitación a la entrevista: el resultado ya quedó registrado,
            // así que un fallo del correo se informa en el mensaje (GTH puede reintentar) en vez
            // de tumbar la operación.
            var message = $"Correo de agradecimiento enviado a {ctx.Correo}. El candidato ya no continúa en el proceso.";
            try
            {
                await EnviarAgradecimientoAsync(ctx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el correo de agradecimiento del candidato {CandidatoId}", candidatoId);
                message = "El candidato quedó registrado como no continúa, pero no se pudo enviar el correo de agradecimiento. Vuelve a intentarlo.";
            }

            return new EvaluacionAccionResultDto { Message = message, Evaluacion = ctx.Resumen };
        }

        /// <summary>
        /// Envía el correo de agradecimiento (tipo AGRADECIMIENTO). El destinatario principal es
        /// SIEMPRE el candidato; la configuración de Reclutamiento solo aporta principales extra y
        /// copias, por si GTH quiere quedarse con el registro de cada cierre.
        ///
        /// Lo comparten los dos lados desde los que sale el mismo correo: cuando GTH marca a un
        /// candidato como "no continúa" y cuando el solicitante rechaza a un finalista. No atrapa
        /// excepciones a propósito — cada llamador ya decide qué hacer si el envío falla.
        /// </summary>
        private async Task EnviarAgradecimientoAsync(AgradecimientoEnvioContextoDto ctx)
        {
            var dest = await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.Agradecimiento);
            var (principales, copias) = CorreoDestinatariosCombinador.Combinar(ctx.Correo, dest);

            await _email.SendAsync(
                to:      principales,
                subject: $"Proceso de selección — {ctx.Puesto} · Abril Grupo Inmobiliario",
                body:    ConstruirCuerpoAgradecimiento(ctx),
                isHtml:  true,
                cc:      copias.Count > 0 ? copias : null,
                sender:  EmailSenders.Gth);
        }

        /// <summary>
        /// Agradecimiento para el candidato que no continúa. No menciona motivos: agradece la
        /// participación y deja abierta la puerta a futuros procesos.
        ///
        /// Es el único correo del módulo que sigue siendo texto corrido, y a propósito: una carta
        /// de no continuidad resuelta con una tabla de datos se lee como un rechazo automático. Se
        /// acortó de cuatro párrafos largos a tres cortos, pero no se vacía más.
        /// </summary>
        private string ConstruirCuerpoAgradecimiento(AgradecimientoEnvioContextoDto ctx)
        {
            var l = Layout.Desde(_configuration);
            var nombre = string.IsNullOrWhiteSpace(ctx.CandidatoNombre) ? "postulante" : ctx.CandidatoNombre;

            return l.Documento(
                new Layout.Cabecera("req-gracias", "Gracias por Participar", PieExtra: ctx.Codigo),
                l.Parrafo($"Estimado(a) {Layout.Esc(nombre)}:"),
                l.Parrafo(
                    "Agradecemos el tiempo que dedicaste al proceso de selección de <b>Abril Grupo "
                    + $"Inmobiliario</b> para la posición <b>{Layout.Esc(ctx.Puesto)}</b>. Decidimos continuar "
                    + "con otros candidatos cuyo perfil se ajusta más a lo que la posición requiere en este "
                    + "momento; esta decisión no desmerece tu experiencia ni tus capacidades."),
                l.Parrafo(
                    "Conservaremos tus datos en nuestra base de postulantes para considerarte en futuras "
                    + "convocatorias que se ajusten a tu perfil. Te deseamos mucho éxito."),
                l.Parrafo("<b>Gestión de Talento Humano</b><br />Abril Grupo Inmobiliario"));
        }

        public async Task<RevisionFinalistasDto> GetRevisionFinalistas(int requerimientoId, int? userId)
        {
            if (!userId.HasValue)
                throw new AbrilException("No se pudo identificar al usuario.", 401);

            var revision = await _repo.GetRevisionFinalistas(requerimientoId, userId.Value);
            if (revision == null)
                throw new AbrilException("No se encontró el informe de finalistas del requerimiento.", 404);

            return revision;
        }

        // ── Decisión final del solicitante sobre un finalista ─────────────────
        public async Task<FinalistaDecisionResultDto> RegistrarDecisionFinalista(
            int requerimientoId, FinalistaDecisionDto dto, int? userId)
        {
            if (!userId.HasValue)
                throw new AbrilException("No se pudo identificar al usuario.", 401);
            if (dto == null || dto.CandidatoId <= 0)
                throw new AbrilException("Selecciona al finalista sobre el que quieres decidir.", 400);

            var ctx = await _repo.RegistrarDecisionFinalista(
                requerimientoId, dto.CandidatoId, dto.Aprobado, userId.Value);
            var res = ctx.Resultado;

            // 1) Al rechazar, el finalista recibe el mismo correo de agradecimiento que le envía GTH
            //    a quien no supera la entrevista. Best-effort: la decisión ya quedó registrada.
            if (!res.Aprobado && !string.IsNullOrWhiteSpace(ctx.CandidatoCorreo))
            {
                try
                {
                    await EnviarAgradecimientoAsync(new AgradecimientoEnvioContextoDto
                    {
                        CandidatoNombre = res.CandidatoNombre,
                        Puesto          = ctx.Puesto,
                        Codigo          = ctx.Codigo,
                        Correo          = ctx.CandidatoCorreo,
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo enviar el correo de agradecimiento al finalista rechazado {CandidatoId}", dto.CandidatoId);
                }
            }

            // 2) Notificar la decisión a GTH (tipo FINALISTA_DECISION), igual que la de long list.
            await NotificarDecisionFinalistaAGthAsync(ctx);

            res.Message = res.Aprobado
                ? $"{res.CandidatoNombre} quedó seleccionado. El proceso de reclutamiento se cerró y GTH continuará con su onboarding."
                : res.TodosRechazados
                    ? $"{res.CandidatoNombre} fue rechazado y se le envió el correo de agradecimiento. Al no quedar finalistas, GTH preparará y enviará una nueva long list."
                    : $"{res.CandidatoNombre} fue rechazado y se le envió el correo de agradecimiento.";

            return res;
        }

        /// <summary>
        /// Envía a GTH el correo con la decisión final del solicitante sobre un finalista (tipo
        /// FINALISTA_DECISION). To = principales configurados, CC = copias. Sin principales no se
        /// envía. No bloquea: cualquier fallo solo se registra como warning.
        /// </summary>
        private async Task NotificarDecisionFinalistaAGthAsync(FinalistaDecisionContextoDto ctx)
        {
            try
            {
                var dest = await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.FinalistaDecision);
                if (dest.Para.Count == 0)
                {
                    _logger.LogWarning(
                        "No hay destinatarios principales activos para el correo de decisión de finalista ({Codigo}); no se envía.",
                        ctx.Codigo);
                    return;
                }

                var res     = ctx.Resultado;
                var accion  = res.Aprobado ? "aprobó" : "rechazó";
                var subject = $"[Reclutamiento] Decisión de finalista — {ctx.Codigo} · {ctx.Puesto}";

                await _email.SendAsync(
                    to:      dest.EmailsPara,
                    subject: subject,
                    body:    ConstruirCuerpoDecisionFinalista(ctx, accion),
                    isHtml:  true,
                    cc:      dest.Copias.Count > 0 ? dest.EmailsCopias : null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar el correo de decisión de finalista del requerimiento {Codigo}", ctx.Codigo);
            }
        }

        /// <summary>
        /// Cuerpo del correo a GTH con la decisión final del solicitante sobre un finalista. Lo que
        /// sigue después de la decisión va en la franja, que es una línea: el detalle del proceso
        /// se ve en la pantalla.
        /// </summary>
        private string ConstruirCuerpoDecisionFinalista(FinalistaDecisionContextoDto ctx, string accion)
        {
            var l   = Layout.Desde(_configuration);
            var res = ctx.Resultado;
            var solicitante = string.IsNullOrWhiteSpace(ctx.SolicitanteNombre)
                ? "El área solicitante"
                : ctx.SolicitanteNombre;

            var siguiente = res.Aprobado
                ? "El proceso de reclutamiento queda cerrado y el seleccionado pasa a onboarding."
                : res.TodosRechazados
                    ? "No quedan finalistas en carrera: el requerimiento volvió a Long list / CVs."
                    : "El proceso continúa con los finalistas que aún están pendientes de decisión.";

            return l.Documento(
                new Layout.Cabecera(
                    "req-finalista", "Decisión de Finalista",
                    $"{Layout.Esc(solicitante)} {Layout.Esc(accion)} a un finalista:"),
                l.Tarjeta(new List<Layout.Fila>
                {
                    new("req-codigo", "Requerimiento", Layout.Esc(ctx.Codigo)),
                    new("req-puesto", "Puesto", Textos.OGuion(ctx.Puesto)),
                    new("req-candidato", "Finalista", Textos.OGuion(res.CandidatoNombre)),
                    new("req-area", "Área solicitante", Textos.OGuion(ctx.Area)),
                    new("req-proyecto", "Proyecto / Obra", Textos.OGuion(ctx.ProyectoObra)),
                    new("req-estado", "Estado", Textos.OGuion(res.EstadoNombre)),
                }),
                res.Aprobado
                    ? l.Franja("req-check", Layout.Tono.Verde, $"<b>Aprobado.</b> {siguiente}")
                    : l.Franja("req-rechazadas", Layout.Tono.Rojo, $"<b>Rechazado.</b> {siguiente}"));
        }

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
            var dest = await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.LongList);

            // Para = solicitante primero + principales configurados; CC = copias que no estén en Para.
            var (principales, copias) = CorreoDestinatariosCombinador.Combinar(ctx.SolicitanteEmail, dest);

            if (principales.Count == 0)
                throw new AbrilException(
                    "No se pudo determinar el correo del solicitante de la long list y no hay " +
                    "destinatarios principales configurados. Verifica que el solicitante tenga " +
                    "un correo registrado o configúralos con el botón «Configuración».", 409);

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
                    body:    ConstruirCuerpoLongList(requerimientoId, ctx, candidatos),
                    isHtml:  true,
                    cc:      copias.Count > 0 ? copias : null,
                    attachments: adjuntos,
                    sender:  EmailSenders.Gth);
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
                    Nombre     = c.Nombre,
                    Comentario = c.Comentario,
                    CvNombre   = cvSubida.FileName,
                    CvUrl      = cvSubida.WebUrl,
                    CvItemId   = cvSubida.ItemId,
                    CvDriveId  = carpeta.DriveId,
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

        /// <summary>
        /// Enlace a la revisión de la long list dentro de «Solicitud de personal». Mismo mecanismo
        /// que el correo de aprobación a Gerencia: si el solicitante no tiene sesión, el
        /// <c>authGuard</c> del frontend lo manda al login con esta URL como <c>returnUrl</c> y lo
        /// devuelve acá al entrar. La ruta abre el modal «Revisar long list y CVs» directamente.
        /// </summary>
        private string ConstruirLinkRevisionLongList(int requerimientoId)
        {
            var frontendUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
            return $"{frontendUrl}/gestion-gth/solicitud-personal/long-list/{requerimientoId}";
        }

        /// <summary>
        /// Cuerpo del correo de la long list al solicitante. Los CVs y los informes van adjuntos al
        /// correo; la tabla es el índice de lo que trae y el botón lleva a la pantalla donde se
        /// aprueba o rechaza candidato por candidato.
        /// </summary>
        private string ConstruirCuerpoLongList(
            int requerimientoId, LongListEnvioContextoDto ctx, List<LongListCandidatoArchivoDto> candidatos)
        {
            var l    = Layout.Desde(_configuration);
            var link = ConstruirLinkRevisionLongList(requerimientoId);

            var datos = new List<Layout.Fila>
            {
                new("req-codigo", "Requerimiento", Layout.Esc(ctx.Codigo)),
                new("req-puesto", "Puesto", Textos.OGuion(ctx.Puesto)),
                new("req-area", "Área solicitante", Textos.OGuion(ctx.Area)),
                new("req-proyecto", "Proyecto / Obra", Textos.OGuion(ctx.ProyectoObra)),
            };
            if (ctx.SlaDias.HasValue)
                datos.Add(new("req-plazo", "Plazo estimado", $"{ctx.SlaDias} días"));

            var filas = new List<IReadOnlyList<Layout.Celda>>(candidatos.Count);
            for (var i = 0; i < candidatos.Count; i++)
            {
                var c = candidatos[i];
                filas.Add(new List<Layout.Celda>
                {
                    new((i + 1).ToString()),
                    new(string.IsNullOrWhiteSpace(c.Nombre) ? $"Candidato {i + 1}" : Layout.Esc(c.Nombre), Negrita: true),
                    new(Textos.OGuion(c.Comentario)),
                    new(c.InformeContent is { Length: > 0 } ? "Sí" : "No"),
                });
            }

            return l.Documento(
                new Layout.Cabecera(
                    "req-longlist", "Long List de CVs",
                    "GTH culminó el filtro de CVs. Los adjuntos van en este correo."),
                l.Tarjeta(datos),
                l.Seccion("req-candidatos", $"Candidatos ({candidatos.Count})"),
                l.Tabla(ColumnasLongList, filas),
                l.Boton("Revisar long list y CVs", link),
                l.EnlaceDirecto(link));
        }

        /// <summary>Columnas de la tabla de la long list (suman los 580px de la tarjeta).</summary>
        private static readonly IReadOnlyList<Layout.Columna> ColumnasLongList = new List<Layout.Columna>
        {
            new("#", 46, Layout.Alineacion.Centro),
            new("Candidato", 180),
            new("Comentario de GTH", 264),
            new("Informe", 90, Layout.Alineacion.Centro),
        };

        public async Task<SeguimientoDto> GetSeguimiento(int requerimientoId, int? userId)
        {
            if (!userId.HasValue)
                throw new AbrilException("No se pudo identificar al usuario.", 401);

            var seguimiento = await _repo.GetSeguimiento(requerimientoId, userId.Value);
            if (seguimiento == null)
                throw new AbrilException("Requerimiento no encontrado.", 404);

            // Tarjeta "Aprobación GG" del modal: la consulta vive en el repositorio dueño de
            // gth_aprobacion_gg. Es una lectura chica e indexada; null en los requerimientos
            // anteriores a esta funcionalidad (no pasaron por el paso del GG).
            seguimiento.AprobacionGg = await _aprobacionGgRepo.GetResumenByRequerimiento(requerimientoId);

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

            // La justificación es el sustento que leen el gerente del área y Gerencia General para
            // aprobar, así que sin ella la solicitud no se registra.
            var justificacion = dto.Justificacion?.Trim();
            if (string.IsNullOrWhiteSpace(justificacion))
                throw new AbrilException("Debe escribir la justificación general de la solicitud.", 400);
            if (justificacion.Length > MaxJustificacionLength)
                throw new AbrilException(
                    $"La justificación no puede superar los {MaxJustificacionLength} caracteres.", 400);

            for (int i = 0; i < dto.Vacantes.Count; i++)
            {
                var v = dto.Vacantes[i];
                var pos = i + 1;

                // Único origen del puesto: el catálogo. El alta de puestos nuevos es tarea de GTH
                // (catálogo de puestos), no de este formulario.
                if (v.PuestoId is null or <= 0)
                    throw new AbrilException($"Vacante {pos}: debe seleccionar un puesto.", 400);

                if (v.TipoRequerimientoId <= 0)   throw new AbrilException($"Vacante {pos}: debe seleccionar el tipo de requerimiento.", 400);
                if (v.ProjectId <= 0)             throw new AbrilException($"Vacante {pos}: debe seleccionar un proyecto/obra.", 400);

                // Salario bruto mensual: obligatorio y positivo. El tope es el de la columna
                // (numeric(12,2)) y ataja el dedazo de escribir el sueldo con céntimos pegados.
                if (v.SalarioBrutoMensual is null or <= 0)
                    throw new AbrilException($"Vacante {pos}: debe indicar el salario bruto mensual.", 400);
                if (v.SalarioBrutoMensual > MaxSalarioBrutoMensual)
                    throw new AbrilException(
                        $"Vacante {pos}: el salario bruto mensual no puede superar S/ {MaxSalarioBrutoMensual:N2}.", 400);

                // Se guarda con 2 decimales: la columna es numeric(12,2) y redondear acá deja el
                // dato igual en la BD y en el correo que ve el gerente.
                v.SalarioBrutoMensual = Math.Round(v.SalarioBrutoMensual.Value, 2, MidpointRounding.AwayFromZero);
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
                Justificacion       = justificacion,
            };

            // Sustento (opcional): validar y subir a SharePoint ANTES de persistir.
            if (sustento != null && sustento.Length > 0)
                await SubirSustentoAsync(sustento, solicitud);

            var result = await _repo.Create(solicitud, dto.Vacantes, userId);

            // Primer paso del flujo: la solicitud va a Gerencia General, NO a GTH. Un solo correo
            // con todas las vacantes; GTH se enterará recién cuando el GG apruebe. No bloquea la
            // creación: si el correo falla, la solicitud ya quedó registrada esperando el reenvío.
            result.CorreoGerenciaEnviado = await _aprobacionGg.EnviarSolicitudAGerencia(result.SolicitudId, userId);

            return result;
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
