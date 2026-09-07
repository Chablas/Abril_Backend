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

        public async Task<RendicionFilterDataDto> GetFilterData(int userId) => new()
        {
            Periodos = await _repo.GetPeriodos(userId),
        };

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

        public Task<ConsolidadoS10Dto> UploadConsolidadoS10(int rendicionId, IFormFile file, int userId) =>
            // ownerUserId = userId: en el autoservicio la planilla tiene que incluir alguna salida
            // propia. El servicio compartido resuelve el resto (SharePoint, reemplazo, subsanación).
            _consolidadoService.UploadParaRendicion(rendicionId, file, userId, ownerUserId: userId);

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
