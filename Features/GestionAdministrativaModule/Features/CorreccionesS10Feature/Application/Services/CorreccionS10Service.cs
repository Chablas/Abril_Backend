using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Application.Services
{
    public class CorreccionS10Service : ICorreccionS10Service
    {
        private readonly ICorreccionS10Repository        _repo;
        private readonly ICorreoSalidaRecipientResolver  _correoResolver;
        private readonly IEmailService                   _emailService;
        private readonly IConfiguration                  _configuration;
        private readonly ILogger<CorreccionS10Service>   _logger;

        public CorreccionS10Service(
            ICorreccionS10Repository repo,
            ICorreoSalidaRecipientResolver correoResolver,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<CorreccionS10Service> logger)
        {
            _repo           = repo;
            _correoResolver = correoResolver;
            _emailService   = emailService;
            _configuration  = configuration;
            _logger         = logger;
        }

        public async Task<CorreccionS10ListResultDto> GetAll(CorreccionS10FiltersDto filters)
        {
            // Las tarjetas se cuentan sobre lo mismo que muestra la tabla, así que salen de esta
            // lista: el listado no está paginado, ya viene entero.
            var data = await _repo.GetAll(filters);
            return new CorreccionS10ListResultDto
            {
                Data    = data,
                Resumen = ResumenCorreccionesS10Dto.De(data),
            };
        }

        public Task<CorreccionS10FilterDataDto> GetFilterData() => _repo.GetFilterData();

        public async Task<CorreccionS10ListItemDto> GetDetalle(int correccionId) =>
            await _repo.GetDetalle(correccionId)
                ?? throw new AbrilException(
                    "La solicitud de corrección no existe o ya se cerró (el consolidador recargó el "
                    + "Consolidado del S10).", 404);

        public async Task<List<CorreoAvisoPreviewDto>> GetCorreoPreview(List<int> correccionIds)
        {
            try
            {
                var destinatarios = await _repo.GetCorreosSolicitantes(correccionIds);

                var envio = await _correoResolver.ResolveEnvioAsync(
                    CorreoEventoCodigos.CorreccionS10Atendida, destinatarios);

                if (!envio.Enviar || envio.Para.Count == 0) return new();

                return new List<CorreoAvisoPreviewDto>
                {
                    new()
                    {
                        Etiqueta = "Al consolidador",
                        Para     = envio.Para,
                        Copia    = envio.Copia,
                    },
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolviendo el preview del aviso de atención del ERP");
                return new();
            }
        }

        public async Task<CorreccionS10BulkResultDto> Atender(
            AtenderCorreccionS10BulkDto accion, int erpUserId)
        {
            if (accion.CorreccionIds.Count == 0)
                throw new AbrilException("Selecciona al menos una corrección.", 400);

            var atendidas = await _repo.Atender(
                accion.CorreccionIds, accion.ComentarioAtencion, erpUserId);

            // El aviso al consolidador es best-effort: la confirmación ya está guardada y no se
            // revierte porque un correo falle — la ve igual en Consolidados (mismo criterio que las
            // decisiones de la jefatura).
            await NotificarAtencionAsync(atendidas);

            return new CorreccionS10BulkResultDto
            {
                Procesadas = atendidas.Count,
                Message = atendidas.Count == 1
                    ? "Corrección marcada como atendida. Le avisamos al consolidador para que recargue el Consolidado del S10."
                    : $"{atendidas.Count} correcciones marcadas como atendidas.",
            };
        }

        /// <summary>
        /// Le avisa al consolidador que pidió la corrección que el ERP ya corrigió en el S10 y puede
        /// recargar el Consolidado (RF-OBS-08). Va UN correo por pedido (mismo consolidado y mismo
        /// consolidador), aunque la bandeja lo muestre por planilla. Respeta la configuración de
        /// destinatarios y no tumba la acción si falla.
        /// </summary>
        private async Task NotificarAtencionAsync(List<int> correccionIds)
        {
            if (correccionIds.Count == 0) return;

            try
            {
                var layout = SalidaEmailLayout.Desde(_configuration);

                foreach (var datos in await _repo.GetCorreoDatosAtendidas(correccionIds))
                {
                    var principal = string.IsNullOrWhiteSpace(datos.SolicitadaPorEmail)
                        ? new List<string>()
                        : new List<string> { datos.SolicitadaPorEmail! };

                    var envio = await _correoResolver.ResolveEnvioAsync(
                        CorreoEventoCodigos.CorreccionS10Atendida, principal);

                    if (!envio.Enviar || envio.Para.Count == 0) continue;

                    // El botón lleva a Gestión de Rendiciones, que es donde el consolidador recarga
                    // el Consolidado del S10: es el paso que esta confirmación acaba de habilitar.
                    var url  = SalidaEnlaces.GestionRendiciones(_configuration, datos.RendicionId);
                    var body = CorreccionS10EmailTemplates.Atendida(layout, datos, url);

                    await _emailService.SendAsync(
                        to: envio.Para,
                        subject: $"Corrección del S10 atendida - {datos.Codigo}",
                        body: body,
                        isHtml: true,
                        cc: envio.Copia.Count > 0 ? envio.Copia : null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error avisando al consolidador de la atención de las correcciones {Ids}",
                    string.Join(",", correccionIds));
            }
        }
    }
}
