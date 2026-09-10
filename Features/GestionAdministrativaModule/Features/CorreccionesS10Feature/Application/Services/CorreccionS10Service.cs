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
                    "La solicitud de corrección no existe o ya se cerró (el colaborador recargó el "
                    + "Consolidado del S10).", 404);

        public async Task<List<CorreoAvisoPreviewDto>> GetCorreoPreview(List<int> correccionIds)
        {
            try
            {
                var destinatarios = new List<string>();
                foreach (var id in correccionIds.Distinct())
                {
                    var quien = await _repo.GetSolicitante(id);
                    if (!string.IsNullOrWhiteSpace(quien?.Email)) destinatarios.Add(quien!.Email!);
                }

                var envio = await _correoResolver.ResolveEnvioAsync(
                    CorreoEventoCodigos.CorreccionS10Atendida, destinatarios);

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
                accion.CorreccionIds, accion.ComentarioAtencion, accion.GuiaAnulada, erpUserId);

            // El aviso al colaborador es best-effort: la confirmación ya está guardada y no se
            // revierte porque un correo falle — la ve igual en Mis Rendiciones (mismo criterio que
            // las decisiones del revisor).
            foreach (var id in atendidas)
                await NotificarAtencionAsync(id);

            return new CorreccionS10BulkResultDto
            {
                Procesadas = atendidas.Count,
                Message = atendidas.Count == 1
                    ? "Corrección marcada como atendida. Le avisamos al colaborador para que recargue el Consolidado del S10."
                    : $"{atendidas.Count} correcciones marcadas como atendidas.",
            };
        }

        /// <summary>
        /// Le avisa al colaborador que el ERP ya corrigió en el S10 y puede recargar el Consolidado
        /// (RF-OBS-08). Respeta la configuración de destinatarios y no tumba la acción si falla.
        /// </summary>
        private async Task NotificarAtencionAsync(int correccionId)
        {
            try
            {
                var datos = await _repo.GetCorreoDatos(correccionId);
                if (datos == null) return;

                var principal = string.IsNullOrWhiteSpace(datos.TrabajadorEmail)
                    ? new List<string>()
                    : new List<string> { datos.TrabajadorEmail! };

                var envio = await _correoResolver.ResolveEnvioAsync(
                    CorreoEventoCodigos.CorreccionS10Atendida, principal);

                if (!envio.Enviar || envio.Para.Count == 0) return;

                // El botón lleva a Mis Rendiciones, que es donde recarga el Consolidado del S10:
                // es el paso que esta confirmación acaba de habilitar.
                var url  = SalidaEnlaces.Rendiciones(_configuration, datos.RendicionId);
                var body = CorreccionS10EmailTemplates.Atendida(
                    SalidaEmailLayout.Desde(_configuration), datos, url);

                await _emailService.SendAsync(
                    to: envio.Para,
                    subject: $"Corrección del S10 atendida - {datos.Codigo}",
                    body: body,
                    isHtml: true,
                    cc: envio.Copia.Count > 0 ? envio.Copia : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error avisando al colaborador de la atención de la corrección {CorreccionId}",
                    correccionId);
            }
        }
    }
}
