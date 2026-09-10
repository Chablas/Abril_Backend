using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Revisores.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Services
{
    public class RendicionService : IRendicionService
    {
        private readonly IRendicionRepository            _repo;
        private readonly IConsolidadoS10Service          _consolidadoService;
        private readonly IGestionSalidaService           _gestionSalidaService;
        private readonly IJefeRevisorResolver            _revisorResolver;
        private readonly ICorreoSalidaRecipientResolver  _correoResolver;
        private readonly IEmailService                   _emailService;
        private readonly IConfiguration                  _configuration;
        private readonly ILogger<RendicionService>       _logger;

        public RendicionService(
            IRendicionRepository repo,
            IConsolidadoS10Service consolidadoService,
            IGestionSalidaService gestionSalidaService,
            IJefeRevisorResolver revisorResolver,
            ICorreoSalidaRecipientResolver correoResolver,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<RendicionService> logger)
        {
            _repo                 = repo;
            _consolidadoService   = consolidadoService;
            _gestionSalidaService = gestionSalidaService;
            _revisorResolver      = revisorResolver;
            _correoResolver       = correoResolver;
            _emailService         = emailService;
            _configuration        = configuration;
            _logger               = logger;
        }

        public async Task<RendicionListResultDto> GetByUserId(int userId, RendicionFiltersDto? filters = null)
        {
            // Las tarjetas se cuentan sobre lo mismo que muestra la tabla, así que salen de esta
            // lista: el listado no está paginado, ya viene entero.
            var data = await _repo.GetByUserId(userId, filters);
            return new RendicionListResultDto
            {
                Data    = data,
                Resumen = ResumenRendicionesDto.De(data),
            };
        }

        public async Task<RendicionFilterDataDto> GetFilterData(int userId)
        {
            var (workerId, periodos) = await _repo.GetPeriodos(userId);

            var data = new RendicionFilterDataDto { Periodos = periodos };

            // A quién le llegan los dos correos que dispara la pantalla: el aviso de la primera
            // revisión ("Enviar a revisión") y el del Consolidado del S10 ("Avisar al revisor").
            // Se calculan con las MISMAS llamadas que hacen los envíos para que las confirmaciones
            // no anuncien un correo a alguien que la configuración dejó fuera. Van acá y no con el
            // listado porque son iguales para toda la pantalla —está acotada a un solo trabajador—
            // y no cambian al mover los filtros. Los dos tienen al mismo destinatario principal:
            // el jefe/revisor del trabajador, que por eso se resuelve una sola vez.
            //
            // Best-effort: si no se resuelven, la pantalla se muestra sin las listas de correos.
            // Un preview no puede tumbar el listado de planillas.
            if (workerId != null)
            {
                try
                {
                    var revisor = await _revisorResolver.ResolveAsync(workerId.Value);
                    var principal = string.IsNullOrWhiteSpace(revisor?.Email)
                        ? null
                        : new List<string> { revisor!.Email };

                    data.CorreoPrimeraRevision = await ResolverDestinatariosAsync(
                        CorreoEventoCodigos.RendicionPrimeraRevision, principal);
                    data.CorreoS10Revisor = await ResolverDestinatariosAsync(
                        CorreoEventoCodigos.S10Revisor, principal);

                    // Este no va al revisor sino al Coordinador ERP, que se resuelve por rol y
                    // no por el organigrama: por eso su destinatario principal es otro.
                    data.CorreoCorreccionS10 = await ResolverDestinatariosAsync(
                        CorreoEventoCodigos.CorreccionS10Solicitada,
                        await _repo.GetCorreosCoordinadorErp());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error resolviendo los destinatarios de los correos del trabajador {WorkerId}",
                        workerId.Value);
                }
            }

            return data;
        }

        /// <summary>
        /// A quién le llega un correo del flujo hoy. Devuelve vacío cuando está apagado o sin
        /// destinatarios: la pantalla lo muestra como "no le llega a nadie" en vez de prometer un
        /// envío que no va a pasar.
        /// </summary>
        private async Task<CorreoDestinatariosDto> ResolverDestinatariosAsync(
            string eventoCodigo, List<string>? destinatarioPrincipal)
        {
            var envio = await _correoResolver.ResolveEnvioAsync(eventoCodigo, destinatarioPrincipal);
            return envio.Enviar
                ? new CorreoDestinatariosDto { Para = envio.Para, Copia = envio.Copia }
                : new CorreoDestinatariosDto();
        }

        public async Task<RendicionDetalleDto> GetDetalle(int rendicionId, int userId) =>
            await _repo.GetDetalleForUser(rendicionId, userId)
                ?? throw new AbrilException("La planilla de rendición no existe o no es tuya.", 404);

        public async Task<string> EnviarAPrimeraRevision(int rendicionId, int userId)
        {
            // GetDetalle ya trae el guard de propiedad y el estado de la primera revisión, así que
            // las validaciones se hacen contra él y no contra consultas sueltas.
            var planilla = await GetDetalle(rendicionId, userId);

            if (!planilla.PuedeEnviarPrimeraRevision)
                throw new AbrilException(
                    planilla.EstadoPrimeraRevision == EstadosSalida.PrimeraRevision.NombreEnRevision
                        ? "Esta rendición ya está en primera revisión: espera la decisión de tu jefatura."
                        : planilla.EstadoPrimeraRevision == EstadosSalida.PrimeraRevision.NombreObservada
                            ? "Esta rendición está observada: corrige las capturas y los montos y vuelve a generarla antes de enviarla."
                            : "Esta rendición ya pasó la primera revisión.",
                    400);

            var solicitante = await _repo.GetSolicitante(rendicionId, userId)
                ?? throw new AbrilException("No se pudo identificar al trabajador de la planilla.", 409);

            var revisor = await _revisorResolver.ResolveAsync(solicitante.WorkerId);
            if (string.IsNullOrWhiteSpace(revisor?.Email))
                throw new AbrilException(
                    "No se pudo determinar el correo de tu jefe/revisor. Avisa a Gestión del Talento Humano.", 409);

            var datos = new RendicionRevisionCorreoDatos
            {
                RendicionId    = rendicionId,
                Codigo         = planilla.Codigo,
                Trabajador     = solicitante.Trabajador,
                Area           = solicitante.Area,
                Periodo        = planilla.Periodo,
                SalidasCount   = planilla.SalidasCount,
                TramosCount    = await _repo.ContarTramos(rendicionId, userId),
                MontoTotal     = planilla.MontoTotal,
                NumeroPlanilla = planilla.NumeroPlanilla,
            };

            // El estado se mueve ANTES de los correos: si un envío falla, la rendición igual quedó
            // en revisión (el jefe la ve en su bandeja) y el trabajador puede reenviar el aviso.
            // Al revés, un correo enviado sobre una rendición que se quedó en "Lista para enviar"
            // mandaría al jefe a una pantalla donde no tiene nada que decidir.
            await _repo.MarcarEnviadaAPrimeraRevision(rendicionId, userId);

            var enviadoRevisorA = await NotificarRevisorPrimeraRevisionAsync(datos, revisor!.Email!);
            await ConfirmarEnvioAlSolicitanteAsync(datos, solicitante, revisor, enviadoRevisorA);

            var nombre = string.IsNullOrWhiteSpace(revisor.Nombre) ? "tu revisor" : revisor.Nombre;
            return $"Rendición {planilla.Codigo} enviada a {nombre} para su primera revisión.";
        }

        public async Task<byte[]> RegenerarPlanilla(int rendicionId, int userId)
        {
            var planilla = await GetDetalle(rendicionId, userId);

            if (!planilla.PuedeSubsanar)
                throw new AbrilException(
                    "Solo se puede volver a generar una rendición observada en primera revisión. " +
                    $"Esta está en «{planilla.EstadoPrimeraRevision}».", 400);

            // La regeneración es de Gestión de Salidas: el PDF cubre la planilla entera (todas sus
            // salidas, de todos sus trabajadores) y ahí vive su armado. Acá solo se valida que sea
            // del trabajador y que esté observada.
            return await _gestionSalidaService.RegenerarPlanilla(rendicionId, userId);
        }

        public Task<ConsolidadoS10Dto> UploadConsolidadoS10(
            int rendicionId, IFormFile file, decimal montoTotal, string numeroGuia, int userId) =>
            // ownerUserId = userId: en el autoservicio la planilla tiene que incluir alguna salida
            // propia. El servicio compartido resuelve el resto (validación del monto contra la
            // planilla, SharePoint, reemplazo, subsanación).
            _consolidadoService.UploadParaRendicion(
                rendicionId, file, montoTotal, numeroGuia, userId, ownerUserId: userId);

        public async Task<string> NotificarRevisor(int rendicionId, int userId)
        {
            // El detalle ya trae el guard de propiedad, el estado del reembolso y el consolidado,
            // así que las validaciones se hacen contra él y no contra consultas sueltas.
            var planilla = await GetDetalle(rendicionId, userId);

            if (planilla.ConsolidadoS10 == null)
                throw new AbrilException(
                    "Primero adjunta el Consolidado del S10: es lo que el revisor tiene que mirar.", 400);

            if (!planilla.PuedeNotificarRevisor)
                throw new AbrilException(
                    "El reembolso de esta planilla ya fue revisado: no hace falta volver a avisar.", 400);

            var quien = await _repo.GetSolicitante(rendicionId, userId)
                ?? throw new AbrilException("No se pudo identificar al trabajador de la planilla.", 409);

            var revisor = await _revisorResolver.ResolveAsync(quien.WorkerId);
            if (string.IsNullOrWhiteSpace(revisor?.Email))
                throw new AbrilException(
                    "No se pudo determinar el correo de tu jefe/revisor. Avisa a Gestión del Talento Humano.", 409);

            var envio = await _correoResolver.ResolveEnvioAsync(
                CorreoEventoCodigos.S10Revisor,
                new List<string> { revisor!.Email! });

            if (!envio.Enviar)
                throw new AbrilException(
                    "El aviso al revisor está desactivado en la configuración de correos de Gestión Administrativa.",
                    409);

            var datos = new ReembolsoPlanillaCorreoDatos
            {
                RendicionId    = rendicionId,
                Trabajador     = quien.Trabajador,
                Area           = quien.Area,
                NumeroPlanilla = planilla.NumeroPlanilla,
                Periodo        = planilla.Periodo,
                SalidasCount   = planilla.SalidasCount,
                MontoTotal     = planilla.MontoTotal,
            };

            // El botón lleva a Gestión de Rendiciones, que es donde el revisor decide, y abre esta
            // planilla: lo que va a mirar (el Consolidado del S10) es del documento, no de una
            // salida suelta. Gestión de Salidas ya no llega hasta el reembolso.
            var url  = SalidaEnlaces.GestionRendiciones(_configuration, rendicionId);
            var body = ReembolsoEmailTemplates.RevisionPendiente(
                SalidaEmailLayout.Desde(_configuration), datos, url);

            var asunto = planilla.NumeroPlanilla is null
                ? $"Reembolso por revisar - {quien.Trabajador} - {planilla.Periodo}"
                : $"Reembolso por revisar - {quien.Trabajador} - planilla {planilla.NumeroPlanilla}";

            await _emailService.SendAsync(
                to: envio.Para,
                subject: asunto,
                body: body,
                isHtml: true,
                cc: envio.Copia.Count > 0 ? envio.Copia : null);

            await _repo.MarcarRevisorNotificado(rendicionId, userId);

            var nombre = string.IsNullOrWhiteSpace(revisor.Nombre) ? "tu revisor" : revisor.Nombre;
            return $"Se le avisó a {nombre}.";
        }


        // ── Corrección con el Coordinador ERP ────────────────────────────────

        public async Task<CorreccionS10Dto> SolicitarCorreccionS10(
            int rendicionId, string motivo, int userId)
        {
            // El detalle ya trae el guard de propiedad y el estado del reembolso; el repositorio
            // vuelve a validar lo mismo al escribir, así que la carrera entre los dos no puede
            // dejar una corrección sobre una planilla que la jefatura acaba de aprobar.
            var planilla = await GetDetalle(rendicionId, userId);

            if (!planilla.PuedeSolicitarCorreccion)
                throw new AbrilException(
                    planilla.CorreccionS10 != null
                        ? "Ya hay una corrección en curso para esta rendición."
                        : planilla.ConsolidadoS10 == null
                            ? "Primero adjunta el Consolidado del S10: es el documento que el ERP tiene que corregir."
                            : "Solo se puede pedir una corrección al ERP cuando la jefatura observó el reembolso.",
                    400);

            // El correo se resuelve ANTES de escribir: una corrección que el ERP nunca ve deja al
            // trabajador esperando algo que no va a pasar. Es la excepción al best-effort del resto
            // de los avisos de esta pantalla, y por eso corta con 409 en vez de seguir.
            var envio = await _correoResolver.ResolveEnvioAsync(
                CorreoEventoCodigos.CorreccionS10Solicitada,
                await _repo.GetCorreosCoordinadorErp());

            if (!envio.Enviar || envio.Para.Count == 0)
                throw new AbrilException(
                    "No hay ningún Coordinador ERP con correo al que enviarle la solicitud. Avisa al "
                    + "administrador del sistema.", 409);

            var correccion = await _repo.CrearCorreccion(rendicionId, motivo, userId);

            var quien = await _repo.GetSolicitante(rendicionId, userId);

            var datos = new CorreccionS10CorreoDatos
            {
                CorreccionId   = correccion.Id,
                RendicionId    = rendicionId,
                Codigo         = planilla.Codigo,
                Trabajador     = quien?.Trabajador ?? "Colaborador",
                Area           = quien?.Area,
                Periodo        = planilla.Periodo,
                NumeroPlanilla = planilla.NumeroPlanilla,
                NumeroGuia     = correccion.NumeroGuia,
                MontoTotal     = planilla.MontoTotalPlanilla,
                Motivo         = correccion.Motivo,
                MotivoJefatura = correccion.MotivoJefatura,
            };

            // El botón abre la bandeja del ERP en esta corrección: es donde marca el check.
            var url  = SalidaEnlaces.CorreccionesS10(_configuration, correccion.Id);
            var body = CorreccionS10EmailTemplates.Solicitada(
                SalidaEmailLayout.Desde(_configuration), datos, url);

            await _emailService.SendAsync(
                to: envio.Para,
                subject: $"Corrección del S10 solicitada - {datos.Trabajador} - {planilla.Codigo}",
                body: body,
                isHtml: true,
                cc: envio.Copia.Count > 0 ? envio.Copia : null);

            return correccion;
        }

        // ── Correos de la primera revisión ───────────────────────────────────

        /// <summary>
        /// Le avisa al jefe/revisor que tiene una rendición esperando su primera revisión, con los
        /// dos botones (aprobar / observar) que lo llevan a la pantalla con la acción planteada.
        ///
        /// Es best-effort: la rendición ya quedó en revisión y no se revierte porque un correo
        /// falle. Devuelve los correos a los que salió realmente —vacío si está apagado en
        /// Configuración o si falló— para que la confirmación al solicitante no diga que se le
        /// avisó a alguien que nunca lo recibió.
        /// </summary>
        private async Task<List<string>> NotificarRevisorPrimeraRevisionAsync(
            RendicionRevisionCorreoDatos datos, string revisorEmail)
        {
            try
            {
                var envio = await _correoResolver.ResolveEnvioAsync(
                    CorreoEventoCodigos.RendicionPrimeraRevision,
                    new List<string> { revisorEmail });

                if (!envio.Enviar)
                {
                    _logger.LogInformation(
                        "Correo {Codigo} no enviado para la rendición {RendicionId}: está apagado o sin destinatarios.",
                        CorreoEventoCodigos.RendicionPrimeraRevision, datos.RendicionId);
                    return new();
                }

                var urlAprobar  = SalidaEnlaces.GestionRendicionesAccion(_configuration, datos.RendicionId, "aprobar");
                var urlObservar = SalidaEnlaces.GestionRendicionesAccion(_configuration, datos.RendicionId, "observar");
                var urlGestion  = SalidaEnlaces.GestionRendiciones(_configuration, datos.RendicionId);

                var body = RendicionRevisionEmailTemplates.PorRevisar(
                    SalidaEmailLayout.Desde(_configuration), datos, urlAprobar, urlObservar, urlGestion);

                await _emailService.SendAsync(
                    to: envio.Para,
                    subject: $"Rendición por revisar - {datos.Codigo} - {datos.Trabajador}",
                    body: body,
                    isHtml: true,
                    cc: envio.Copia.Count > 0 ? envio.Copia : null);

                return envio.Para;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error avisando al revisor la primera revisión de la rendición {RendicionId}",
                    datos.RendicionId);
                return new();
            }
        }

        /// <summary>
        /// Le confirma al solicitante que su rendición quedó registrada y a quién se le envió. Es
        /// informativo y best-effort: no le pide nada y no puede tumbar el envío.
        /// </summary>
        private async Task ConfirmarEnvioAlSolicitanteAsync(
            RendicionRevisionCorreoDatos datos,
            RendicionSolicitanteDto solicitante,
            JefeRevisorResolution revisor,
            IReadOnlyList<string> enviadoRevisorA)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(solicitante.Email))
                {
                    _logger.LogWarning(
                        "Rendición {RendicionId}: el solicitante no tiene correo registrado, no se le confirmó el envío.",
                        datos.RendicionId);
                    return;
                }

                var envio = await _correoResolver.ResolveEnvioAsync(
                    CorreoEventoCodigos.RendicionEnviada,
                    new List<string> { solicitante.Email! });

                if (!envio.Enviar)
                {
                    _logger.LogInformation(
                        "Correo {Codigo} no enviado para la rendición {RendicionId}: está apagado o sin destinatarios.",
                        CorreoEventoCodigos.RendicionEnviada, datos.RendicionId);
                    return;
                }

                var url  = SalidaEnlaces.Rendiciones(_configuration, datos.RendicionId);
                var body = RendicionRevisionEmailTemplates.EnRevision(
                    SalidaEmailLayout.Desde(_configuration), datos, url, enviadoRevisorA, revisor.Nombre);

                await _emailService.SendAsync(
                    to: envio.Para,
                    subject: $"Tu rendición {datos.Codigo} está en revisión",
                    body: body,
                    isHtml: true,
                    cc: envio.Copia.Count > 0 ? envio.Copia : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error confirmándole al solicitante el envío de la rendición {RendicionId}",
                    datos.RendicionId);
            }
        }

    }
}
