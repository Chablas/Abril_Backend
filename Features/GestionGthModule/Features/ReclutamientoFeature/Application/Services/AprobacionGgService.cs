using System.Globalization;
using System.Security.Cryptography;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Shared;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Models;
using Abril_Backend.Shared.Services.Notificaciones.Dtos;
using Abril_Backend.Shared.Services.Notificaciones.Interfaces;
using Layout = Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Shared.ReclutamientoEmailLayout;
using Textos = Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Shared.ReclutamientoEmailTextos;

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
        /// Salario bruto mensual de la vacante formateado en soles para la tabla de los correos a
        /// los gerentes y a GTH (los dos que sí lo necesitan: uno lo aprueba y el otro arma la
        /// oferta). Guion cuando la vacante es anterior a que se pidiera el dato.
        /// </summary>
        private static string SalarioTexto(AprobacionGgVacanteDto v) =>
            v.SalarioBrutoMensual.HasValue
                ? $"S/ {v.SalarioBrutoMensual.Value.ToString("N2", CultureInfo.InvariantCulture)}"
                : "—";

        /// <summary>
        /// Cuerpo del correo a los gerentes: la solicitud completa en una tabla + un acceso a la
        /// pantalla «Aprobaciones», donde cada uno decide vacante por vacante. Es UN solo correo
        /// con varios destinatarios (Gerente General y gerente del área), así que el texto no puede
        /// hablarle a uno solo: dentro de la pantalla cada quien ve su propia casilla. Por eso el
        /// correo no explica el flujo de aprobación ni qué hace el botón — solo lleva los datos de
        /// la solicitud y el acceso.
        ///
        /// El HTML vive en <see cref="AprobacionGgEmailTemplate"/>, con la misma identidad visual
        /// que el correo de «EMO Confirmado».
        /// </summary>
        private string ConstruirCuerpoGerencia(AprobacionGgEnvioContextoDto ctx, bool esReenvio)
        {
            // El origen de las imágenes es una clave aparte de App:FrontendUrl a propósito: Outlook
            // no las descarga desde el cliente sino a través del proxy de imágenes de Microsoft, que
            // nunca puede alcanzar un localhost. Con App:FrontendUrl (que en dev tiene que seguir
            // apuntando a localhost para que el enlace del correo sea clicable) las imágenes salen
            // siempre rotas al probar en local.
            var assetsUrl = _configuration["App:EmailAssetsUrl"]
                ?? _configuration["App:FrontendUrl"]
                ?? "https://intranet.abril.pe";

            var vacantes = ctx.Vacantes
                .Select(v => new AprobacionGgEmailTemplate.Vacante(
                    Codigo:       v.Codigo,
                    Puesto:       v.Puesto,
                    Tipo:         v.TipoRequerimiento,
                    Reemplazado:  v.TrabajadorReemplazado,
                    ProyectoObra: string.IsNullOrWhiteSpace(v.ProyectoObra) ? "—" : v.ProyectoObra!,
                    Salario:      SalarioTexto(v)))
                .ToList();

            return AprobacionGgEmailTemplate.Construir(
                new AprobacionGgEmailTemplate.Datos(
                    Area:           string.IsNullOrWhiteSpace(ctx.Area) ? "—" : ctx.Area!,
                    Solicitante:    ctx.SolicitanteNombre,
                    Vacantes:       vacantes,
                    Justificacion:  ctx.Justificacion,
                    SustentoUrl:    ctx.SustentoUrl,
                    SustentoNombre: ctx.SustentoNombre,
                    Link:           ConstruirLink(ctx.AprobacionId),
                    EsRecordatorio: esReenvio),
                assetsUrl);
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

        /// <inheritdoc/>
        /// <remarks>
        /// Aprobar (o rechazar) desde la lista es exactamente el mismo acto que abrir el modal de
        /// cada solicitud y marcar todas sus vacantes igual, así que se apoya en las mismas reglas:
        /// el nivel lo resuelve el scope y solo la decisión de Gerencia General manda lo aprobado a
        /// GTH y a TI. Lo único propio del bloque es que la escritura ocurre en un solo lote y que
        /// los destinatarios de los correos se resuelven una vez para todo él.
        /// </remarks>
        public async Task<AprobacionGgDecisionMasivaResultDto> RegistrarDecisionMasiva(
            AprobacionGgDecisionMasivaDto dto, int? userId)
        {
            if (!userId.HasValue)
                throw new AbrilException("No se pudo identificar al usuario.", 401);

            var ids = dto?.AprobacionIds?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0)
                throw new AbrilException("Selecciona al menos una solicitud para registrar tu decisión.", 400);

            // El nivel sale de la categoría del usuario, nunca del payload: igual que en la decisión
            // de una sola, nadie puede pedir que su firma cuente como la de Gerencia General.
            var scope = await _scopes.ResolveAsync(userId);

            var ctx = await _repo.RegistrarDecisionMasiva(ids, dto!.Aprobado, dto.Comentario, userId.Value, scope);

            var res = new AprobacionGgDecisionMasivaResultDto
            {
                Nivel       = ctx.Nivel,
                Aprobado    = dto.Aprobado,
                Solicitudes = ctx.Registradas.Count,
                Vacantes    = ctx.Registradas.Sum(c => c.Resultado.Aprobados + c.Resultado.Rechazados),
                Omitidas    = ctx.Omitidas,
            };

            // Los correos salen como en la decisión de una: UNO por solicitud aprobada. GTH y TI
            // trabajan por solicitud (cada correo lleva sus códigos de vacante y su justificación),
            // así que fusionarlas en un solo correo cambiaría lo que reciben. Solo el visto bueno del
            // gerente del área sigue sin disparar nada.
            if (ctx.Nivel == AprobacionNivel.GerenteGeneral)
            {
                var conAprobadas = ctx.Registradas.Where(c => c.Aprobadas.Count > 0).ToList();
                if (conAprobadas.Count > 0)
                {
                    var destGth = await ResolverDestinatariosDelLote(CorreoTipoReclutamiento.Solicitud);
                    var destTi  = await ResolverDestinatariosDelLote(CorreoTipoReclutamiento.Ti);

                    // Best-effort, como en la decisión de una: la decisión ya quedó registrada y no
                    // se revierte porque un correo falle.
                    foreach (var c in conAprobadas)
                    {
                        if (destGth != null) await NotificarAGthAsync(c, userId, destGth);
                        if (destTi  != null) await NotificarATiAsync(c, destTi);
                    }
                }
            }

            res.Message = ConstruirMensajeMasivo(res);
            return res;
        }

        /// <summary>
        /// Destinatarios de un correo resueltos una sola vez para todo el lote. Null si no se
        /// pudieron resolver: en ese caso ese correo no sale, pero la decisión ya está registrada.
        /// </summary>
        private async Task<SolicitudDestinatariosDto?> ResolverDestinatariosDelLote(string tipoCodigo)
        {
            try
            {
                return await _destinatarios.ResolverAsync(tipoCodigo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "No se pudieron resolver los destinatarios del correo {Tipo} de la decisión en bloque", tipoCodigo);
                return null;
            }
        }

        /// <summary>
        /// Mensaje de la decisión en bloque. Como en la decisión de una, dice si la solicitud avanza
        /// —lo único que el gerente necesita saber—: el Gerente General manda lo aprobado a GTH, el
        /// gerente del área solo deja constancia. Si hubo solicitudes omitidas se dice cuántas, para
        /// que el conteo no quede sin explicar.
        /// </summary>
        private static string ConstruirMensajeMasivo(AprobacionGgDecisionMasivaResultDto res)
        {
            if (res.Solicitudes == 0)
                return "No se registró ninguna decisión: las solicitudes seleccionadas ya no admitían tu decisión.";

            var omitidas = res.Omitidas.Count == 0
                ? string.Empty
                : $" {res.Omitidas.Count} solicitud(es) quedaron fuera porque ya no admitían tu decisión.";

            if (res.Nivel == AprobacionNivel.GerenteGeneral)
            {
                return res.Aprobado
                    ? $"Aprobaste {res.Solicitudes} solicitud(es) ({res.Vacantes} vacante(s)). Ya se enviaron a Gestión de Talento Humano para iniciar el reclutamiento.{omitidas}"
                    : $"Rechazaste {res.Solicitudes} solicitud(es) ({res.Vacantes} vacante(s)). Ninguna continúa y no se enviaron a Gestión de Talento Humano.{omitidas}";
            }

            return res.Aprobado
                ? $"Visto bueno registrado en {res.Solicitudes} solicitud(es) ({res.Vacantes} vacante(s)). Avanzan recién con la aprobación de Gerencia General.{omitidas}"
                : $"Observaste {res.Solicitudes} solicitud(es) ({res.Vacantes} vacante(s)). Gerencia General verá tu postura al decidir.{omitidas}";
        }

        /// <summary>
        /// Correo + campanita a GTH con las vacantes que Gerencia General aprobó (tipo SOLICITUD).
        /// Es el correo de "nueva solicitud de personal" que antes salía al registrar la solicitud:
        /// ahora espera la aprobación del GG y solo lleva lo aprobado. No bloquea.
        /// </summary>
        private async Task NotificarAGthAsync(
            AprobacionGgDecisionContextoDto ctx, int? userId, SolicitudDestinatariosDto? destinatarios = null)
        {
            // En la decisión de UNA solicitud los destinatarios se resuelven acá. En la decisión en
            // bloque llegan ya resueltos, una sola vez para todo el lote: no dependen de la
            // solicitud ni de su área, así que repetir la consulta por cada una sería un roundtrip
            // por solicitud sin ninguna diferencia en el resultado.
            var dest = destinatarios;
            if (dest == null)
            {
                try
                {
                    dest = await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.Solicitud);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo resolver a los destinatarios de GTH de la solicitud {SolicitudId}", ctx.SolicitudId);
                    return;
                }
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
        private async Task NotificarATiAsync(
            AprobacionGgDecisionContextoDto ctx, SolicitudDestinatariosDto? destinatarios = null)
        {
            try
            {
                // Igual que en el correo a GTH: en bloque llegan ya resueltos para todo el lote.
                var dest = destinatarios ?? await _destinatarios.ResolverAsync(CorreoTipoReclutamiento.Ti);
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
        /// Filas de la tabla de vacantes aprobadas. La línea "Reemplaza a {trabajador}" va bajo el
        /// tipo, en la misma celda, para no agregar una columna a una tabla que ya se lee bien.
        /// </summary>
        /// <param name="conSalario">
        /// false en el correo a TI: no participa del reclutamiento ni arma la oferta, así que la
        /// banda salarial no es asunto suyo y su tabla tiene una columna menos.
        /// </param>
        private static List<IReadOnlyList<Layout.Celda>> FilasVacantes(
            IReadOnlyList<AprobacionGgVacanteDto> vacantes, bool conSalario)
        {
            var filas = new List<IReadOnlyList<Layout.Celda>>(vacantes.Count);
            foreach (var v in vacantes)
            {
                var celdas = new List<Layout.Celda>
                {
                    new(Layout.Esc(v.Codigo), Negrita: true, Color: Layout.Azul, NoWrap: true),
                    new(Layout.Esc(v.Puesto)),
                    new(Layout.Esc(v.TipoRequerimiento) + Textos.Reemplaza(v.TrabajadorReemplazado)),
                    new(Textos.OGuion(v.ProyectoObra)),
                };
                if (conSalario) celdas.Add(new(Layout.Esc(SalarioTexto(v)), Negrita: true, NoWrap: true));
                filas.Add(celdas);
            }
            return filas;
        }

        /// <summary>
        /// Cuerpo del correo a TI: las vacantes aprobadas, para que puedan ir previendo lo que
        /// necesitará cada puesto. No lleva justificación, comentario ni las vacantes rechazadas:
        /// eso es contexto de la decisión y del reclutamiento, no del trabajo de TI. La fecha de
        /// ingreso no se conoce en este punto — la confirma GTH al cerrar el proceso, y eso ya no
        /// se dice en el correo: era una nota al pie que nadie accionaba.
        /// </summary>
        private string ConstruirCuerpoTi(AprobacionGgDecisionContextoDto ctx)
        {
            var l = Layout.Desde(_configuration);

            var datos = new List<Layout.Fila>
            {
                new("req-area", "Área solicitante", Textos.OGuion(ctx.Area)),
            };
            if (!string.IsNullOrWhiteSpace(ctx.SolicitanteNombre))
                datos.Add(new("req-solicitante", "Solicitante", Layout.Esc(ctx.SolicitanteNombre)));

            return l.Documento(
                new Layout.Cabecera(
                    "req-ti", "Vacantes Aprobadas", "Para que TI vaya previendo equipo, usuario y accesos:"),
                l.Tarjeta(datos),
                l.Seccion("req-vacantes", $"Vacantes aprobadas ({ctx.Aprobadas.Count})"),
                l.Tabla(Textos.ColumnasVacantes, FilasVacantes(ctx.Aprobadas, conSalario: false)));
        }

        /// <summary>
        /// Cuerpo del correo a GTH: las vacantes que aprobó Gerencia General, con el contexto de la
        /// decisión (justificación, comentario del GG, visto bueno del gerente del área y sustento).
        /// Las rechazadas van como franja y no como tabla: no generan trabajo para GTH, solo
        /// explican por qué la solicitud llegó incompleta.
        /// </summary>
        private string ConstruirCuerpoGth(AprobacionGgDecisionContextoDto ctx)
        {
            var l = Layout.Desde(_configuration);

            var datos = new List<Layout.Fila>
            {
                new("req-area", "Área solicitante", Textos.OGuion(ctx.Area)),
            };
            if (!string.IsNullOrWhiteSpace(ctx.SolicitanteNombre))
                datos.Add(new("req-solicitante", "Solicitante", Layout.Esc(ctx.SolicitanteNombre)));

            // El visto bueno del gerente del área no condiciona nada, pero saber que opinó (y qué
            // opinó) le da contexto a GTH. Vacío si nunca llegó a registrarlo.
            var contexto = new List<Layout.Fila>();
            if (!string.IsNullOrWhiteSpace(ctx.Justificacion))
                contexto.Add(new("req-justificacion", "Justificación", Layout.EscMultilinea(ctx.Justificacion)));
            if (!string.IsNullOrWhiteSpace(ctx.Comentario))
                contexto.Add(new("req-comentario", "Comentario de GG", Layout.EscMultilinea(ctx.Comentario)));
            if (!string.IsNullOrWhiteSpace(ctx.GerenteAreaResumen))
                contexto.Add(new("req-vistobueno", "Visto bueno del área", Layout.Esc(ctx.GerenteAreaResumen)));
            if (!string.IsNullOrWhiteSpace(ctx.SustentoUrl))
                contexto.Add(new("req-sustento", "Sustento adjunto",
                    Textos.Enlace(ctx.SustentoUrl!, ctx.SustentoNombre ?? "Ver documento")));

            var rechazadas = ctx.Rechazadas.Count == 0
                ? ""
                : l.Franja("req-rechazadas", Layout.Tono.Rojo,
                    $"<b>No aprobadas ({ctx.Rechazadas.Count}):</b> "
                    + Layout.Esc(string.Join(", ", ctx.Rechazadas.Select(v => $"{v.Codigo} ({v.Puesto})"))) + ".");

            return l.Documento(
                new Layout.Cabecera(
                    "req-aprobada", "Solicitud Aprobada", "Gerencia General aprobó estas vacantes:"),
                l.Tarjeta(datos),
                l.Seccion("req-vacantes", $"Vacantes aprobadas ({ctx.Aprobadas.Count})"),
                l.Tabla(Textos.ColumnasVacantesConSalario, FilasVacantes(ctx.Aprobadas, conSalario: true)),
                rechazadas,
                l.Tarjeta(contexto));
        }
    }
}
