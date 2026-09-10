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
        public async Task<List<CorreoAvisoPreviewDto>> GetCorreoPreviewPago(
            ReembolsoSeleccionDto dto)
        {
            try
            {
                var ids = await _repo.ResolverSolicitudIds(
                    dto.RendicionIds, dto.SolicitudIds, EstadosSalida.Reembolso.PorPagar);
                if (ids.Count == 0) return new();

                var datos = await _repo.GetPagoCorreoInfo(ids);
                var principal = datos
                    .Select(d => d.TrabajadorEmail)
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => e!)
                    .Distinct()
                    .ToList();

                var envio = await _correoResolver.ResolveEnvioAsync(
                    CorreoEventoCodigos.ReembolsoPagado, principal);

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
                _logger.LogError(ex, "Error resolviendo el preview del correo de pago");
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

            try
            {
                var datos  = await _repo.GetPagoCorreoInfo(solicitudIds);
                var layout = SalidaEmailLayout.Desde(_configuration);

                foreach (var d in datos)
                {
                    if (string.IsNullOrWhiteSpace(d.TrabajadorEmail))
                    {
                        _logger.LogWarning(
                            "Rendición {RendicionId}: el colaborador no tiene correo registrado, no se avisó el pago.",
                            d.RendicionId);
                        continue;
                    }

                    var envio = await _correoResolver.ResolveEnvioAsync(
                        CorreoEventoCodigos.ReembolsoPagado, new List<string> { d.TrabajadorEmail! });

                    if (!envio.Enviar)
                    {
                        _logger.LogInformation(
                            "Correo {Codigo} no enviado para la rendición {RendicionId}: está apagado o sin destinatarios.",
                            CorreoEventoCodigos.ReembolsoPagado, d.RendicionId);
                        continue;
                    }

                    // El botón lleva a Mis Rendiciones: ahí vive el expediente completo del
                    // trabajador (planilla firmada, Consolidado del S10 y el estado final).
                    var url = SalidaEnlaces.Rendiciones(_configuration, d.RendicionId);

                    await _emailService.SendAsync(
                        to: envio.Para,
                        subject: $"Reembolso realizado - rendición {d.Codigo}",
                        body: ReembolsoEmailTemplates.Pagado(layout, d, url),
                        isHtml: true,
                        cc: envio.Copia.Count > 0 ? envio.Copia : null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error avisando el pago de los reembolsos {Ids}", string.Join(",", solicitudIds));
            }
        }
    }
}
