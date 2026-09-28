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

        public async Task<ReembolsoDetalleDto> GetDetalle(int consolidadoId)
        {
            return await _repo.GetDetalle(consolidadoId)
                ?? throw new AbrilException(
                    "El Consolidado del S10 no existe o todavía no está firmado por la jefatura.", 404);
        }

        public async Task<SolicitudSalidaDetalleDto> GetSalidaDetalle(int solicitudId)
        {
            return await _repo.GetSalidaDetalle(solicitudId)
                ?? throw new AbrilException(
                    "La salida no existe o todavía no llegó a Tesorería.", 404);
        }

        public async Task<ReembolsoSeguimientoDto> GetSeguimiento(ReembolsoFiltersDto filters)
        {
            return await _repo.GetSeguimiento(filters);
        }

        public async Task<ReembolsoBulkResultDto> ConfirmarRevision(
            ReembolsoSeleccionDto dto, int tesoreroUserId)
        {
            var ids = await _repo.ResolverSolicitudIds(
                dto.ConsolidadoIds, EstadosSalida.Reembolso.Firmado);

            if (ids.Count == 0)
                throw new AbrilException(
                    "No hay salidas firmadas por revisar en la selección: la revisión de Tesorería se "
                    + "confirma sobre lo que la jefatura ya firmó.", 400);

            var confirmadas = await _repo.ConfirmarRevision(ids, tesoreroUserId);

            // El aviso es para Tesorería misma (plantilla 21): el consolidado ya se puede pagar. El
            // colaborador sigue enterándose recién con el pago.
            await NotificarPorPagarAsync(confirmadas);

            return new ReembolsoBulkResultDto
            {
                Procesadas = confirmadas.Count,
                Message    = $"{confirmadas.Count} reembolso(s) listo(s) para pagar.",
            };
        }

        /// <summary>
        /// Tesorería devuelve el consolidado con un motivo obligatorio (RG-49), y solo mientras no
        /// haya confirmado la revisión: lo confirmado sigue al pago
        /// (<see cref="EstadosSalida.Reembolso.ObservablesPorTesoreria"/>). No hay un "flujo de
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
                dto.ConsolidadoIds, EstadosSalida.Reembolso.ObservablesPorTesoreria);

            if (ids.Count == 0)
                throw new AbrilException(
                    "No hay reembolsos que observar en la selección: solo se devuelve lo que está "
                    + "firmado y todavía sin la revisión confirmada. Lo confirmado o pagado no vuelve.", 400);

            var observadas = await _repo.Observar(ids, dto.Observacion!, tesoreroUserId);

            await NotificarObservacionAsync(observadas);

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
                dto.ConsolidadoIds, EstadosSalida.Reembolso.PorPagar);

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
        /// A quién le llegaría el aviso de que la revisión quedó confirmada: a Tesorería, resuelta
        /// por el rol TESORERO (no por la categoría del puesto) más lo que se agregue en Reembolsos →
        /// Configuración → Correos. Se pregunta sobre las salidas firmadas de la selección, que son
        /// las que de verdad se van a confirmar: sin ninguna no se confirma nada y no sale correo.
        /// </summary>
        public Task<List<CorreoAvisoPreviewDto>> GetCorreoPreviewConfirmacion(ReembolsoSeleccionDto dto) =>
            PreviewAsync(
                dto,
                new[] { EstadosSalida.Reembolso.Firmado },
                CorreoEventoCodigos.TesoreriaPorPagar,
                "A Tesorería",
                async _ => await _repo.GetCorreosTesoreria(),
                "preview del correo de revisión confirmada");

        /// <summary>
        /// A quién le llegarían los avisos de pago si se marcan como pagados los consolidados
        /// seleccionados: a cada colaborador lo suyo y al consolidador de cada consolidado lo que
        /// Tesorería le abona. Se resuelve con los MISMOS datos que usa el envío
        /// (<see cref="NotificarPagoAsync"/>) sobre las salidas que de verdad se van a pagar —las
        /// que ya pasaron la revisión de Tesorería—, así que la confirmación no promete un aviso
        /// que la configuración dejó fuera ni nombra a alguien a quien el pago no va a tocar.
        ///
        /// Best-effort: ante un error devuelve una lista vacía y la confirmación sale sin correos.
        /// </summary>
        public async Task<List<CorreoAvisoPreviewDto>> GetCorreoPreviewPago(ReembolsoSeleccionDto dto)
        {
            try
            {
                var ids = await _repo.ResolverSolicitudIds(dto.ConsolidadoIds, EstadosSalida.Reembolso.PorPagar);
                if (ids.Count == 0) return new();

                var info = await _repo.GetPagoCorreoInfo(ids);

                var avisos = new List<CorreoAvisoPreviewDto?>
                {
                    await AvisoAsync(CorreoEventoCodigos.ReembolsoPagadoConsolidador, "Al consolidador",
                        info.Consolidados.Select(c => c.ConsolidadorEmail)),
                    await AvisoAsync(CorreoEventoCodigos.ReembolsoPagado, "Al colaborador",
                        info.Personas.Select(p => p.TrabajadorEmail)),
                };

                return avisos.OfType<CorreoAvisoPreviewDto>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolviendo el preview de los correos de pago");
                return new();
            }
        }

        /// <summary>
        /// A quién le llegaría el aviso de que Tesorería devolvió lo seleccionado: al consolidador de
        /// cada consolidado, que es quien lo subsana. Mismo criterio que el de pago: se resuelve
        /// sobre las salidas que de verdad se van a observar.
        /// </summary>
        public Task<List<CorreoAvisoPreviewDto>> GetCorreoPreviewObservacion(ReembolsoSeleccionDto dto) =>
            PreviewAsync(
                dto,
                EstadosSalida.Reembolso.ObservablesPorTesoreria,
                CorreoEventoCodigos.ReembolsoObservadoTesoreria,
                "Al consolidador",
                async ids => (await _repo.GetConsolidadoCorreoDatos(ids)).Select(d => d.ConsolidadorEmail),
                "preview del correo de observación");

        private async Task<List<CorreoAvisoPreviewDto>> PreviewAsync(
            ReembolsoSeleccionDto dto, int[] estados, string eventoCodigo, string etiqueta,
            Func<List<int>, Task<IEnumerable<string?>>> destinatarios, string queSeEstabaHaciendo)
        {
            try
            {
                var ids = await _repo.ResolverSolicitudIds(dto.ConsolidadoIds, estados);
                if (ids.Count == 0) return new();

                var aviso = await AvisoAsync(eventoCodigo, etiqueta, await destinatarios(ids));
                return aviso == null ? new() : new List<CorreoAvisoPreviewDto> { aviso };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolviendo el {Que}", queSeEstabaHaciendo);
                return new();
            }
        }

        /// <summary>
        /// Un correo del preview: sus destinatarios principales, sin repetir, pasados por
        /// Configuración → Correos con la misma llamada que hace el envío. Null si no sale.
        /// </summary>
        private async Task<CorreoAvisoPreviewDto?> AvisoAsync(
            string eventoCodigo, string etiqueta, IEnumerable<string?> destinatarios)
        {
            var principal = destinatarios
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var envio = await _correoResolver.ResolveEnvioAsync(eventoCodigo, principal);

            if (!envio.Enviar || envio.Para.Count == 0) return null;

            return new CorreoAvisoPreviewDto
            {
                Etiqueta = etiqueta,
                Para     = envio.Para,
                Copia    = envio.Copia,
            };
        }

        // ── Aviso a Tesorería ────────────────────────────────────────────────

        /// <summary>
        /// Avisa a Tesorería que el consolidado quedó en «Proceder con el reembolso», listo para
        /// programar el pago (plantilla 21). Va UNO por consolidado —lo que se paga es el documento—
        /// con lo que ese consolidado tiene por pagar. El destinatario principal es el rol TESORERO,
        /// igual que en el aviso de consolidado firmado, y los de Reembolsos → Configuración →
        /// Correos se suman como copia.
        ///
        /// Es best-effort, como el resto de los avisos del ciclo: la revisión ya quedó confirmada y
        /// no se revierte porque un correo falle.
        /// </summary>
        private async Task NotificarPorPagarAsync(List<int> solicitudIds)
        {
            if (solicitudIds.Count == 0) return;

            try
            {
                var info = await _repo.GetPorPagarCorreoInfo(solicitudIds);
                if (info.Consolidados.Count == 0) return;

                // El destinatario no depende del consolidado: se resuelve una sola vez para el lote.
                var envio = await _correoResolver.ResolveEnvioAsync(
                    CorreoEventoCodigos.TesoreriaPorPagar, info.Destinatarios);

                if (!envio.Enviar)
                {
                    _logger.LogInformation(
                        "Correo {Codigo} no enviado para los consolidados {Ids}: está apagado, sin "
                        + "destinatarios configurados o sin nadie con el rol de Tesorería.",
                        CorreoEventoCodigos.TesoreriaPorPagar,
                        string.Join(",", info.Consolidados.Select(c => c.ConsolidadoId)));
                    return;
                }

                var layout = SalidaEmailLayout.Desde(_configuration);

                foreach (var d in info.Consolidados)
                {
                    // El botón abre el consolidado en Reembolsos, que es donde se marca como pagado.
                    var url = SalidaEnlaces.Reembolsos(_configuration, d.ConsolidadoId);

                    await _emailService.SendAsync(
                        to: envio.Para,
                        subject: "El consolidado está listo para programación de pago"
                                 + ReembolsoEmailTemplates.NombreEnAsunto(d.Codigo, d.NumeroReembolso),
                        body: ReembolsoEmailTemplates.ConsolidadoListoParaPago(layout, d, url),
                        isHtml: true,
                        cc: envio.Copia.Count > 0 ? envio.Copia : null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error avisando a Tesorería la revisión confirmada de las salidas {Ids}",
                    string.Join(",", solicitudIds));
            }
        }

        // ── Correos de cierre ────────────────────────────────────────────────

        /// <summary>
        /// Los dos avisos del pago, armados con los mismos datos: Tesorería le abona el consolidado
        /// al consolidador y él le reembolsa a cada trabajador, así que le avisa a él cuánto recibe
        /// y cuánto le toca a cada uno (<see cref="NotificarConsolidadoresAsync"/>), y a cada
        /// colaborador lo suyo (<see cref="NotificarColaboradoresAsync"/>).
        ///
        /// Es best-effort: el pago ya está registrado y no se revierte porque un correo falle
        /// (mismo criterio que la decisión del reembolso). Cada aviso va por su lado: si uno
        /// falla, el otro sale igual.
        /// </summary>
        private async Task NotificarPagoAsync(List<int> solicitudIds)
        {
            if (solicitudIds.Count == 0) return;

            ReembolsoPagoCorreoInfoDto info;
            try
            {
                info = await _repo.GetPagoCorreoInfo(solicitudIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error armando los avisos del pago de los reembolsos {Ids}",
                    string.Join(",", solicitudIds));
                return;
            }

            var layout = SalidaEmailLayout.Desde(_configuration);

            await NotificarConsolidadoresAsync(info.Consolidados, layout);
            await NotificarColaboradoresAsync(info.Personas, layout);
        }

        /// <summary>
        /// Avisa al consolidador que Tesorería pagó su consolidado (plantilla 22): el monto que le
        /// abonó y lo que le toca reembolsar a cada trabajador, con el código del consolidado y el
        /// de la planilla grupal. Va UNO por consolidado y solo a quien lo adjuntó; el botón lo
        /// abre en Consolidados, que es donde el consolidador sigue lo que adjuntó.
        /// </summary>
        private async Task NotificarConsolidadoresAsync(
            List<ConsolidadoPagadoCorreoDatos> consolidados, SalidaEmailLayout layout)
        {
            try
            {
                foreach (var d in consolidados)
                {
                    if (string.IsNullOrWhiteSpace(d.ConsolidadorEmail))
                    {
                        _logger.LogWarning(
                            "Consolidado {ConsolidadoId}: quien lo adjuntó no tiene correo registrado, no se avisó el pago.",
                            d.ConsolidadoId);
                        continue;
                    }

                    var envio = await _correoResolver.ResolveEnvioAsync(
                        CorreoEventoCodigos.ReembolsoPagadoConsolidador, new List<string> { d.ConsolidadorEmail });

                    if (!envio.Enviar)
                    {
                        _logger.LogInformation(
                            "Correo {Codigo} no enviado para el consolidado {ConsolidadoId}: está apagado o sin destinatarios.",
                            CorreoEventoCodigos.ReembolsoPagadoConsolidador, d.ConsolidadoId);
                        continue;
                    }

                    var url = SalidaEnlaces.Consolidados(_configuration, d.ConsolidadoId);

                    await _emailService.SendAsync(
                        to: envio.Para,
                        subject: "El consolidado fue pagado"
                                 + ReembolsoEmailTemplates.NombreEnAsunto(d.Codigo, d.NumeroReembolso),
                        body: ReembolsoEmailTemplates.ConsolidadoPagado(layout, d, url),
                        isHtml: true,
                        cc: envio.Copia.Count > 0 ? envio.Copia : null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error avisando a los consolidadores el pago de los consolidados {Ids}",
                    string.Join(",", consolidados.Select(c => c.ConsolidadoId)));
            }
        }

        /// <summary>
        /// Avisa a cada colaborador que su reembolso ya se pagó (RG-28). Va UN correo por persona
        /// con todo lo que se le pagó —el monto sumado y sus rendiciones una por una—, no uno por
        /// planilla ni por salida: quien tenía tres rendiciones en el consolidado recibía tres
        /// correos del mismo pago. El botón lo lleva a Mis Rendiciones, que es donde sigue sus
        /// planillas: a la rendición si es una sola, a la lista si son varias.
        /// </summary>
        private async Task NotificarColaboradoresAsync(
            List<ReembolsoPagadoCorreoDatos> personas, SalidaEmailLayout layout)
        {
            try
            {
                foreach (var d in personas)
                {
                    var codigos = string.Join(", ", d.Rendiciones.Select(r => r.Codigo));

                    if (string.IsNullOrWhiteSpace(d.TrabajadorEmail))
                    {
                        _logger.LogWarning(
                            "Rendiciones {Rendiciones}: el colaborador no tiene correo registrado, no se envió {Codigo}.",
                            codigos, CorreoEventoCodigos.ReembolsoPagado);
                        continue;
                    }

                    var envio = await _correoResolver.ResolveEnvioAsync(
                        CorreoEventoCodigos.ReembolsoPagado, new List<string> { d.TrabajadorEmail! });

                    if (!envio.Enviar)
                    {
                        _logger.LogInformation(
                            "Correo {Codigo} no enviado para las rendiciones {Rendiciones}: está apagado o sin destinatarios.",
                            CorreoEventoCodigos.ReembolsoPagado, codigos);
                        continue;
                    }

                    var una = d.Rendiciones.Count == 1;
                    var url = una
                        ? SalidaEnlaces.Rendiciones(_configuration, d.Rendiciones[0].RendicionId)
                        : SalidaEnlaces.Rendiciones(_configuration);

                    await _emailService.SendAsync(
                        to: envio.Para,
                        subject: una
                            ? $"Reembolso realizado - rendición {d.Rendiciones[0].Codigo}"
                            : $"Reembolso realizado - {d.Rendiciones.Count} rendiciones",
                        body: ReembolsoEmailTemplates.Pagado(layout, d, url),
                        isHtml: true,
                        cc: envio.Copia.Count > 0 ? envio.Copia : null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error avisando a los colaboradores el pago de las rendiciones {Rendiciones}",
                    string.Join(", ", personas.SelectMany(p => p.Rendiciones).Select(r => r.Codigo)));
            }
        }

        /// <summary>
        /// Avisa al consolidador que Tesorería le devolvió el consolidado, con el motivo y los dos
        /// caminos para subsanar: desde la primera revisión el trámite del S10 es suyo, no del
        /// trabajador. Va UN correo por consolidado. Va por su propio evento y no por el de la
        /// jefatura (REEMBOLSO_RECHAZADO) porque se origina en otra pantalla y se administra desde ahí.
        ///
        /// Es best-effort, igual que el resto de los avisos del ciclo: la observación ya está
        /// escrita y no se revierte porque un correo falle. El consolidado queda visible como
        /// Observado aunque el correo no salga.
        /// </summary>
        private async Task NotificarObservacionAsync(List<int> solicitudIds)
        {
            if (solicitudIds.Count == 0) return;

            try
            {
                var layout = SalidaEmailLayout.Desde(_configuration);

                foreach (var d in await _repo.GetConsolidadoCorreoDatos(solicitudIds))
                {
                    if (string.IsNullOrWhiteSpace(d.ConsolidadorEmail))
                    {
                        _logger.LogWarning(
                            "Consolidado {ConsolidadoId}: quien lo adjuntó no tiene correo registrado, no se avisó la observación de Tesorería.",
                            d.ConsolidadoId);
                        continue;
                    }

                    var envio = await _correoResolver.ResolveEnvioAsync(
                        CorreoEventoCodigos.ReembolsoObservadoTesoreria, new List<string> { d.ConsolidadorEmail });

                    if (!envio.Enviar)
                    {
                        _logger.LogInformation(
                            "Correo {Codigo} no enviado para el consolidado {ConsolidadoId}: está apagado o sin destinatarios.",
                            CorreoEventoCodigos.ReembolsoObservadoTesoreria, d.ConsolidadoId);
                        return;
                    }

                    var url = SalidaEnlaces.Consolidados(_configuration, d.ConsolidadoId);

                    await _emailService.SendAsync(
                        to: envio.Para,
                        subject: $"Reembolso observado por Tesorería{ReembolsoEmailTemplates.NombreEnAsunto(d.Codigo, d.NumeroReembolso)}",
                        body: ReembolsoEmailTemplates.ConsolidadoObservadoPorTesoreria(layout, d, url),
                        isHtml: true,
                        cc: envio.Copia.Count > 0 ? envio.Copia : null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error avisando la observación de Tesorería de las salidas {Ids}",
                    string.Join(",", solicitudIds));
            }
        }
    }
}
