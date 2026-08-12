using System.Text;
using System.Text.RegularExpressions;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
{
    public class ReclutamientoService : IReclutamientoService
    {
        private readonly IReclutamientoRepository _repo;
        private readonly IAprobacionGgRepository  _aprobacionGgRepo;
        private readonly IAprobacionGgService     _aprobacionGg;
        private readonly IGraphSharePointService  _sharePoint;
        private readonly IEmailService            _email;
        private readonly ILogger<ReclutamientoService> _logger;

        private const long MaxSustentoBytes = 10 * 1024 * 1024; // 10 MB
        private static readonly string[] AllowedSustentoExt = { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };

        public ReclutamientoService(
            IReclutamientoRepository repo,
            IAprobacionGgRepository aprobacionGgRepo,
            IAprobacionGgService aprobacionGg,
            IGraphSharePointService sharePoint,
            IEmailService email,
            ILogger<ReclutamientoService> logger)
        {
            _repo             = repo;
            _aprobacionGgRepo = aprobacionGgRepo;
            _aprobacionGg     = aprobacionGg;
            _sharePoint       = sharePoint;
            _email            = email;
            _logger           = logger;
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
            await NotificarDecisionAGthAsync(ctx);

            return ctx.Resultado;
        }

        /// <summary>
        /// Envía a GTH el correo con la decisión del solicitante sobre la long list (tipo
        /// LONG_LIST_DECISION). To = principales configurados, CC = copias. Sin principales no se
        /// envía. No bloquea: cualquier fallo solo se registra como warning.
        /// </summary>
        private async Task NotificarDecisionAGthAsync(LongListDecisionContextoDto ctx)
        {
            try
            {
                var dest = await _repo.GetCorreoDestinatarios(CorreoTipoReclutamiento.LongListDecision);
                if (dest.Principales.Count == 0)
                {
                    _logger.LogWarning(
                        "No hay destinatarios principales configurados para el correo de decisión de long list ({Codigo}); no se envía.",
                        ctx.Codigo);
                    return;
                }

                var estado = ctx.Resultado.TodosRechazados ? "rechazó a todos los candidatos" : $"aprobó {ctx.Resultado.Aprobados} candidato(s)";
                var subject = $"[Reclutamiento] Decisión de long list — {ctx.Codigo} · {ctx.Puesto}";

                await _email.SendAsync(
                    to:      dest.Principales,
                    subject: subject,
                    body:    ConstruirCuerpoDecision(ctx),
                    isHtml:  true,
                    cc:      dest.Copias.Count > 0 ? dest.Copias : null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar el correo de decisión de long list del requerimiento {Codigo}", ctx.Codigo);
            }
        }

        private static string ConstruirCuerpoDecision(LongListDecisionContextoDto ctx)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

            var r = ctx.Resultado;
            var filas = new StringBuilder();
            for (int i = 0; i < ctx.Candidatos.Count; i++)
            {
                var c = ctx.Candidatos[i];
                var nombre = string.IsNullOrWhiteSpace(c.Nombre) ? $"Candidato {i + 1}" : Esc(c.Nombre);
                var puesto = string.IsNullOrWhiteSpace(c.Puesto) ? "—" : Esc(c.Puesto);
                var (etiqueta, color) = c.Aprobado ? ("Aprobado", "#15803D") : ("Rechazado", "#B91C1C");
                filas.Append($"""
                    <tr>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb;text-align:center">{i + 1}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb;font-weight:bold">{nombre}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb">{puesto}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb;text-align:center;font-weight:bold;color:{color}">{etiqueta}</td>
                    </tr>
                    """);
            }

            var solicitante = string.IsNullOrWhiteSpace(ctx.SolicitanteNombre)
                ? ""
                : $"""<p style="font-size:13px"><b>Solicitante:</b> {Esc(ctx.SolicitanteNombre)}</p>""";

            // Mensaje de cierre según el resultado.
            var cierre = r.TodosRechazados
                ? """<p style="font-size:13px;color:#B91C1C"><b>El solicitante rechazó a todos los candidatos.</b> El requerimiento vuelve a la etapa de long list: GTH debe preparar y enviar una nueva long list para su revisión.</p>"""
                : $"""<p style="font-size:13px;color:#15803D"><b>El solicitante aprobó {r.Aprobados} candidato(s).</b> GTH continúa el proceso únicamente con los candidatos aprobados (envío de la plantilla del proceso y pruebas psicotécnicas).</p>""";

            return $"""
                <div style="font-family:Arial,sans-serif;max-width:680px">
                  <div style="background:#005D9D;padding:12px 16px">
                    <h2 style="color:#fff;margin:0;font-size:18px">Decisión de la long list</h2>
                  </div>
                  <div style="padding:16px;border:1px solid #e5e7eb;border-top:none">
                    <p style="font-size:13px;margin-top:0">
                      El solicitante revisó la long list y registró su decisión sobre los candidatos del
                      siguiente requerimiento.
                    </p>
                    <p style="font-size:13px"><b>Requerimiento:</b> {Esc(ctx.Codigo)}</p>
                    <p style="font-size:13px"><b>Puesto:</b> {Esc(ctx.Puesto)}</p>
                    <p style="font-size:13px"><b>Área solicitante:</b> {Esc(ctx.Area)}</p>
                    <p style="font-size:13px"><b>Proyecto / Obra:</b> {Esc(ctx.ProyectoObra)}</p>
                    {solicitante}
                    <p style="font-size:13px"><b>Resumen:</b> {r.Aprobados} aprobado(s) · {r.Rechazados} rechazado(s)</p>
                    <table cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;font-size:13px;margin:10px 0">
                      <thead>
                        <tr style="background:#f3f4f6">
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:center">#</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Candidato</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:left">Puesto</th>
                          <th style="padding:6px 10px;border:1px solid #e5e7eb;text-align:center">Decisión</th>
                        </tr>
                      </thead>
                      <tbody>{filas}</tbody>
                    </table>
                    {cierre}
                    <p style="font-size:11px;color:#888;margin-top:16px">Correo automático de Abril One · Gestión GTH · Reclutamiento.</p>
                  </div>
                </div>
                """;
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

            // El correo es best-effort: la entrevista ya quedó programada, así que un fallo del
            // envío se informa en el mensaje en vez de tumbar la operación.
            var message = $"Invitación enviada a {ctx.Resumen.CorreoEnvio}.";
            try
            {
                await _email.SendAsync(
                    to:      new List<string> { ctx.Resumen.CorreoEnvio },
                    subject: $"Entrevista — {ctx.Puesto} · Abril Grupo Inmobiliario",
                    body:    ConstruirCuerpoEntrevista(ctx),
                    isHtml:  true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar la invitación a la entrevista del candidato {CandidatoId}", candidatoId);
                message = "La entrevista quedó programada, pero no se pudo enviar la invitación por correo. Vuelve a intentarlo.";
            }

            return new EntrevistaAccionResultDto { Message = message, Entrevista = ctx.Resumen };
        }

        /// <summary>Correo genérico de citación a entrevista (fecha, hora y lugar de la cita).</summary>
        private static string ConstruirCuerpoEntrevista(EntrevistaEnvioContextoDto ctx)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            var nombre = string.IsNullOrWhiteSpace(ctx.CandidatoNombre) ? "postulante" : Esc(ctx.CandidatoNombre);
            var fecha  = ctx.Resumen.Fecha.ToString("dd/MM/yyyy");

            return $"""
                <div style="font-family:Arial,sans-serif;max-width:640px">
                  <div style="background:#005D9D;padding:14px 18px">
                    <h2 style="color:#fff;margin:0;font-size:18px">Invitación a entrevista</h2>
                  </div>
                  <div style="padding:18px;border:1px solid #e5e7eb;border-top:none">
                    <p style="font-size:13px;margin-top:0">Estimado(a) {nombre},</p>
                    <p style="font-size:13px">
                      Continuamos con tu proceso de selección en <b>Abril Grupo Inmobiliario</b> para la
                      posición <b>{Esc(ctx.Puesto)}</b>. Te invitamos a la entrevista programada con los
                      siguientes datos:
                    </p>
                    <table style="font-size:13px;border-collapse:collapse;margin:14px 0">
                      <tr><td style="padding:4px 14px 4px 0;color:#555">Fecha</td><td style="padding:4px 0"><b>{fecha}</b></td></tr>
                      <tr><td style="padding:4px 14px 4px 0;color:#555">Hora</td><td style="padding:4px 0"><b>{Esc(ctx.Resumen.Hora)}</b></td></tr>
                      <tr><td style="padding:4px 14px 4px 0;color:#555">Lugar</td><td style="padding:4px 0"><b>{Esc(ctx.Resumen.LugarNombre)}</b></td></tr>
                    </table>
                    <p style="font-size:13px">
                      Te pedimos llegar con 10 minutos de anticipación y traer tu documento de identidad.
                      Si no pudieras asistir, responde este correo para reprogramar la cita.
                    </p>
                    <p style="font-size:11px;color:#888;margin-top:18px">
                      Correo automático de Abril One · Gestión GTH · Reclutamiento · {Esc(ctx.Codigo)}.
                    </p>
                  </div>
                </div>
                """;
        }

        // ── Evaluación de la entrevista y no continuidad ──────────────────────
        public async Task<EvaluacionAccionResultDto> GuardarEvaluacion(
            int candidatoId, EvaluacionGuardarDto dto, int? userId)
        {
            if (dto == null)
                throw new AbrilException("Datos de la evaluación no recibidos.", 400);

            ValidarPuntaje(dto.PuntajeEntrevista,   "de la entrevista");
            ValidarPuntaje(dto.PuntajePsicotecnico, "psicotécnico");
            ValidarPuntaje(dto.PuntajeTecnica,      "de la evaluación técnica");
            ValidarPuntaje(dto.PuntajeResultado,    "del resultado");

            var resumen = await _repo.GuardarEvaluacion(candidatoId, dto, userId);
            return new EvaluacionAccionResultDto { Message = "Evaluación guardada.", Evaluacion = resumen };
        }

        /// <summary>Los puntajes son porcentajes: 0-100 o null (aún sin registrar).</summary>
        private static void ValidarPuntaje(int? valor, string campo)
        {
            if (valor is < 0 or > 100)
                throw new AbrilException($"El puntaje {campo} debe estar entre 0 y 100.", 400);
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
                await _email.SendAsync(
                    to:      new List<string> { ctx.Correo },
                    subject: $"Proceso de selección — {ctx.Puesto} · Abril Grupo Inmobiliario",
                    body:    ConstruirCuerpoAgradecimiento(ctx),
                    isHtml:  true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el correo de agradecimiento del candidato {CandidatoId}", candidatoId);
                message = "El candidato quedó registrado como no continúa, pero no se pudo enviar el correo de agradecimiento. Vuelve a intentarlo.";
            }

            return new EvaluacionAccionResultDto { Message = message, Evaluacion = ctx.Resumen };
        }

        /// <summary>
        /// Correo genérico de agradecimiento para el candidato que no continúa en el proceso. No
        /// menciona puntajes ni motivos: solo agradece la participación y deja abierta la puerta a
        /// futuros procesos.
        /// </summary>
        private static string ConstruirCuerpoAgradecimiento(AgradecimientoEnvioContextoDto ctx)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            var nombre = string.IsNullOrWhiteSpace(ctx.CandidatoNombre) ? "postulante" : Esc(ctx.CandidatoNombre);

            return $"""
                <div style="font-family:Arial,sans-serif;max-width:640px">
                  <div style="background:#005D9D;padding:14px 18px">
                    <h2 style="color:#fff;margin:0;font-size:18px">Gracias por participar</h2>
                  </div>
                  <div style="padding:18px;border:1px solid #e5e7eb;border-top:none">
                    <p style="font-size:13px;margin-top:0">Estimado(a) {nombre},</p>
                    <p style="font-size:13px">
                      Agradecemos el tiempo que dedicaste al proceso de selección de
                      <b>Abril Grupo Inmobiliario</b> para la posición <b>{Esc(ctx.Puesto)}</b>, así como
                      el interés que mostraste por formar parte de nuestro equipo.
                    </p>
                    <p style="font-size:13px">
                      Luego de evaluar a todos los participantes, hemos decidido continuar el proceso con
                      otros candidatos cuyo perfil se ajusta más a lo que la posición requiere en este
                      momento. Esta decisión no desmerece tu experiencia ni tus capacidades profesionales.
                    </p>
                    <p style="font-size:13px">
                      Conservaremos tus datos en nuestra base de postulantes para considerarte en futuras
                      convocatorias que se ajusten a tu perfil. Te deseamos mucho éxito en tu desarrollo
                      profesional.
                    </p>
                    <p style="font-size:13px;margin-bottom:0">
                      Atentamente,<br />
                      <b>Gestión de Talento Humano</b><br />
                      Abril Grupo Inmobiliario
                    </p>
                    <p style="font-size:11px;color:#888;margin-top:18px">
                      Correo automático de Abril One · Gestión GTH · Reclutamiento · {Esc(ctx.Codigo)}.
                    </p>
                  </div>
                </div>
                """;
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
                    await _email.SendAsync(
                        to:      new List<string> { ctx.CandidatoCorreo },
                        subject: $"Proceso de selección — {ctx.Puesto} · Abril Grupo Inmobiliario",
                        body:    ConstruirCuerpoAgradecimiento(new AgradecimientoEnvioContextoDto
                        {
                            CandidatoNombre = res.CandidatoNombre,
                            Puesto          = ctx.Puesto,
                            Codigo          = ctx.Codigo,
                            Correo          = ctx.CandidatoCorreo,
                        }),
                        isHtml:  true);
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
                var dest = await _repo.GetCorreoDestinatarios(CorreoTipoReclutamiento.FinalistaDecision);
                if (dest.Principales.Count == 0)
                {
                    _logger.LogWarning(
                        "No hay destinatarios principales configurados para el correo de decisión de finalista ({Codigo}); no se envía.",
                        ctx.Codigo);
                    return;
                }

                var res     = ctx.Resultado;
                var accion  = res.Aprobado ? "aprobó" : "rechazó";
                var subject = $"[Reclutamiento] Decisión de finalista — {ctx.Codigo} · {ctx.Puesto}";

                await _email.SendAsync(
                    to:      dest.Principales,
                    subject: subject,
                    body:    ConstruirCuerpoDecisionFinalista(ctx, accion),
                    isHtml:  true,
                    cc:      dest.Copias.Count > 0 ? dest.Copias : null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar el correo de decisión de finalista del requerimiento {Codigo}", ctx.Codigo);
            }
        }

        private static string ConstruirCuerpoDecisionFinalista(FinalistaDecisionContextoDto ctx, string accion)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

            var res         = ctx.Resultado;
            var solicitante = string.IsNullOrWhiteSpace(ctx.SolicitanteNombre) ? "El área solicitante" : Esc(ctx.SolicitanteNombre);
            var color       = res.Aprobado ? "#15803D" : "#B91C1C";
            var siguiente   = res.Aprobado
                ? "El proceso de reclutamiento queda cerrado y el seleccionado pasa al proceso de onboarding."
                : res.TodosRechazados
                    ? "No quedan finalistas en carrera: el requerimiento volvió a Long list / CVs para que GTH envíe una nueva long list."
                    : "El proceso continúa con los finalistas que aún están pendientes de decisión.";

            return $"""
                <div style="font-family:Arial,sans-serif;max-width:640px">
                  <div style="background:#005D9D;padding:14px 18px">
                    <h2 style="color:#fff;margin:0;font-size:18px">Decisión de finalista</h2>
                  </div>
                  <div style="padding:18px;border:1px solid #e5e7eb;border-top:none">
                    <p style="font-size:13px;margin-top:0">
                      {solicitante} {accion} a un finalista del requerimiento
                      <b>{Esc(ctx.Codigo)}</b> — <b>{Esc(ctx.Puesto)}</b>.
                    </p>
                    <table style="font-size:13px;border-collapse:collapse;margin:14px 0">
                      <tr><td style="padding:4px 14px 4px 0;color:#555">Finalista</td><td style="padding:4px 0"><b>{Esc(res.CandidatoNombre)}</b></td></tr>
                      <tr><td style="padding:4px 14px 4px 0;color:#555">Decisión</td><td style="padding:4px 0"><b style="color:{color}">{(res.Aprobado ? "Aprobado" : "Rechazado")}</b></td></tr>
                      <tr><td style="padding:4px 14px 4px 0;color:#555">Área</td><td style="padding:4px 0">{Esc(ctx.Area) }</td></tr>
                      <tr><td style="padding:4px 14px 4px 0;color:#555">Proyecto/Obra</td><td style="padding:4px 0">{Esc(ctx.ProyectoObra)}</td></tr>
                      <tr><td style="padding:4px 14px 4px 0;color:#555">Estado</td><td style="padding:4px 0">{Esc(res.EstadoNombre)}</td></tr>
                    </table>
                    <p style="font-size:13px">{siguiente}</p>
                    <p style="font-size:11px;color:#888;margin-top:18px">
                      Correo automático de Abril One · Gestión GTH · Reclutamiento.
                    </p>
                  </div>
                </div>
                """;
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

        private static string ConstruirCuerpoLongList(LongListEnvioContextoDto ctx, List<LongListCandidatoArchivoDto> candidatos)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

            var filas = new StringBuilder();
            for (int i = 0; i < candidatos.Count; i++)
            {
                var c = candidatos[i];
                var comentario = string.IsNullOrWhiteSpace(c.Comentario) ? "—" : Esc(c.Comentario);
                var nombre     = string.IsNullOrWhiteSpace(c.Nombre) ? $"Candidato {i + 1}" : Esc(c.Nombre);
                var informe    = c.InformeContent != null && c.InformeContent.Length > 0 ? "Sí" : "No";
                filas.Append($"""
                    <tr>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb;text-align:center">{i + 1}</td>
                      <td style="padding:6px 10px;border:1px solid #e5e7eb;font-weight:bold">{nombre}</td>
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
