using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Services
{
    public class ReembolsoService : IReembolsoService
    {
        private readonly IReembolsoRepository           _repo;
        private readonly ICorreoSalidaRecipientResolver _correoResolver;
        private readonly IEmailService                  _emailService;
        private readonly IConfiguration                 _configuration;
        private readonly ILogger<ReembolsoService>      _logger;

        public ReembolsoService(
            IReembolsoRepository repo,
            ICorreoSalidaRecipientResolver correoResolver,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<ReembolsoService> logger)
        {
            _repo               = repo;
            _correoResolver     = correoResolver;
            _emailService       = emailService;
            _configuration      = configuration;
            _logger             = logger;
        }

        public async Task<ReembolsoListResultDto> GetAll(ReembolsoFiltersDto filters)
        {
            var data = await _repo.GetAll(filters);
            return new ReembolsoListResultDto
            {
                Data    = data,
                Resumen = ResumenReembolsosDto.De(data),
            };
        }

        public async Task<ReembolsoFilterDataDto> GetFilterData()
        {
            return await _repo.GetFilterData();
        }

        public async Task<ReembolsoDetalleDto> GetDetalle(int rendicionId)
        {
            return await _repo.GetDetalle(rendicionId)
                ?? throw new AbrilException(
                    "La planilla no existe o todavía no está firmada por la jefatura.", 404);
        }

        public async Task<ReembolsoSeguimientoDto> GetSeguimiento(ReembolsoFiltersDto filters)
        {
            return await _repo.GetSeguimiento(filters);
        }

        public async Task<ReembolsoBulkResultDto> ConfirmarRevision(
            ReembolsoSeleccionDto dto, int tesoreroUserId)
        {
            var ids = await _repo.ResolverSolicitudIds(
                dto.RendicionIds, dto.SolicitudIds, EstadosSalida.Reembolso.Firmado);

            if (ids.Count == 0)
                throw new AbrilException(
                    "No hay salidas firmadas por revisar en la selección: la revisión de Tesorería se "
                    + "confirma sobre lo que la jefatura ya firmó.", 400);

            var confirmadas = await _repo.ConfirmarRevision(ids, tesoreroUserId);

            // Confirmar la revisión no avisa a nadie: es un paso interno de Tesorería y el
            // colaborador se entera recién con el pago.
            return new ReembolsoBulkResultDto
            {
                Procesadas = confirmadas.Count,
                Message    = $"{confirmadas.Count} reembolso(s) listo(s) para pagar.",
            };
        }

        /// <summary>
        /// Tesorería devuelve el consolidado con un motivo obligatorio (RG-49). No hay un "flujo de
        /// subsanación de Tesorería" aparte: la planilla vuelve al MISMO camino que ya existe para
        /// la observación de la jefatura (§10.4-10.6) —queda Observada y el consolidador elige
        /// entre recargar el Consolidado del S10 o pedirle la corrección al Coordinador ERP—, con
        /// la única diferencia de que el origen queda marcado.
        ///
        /// Por eso Tesorería NO le escribe al ERP: el pedido al ERP lleva el «MOTIVO *» del
        /// consolidador (RG-21), que es quien sabe qué hay que tocar en el S10.
        /// </summary>
        public async Task<ReembolsoBulkResultDto> Observar(
            ReembolsoObservacionDto dto, int tesoreroUserId)
        {
            if (string.IsNullOrWhiteSpace(dto.Observacion))
                throw new AbrilException("Para observar un reembolso hay que escribir el motivo.", 400);

            var ids = await _repo.ResolverSolicitudIds(
                dto.RendicionIds, dto.SolicitudIds, EstadosSalida.Reembolso.ObservablesPorTesoreria);

            if (ids.Count == 0)
                throw new AbrilException(
                    "No hay reembolsos que observar en la selección: solo se devuelve lo que está "
                    + "firmado o listo para pagar. Lo ya pagado no vuelve.", 400);

            var observadas = await _repo.Observar(ids, dto.Observacion!, tesoreroUserId);

            await NotificarObservacionAsync(observadas, dto.Observacion!);

            return new ReembolsoBulkResultDto
            {
                Procesadas = observadas.Count,
                Message    = $"{observadas.Count} reembolso(s) observado(s) y devuelto(s) para subsanar.",
            };
        }

        public async Task<ReembolsoBulkResultDto> MarcarPagadas(
            ReembolsoSeleccionDto dto, int tesoreroUserId)
        {
            var ids = await _repo.ResolverSolicitudIds(
                dto.RendicionIds, dto.SolicitudIds, EstadosSalida.Reembolso.PorPagar);

            if (ids.Count == 0)
                throw new AbrilException(
                    "No hay reembolsos listos para pagar en la selección: primero hay que confirmar la "
                    + "revisión de Tesorería.", 400);

            var pagadas = await _repo.MarcarPagadas(ids, tesoreroUserId);

            await NotificarPagoAsync(pagadas);

            return new ReembolsoBulkResultDto
            {
                Procesadas = pagadas.Count,
                Message    = $"{pagadas.Count} reembolso(s) marcado(s) como pagado(s).",
            };
        }

        /// <summary>
        /// A quién le llegaría el aviso de pago si se marcan como pagadas las planillas
        /// seleccionadas. Se resuelve con la MISMA llamada que hace el envío
        /// (<see cref="NotificarPagoAsync"/>) sobre las salidas que de verdad se van a pagar —las
        /// que ya pasaron la revisión de Tesorería—, así que la confirmación no promete un aviso
        /// que la configuración dejó fuera ni nombra a alguien a quien el pago no va a tocar.
        ///
        /// Confirmar la revisión no manda ningún correo, así que no tiene preview: el único correo
        /// de esta pantalla es el del pago.
        ///
        /// Best-effort: ante un error devuelve una lista vacía y la confirmación sale sin correos.
        /// </summary>
        public Task<List<CorreoAvisoPreviewDto>> GetCorreoPreviewPago(ReembolsoSeleccionDto dto) =>
            PreviewAsync(
                dto,
                new[] { EstadosSalida.Reembolso.PorPagar },
                CorreoEventoCodigos.ReembolsoPagado,
                "preview del correo de pago");

        /// <summary>
        /// A quién le llegaría el aviso de que Tesorería devolvió lo seleccionado. Mismo criterio
        /// que el de pago: se resuelve sobre las salidas que de verdad se van a observar.
        /// </summary>
        public Task<List<CorreoAvisoPreviewDto>> GetCorreoPreviewObservacion(ReembolsoSeleccionDto dto) =>
            PreviewAsync(
                dto,
                EstadosSalida.Reembolso.ObservablesPorTesoreria,
                CorreoEventoCodigos.ReembolsoObservadoTesoreria,
                "preview del correo de observación");

        private async Task<List<CorreoAvisoPreviewDto>> PreviewAsync(
            ReembolsoSeleccionDto dto, int[] estados, string eventoCodigo, string queSeEstabaHaciendo)
        {
            try
            {
                var ids = await _repo.ResolverSolicitudIds(dto.RendicionIds, dto.SolicitudIds, estados);
                if (ids.Count == 0) return new();

                var datos = await _repo.GetPlanillaCorreoInfo(ids);
                var principal = datos
                    .Select(d => d.TrabajadorEmail)
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => e!)
                    .Distinct()
                    .ToList();

                var envio = await _correoResolver.ResolveEnvioAsync(eventoCodigo, principal);

                if (!envio.Enviar || envio.Para.Count == 0) return new();

                return new List<CorreoAvisoPreviewDto>
                {
                    new()
                    {
                        Etiqueta = "Al colaborador",
                        Para     = envio.Para,
                        Copia    = envio.Copia,
                    },
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolviendo el {Que}", queSeEstabaHaciendo);
                return new();
            }
        }

        // ── Correo de cierre ─────────────────────────────────────────────────

        /// <summary>
        /// Avisa a cada colaborador que su reembolso ya se pagó (RG-28). Va UN correo por planilla
        /// y trabajador, no uno por salida: una planilla puede traer diez salidas de la misma
        /// persona y el aviso es del desembolso, que es uno solo.
        ///
        /// Es best-effort: el pago ya está registrado y no se revierte porque un correo falle
        /// (mismo criterio que la decisión del reembolso).
        /// </summary>
        private async Task NotificarPagoAsync(List<int> solicitudIds)
        {
            if (solicitudIds.Count == 0) return;

            await NotificarAsync(
                solicitudIds,
                CorreoEventoCodigos.ReembolsoPagado,
                d => $"Reembolso realizado - rendición {d.Codigo}",
                (layout, d, url) => ReembolsoEmailTemplates.Pagado(layout, d, url),
                "el pago de los reembolsos");
        }

        /// <summary>
        /// Avisa a cada colaborador que Tesorería le devolvió el reembolso, con el motivo y los dos
        /// caminos para subsanar. Va por su propio evento y no por el de la jefatura
        /// (REEMBOLSO_RECHAZADO) porque se origina en otra pantalla y se administra desde ahí.
        ///
        /// Es best-effort, igual que el resto de los avisos del ciclo: la observación ya está
        /// escrita y no se revierte porque un correo falle. La planilla queda visible como
        /// Observada en las tres pantallas aunque el correo no salga.
        /// </summary>
        private async Task NotificarObservacionAsync(List<int> solicitudIds, string observacion)
        {
            await NotificarAsync(
                solicitudIds,
                CorreoEventoCodigos.ReembolsoObservadoTesoreria,
                d => $"Reembolso observado por Tesorería - rendición {d.Codigo}",
                (layout, d, url) => ReembolsoEmailTemplates.ObservadoPorTesoreria(
                    layout, d, observacion, url),
                "la observación de los reembolsos");
        }

        /// <summary>
        /// El envío que comparten los dos correos de la pantalla: UNO por (planilla, trabajador),
        /// nunca uno por salida —una planilla puede traer diez salidas de la misma persona y lo que
        /// se avisa es del documento—, con el botón apuntando a Mis Rendiciones, que es donde vive
        /// el expediente completo del trabajador.
        /// </summary>
        private async Task NotificarAsync(
            List<int> solicitudIds,
            string eventoCodigo,
            Func<ReembolsoPlanillaCorreoDatos, string> asunto,
            Func<SalidaEmailLayout, ReembolsoPlanillaCorreoDatos, string, string> cuerpo,
            string queSeEstabaAvisando)
        {
            if (solicitudIds.Count == 0) return;

            try
            {
                var datos  = await _repo.GetPlanillaCorreoInfo(solicitudIds);
                var layout = SalidaEmailLayout.Desde(_configuration);

                foreach (var d in datos)
                {
                    if (string.IsNullOrWhiteSpace(d.TrabajadorEmail))
                    {
                        _logger.LogWarning(
                            "Rendición {RendicionId}: el colaborador no tiene correo registrado, no se envió {Codigo}.",
                            d.RendicionId, eventoCodigo);
                        continue;
                    }

                    var envio = await _correoResolver.ResolveEnvioAsync(
                        eventoCodigo, new List<string> { d.TrabajadorEmail! });

                    if (!envio.Enviar)
                    {
                        _logger.LogInformation(
                            "Correo {Codigo} no enviado para la rendición {RendicionId}: está apagado o sin destinatarios.",
                            eventoCodigo, d.RendicionId);
                        continue;
                    }

                    var url = SalidaEnlaces.Rendiciones(_configuration, d.RendicionId);

                    await _emailService.SendAsync(
                        to: envio.Para,
                        subject: asunto(d),
                        body: cuerpo(layout, d, url),
                        isHtml: true,
                        cc: envio.Copia.Count > 0 ? envio.Copia : null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error avisando {Que} {Ids}",
                    queSeEstabaAvisando, string.Join(",", solicitudIds));
            }
        }
    }
}
