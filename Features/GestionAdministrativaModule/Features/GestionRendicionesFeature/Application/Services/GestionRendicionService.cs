using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Firma.Interfaces;
using Abril_Backend.Shared.Services.Pdf;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Services
{
    public class GestionRendicionService : IGestionRendicionService
    {
        private readonly IGestionRendicionRepository    _repo;
        private readonly ISalidaVisibilityResolver      _visibilityResolver;
        private readonly IConsolidadoS10Service         _consolidadoService;
        private readonly IFirmaPersonalRepository       _firmaRepository;
        private readonly IGraphSharePointService        _sharePointService;
        private readonly ICorreoSalidaRecipientResolver _correoResolver;
        private readonly IEmailService                  _emailService;
        private readonly IConfiguration                 _configuration;
        private readonly ILogger<GestionRendicionService> _logger;

        public GestionRendicionService(
            IGestionRendicionRepository repo,
            ISalidaVisibilityResolver visibilityResolver,
            IConsolidadoS10Service consolidadoService,
            IFirmaPersonalRepository firmaRepository,
            IGraphSharePointService sharePointService,
            ICorreoSalidaRecipientResolver correoResolver,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<GestionRendicionService> logger)
        {
            _repo               = repo;
            _visibilityResolver = visibilityResolver;
            _consolidadoService = consolidadoService;
            _firmaRepository    = firmaRepository;
            _sharePointService  = sharePointService;
            _correoResolver     = correoResolver;
            _emailService       = emailService;
            _configuration      = configuration;
            _logger             = logger;
        }

        public async Task<GestionRendicionListResultDto> GetAll(GestionRendicionFiltersDto filters)
        {
            await ApplyVisibilityAsync(filters);
            var data = await _repo.GetAll(filters);
            return new GestionRendicionListResultDto
            {
                Data    = data,
                Resumen = ResumenGestionRendicionesDto.De(data),
            };
        }

        public async Task<GestionRendicionFilterDataDto> GetFilterData(GestionRendicionFiltersDto scope)
        {
            await ApplyVisibilityAsync(scope);
            return await _repo.GetFilterData(scope);
        }

        public async Task<GestionRendicionDetalleDto> GetDetalle(int rendicionId, GestionRendicionFiltersDto scope)
        {
            await ApplyVisibilityAsync(scope);
            return await _repo.GetDetalle(rendicionId, scope)
                ?? throw new AbrilException("La planilla de rendición no existe o no está en tu alcance.", 404);
        }

        public async Task<ConsolidadoS10Dto> UploadConsolidadoS10(int rendicionId, IFormFile file, int userId)
            // Sin guard de propiedad: el revisor lo sube en nombre del trabajador. El alcance ya lo
            // recorta la pantalla — solo ve las planillas que le competen.
            => await _consolidadoService.UploadParaRendicion(rendicionId, file, userId);

        public async Task<ReembolsoBulkResultDto> DecidirReembolso(
            ReembolsoAccionDto accion, bool aprobar, GestionRendicionFiltersDto scope, int reviewerUserId)
        {
            await ApplyVisibilityAsync(scope);

            var ids = await _repo.ResolverSolicitudIds(accion.RendicionIds, accion.SolicitudIds, scope);
            if (ids.Count == 0)
                throw new AbrilException("No hay salidas en la selección dentro de tu alcance.", 400);

            var decididas = await _repo.DecidirReembolso(ids, aprobar, accion.Observacion, reviewerUserId);

            // El aviso al solicitante es best-effort: la decisión ya está guardada y no se revierte
            // porque un correo falle (mismo criterio que la aprobación de la salida).
            foreach (var id in decididas)
                await NotificarDecisionReembolsoAsync(id, aprobar);

            return new ReembolsoBulkResultDto
            {
                Procesadas = decididas.Count,
                Message = aprobar
                    ? $"{decididas.Count} reembolso(s) aprobado(s)."
                    : $"{decididas.Count} reembolso(s) rechazado(s).",
            };
        }

        public async Task<ReembolsoBulkResultDto> Firmar(
            ReembolsoAccionDto accion, GestionRendicionFiltersDto scope, int userId)
        {
            await ApplyVisibilityAsync(scope);

            var ids = await _repo.ResolverSolicitudIds(accion.RendicionIds, accion.SolicitudIds, scope);
            if (ids.Count == 0)
                throw new AbrilException("No hay salidas en la selección dentro de tu alcance.", 400);

            var firma = await _firmaRepository.GetActiveBytesByUserId(userId)
                // 409 y no 400: la pantalla lo distingue para abrir el modal donde el usuario dibuja
                // su firma en el momento en vez de mandarlo a Configuración.
                ?? throw new AbrilException(
                    "Todavía no registraste tu firma. Dibújala una vez y vuelve a firmar.", 409);

            var planillas = await _repo.GetRendicionesPorFirmar(ids);
            if (planillas.Count == 0)
                throw new AbrilException(
                    "Ninguna de las salidas seleccionadas se puede firmar: primero hay que aprobar su reembolso.",
                    400);

            var folderUrl = await _repo.GetRendicionFolderUrl();
            if (string.IsNullOrWhiteSpace(folderUrl))
                throw new AbrilException(
                    "No se ha configurado la carpeta de SharePoint donde guardar las planillas de rendición. " +
                    "Pide al administrador registrarla en la tabla ga_rendicion_folder.", 409);

            var carpeta = await _sharePointService.ResolveSharePointFolderUrlAsync(folderUrl);
            if (carpeta == null || !carpeta.IsFolder)
                throw new AbrilException("No se pudo resolver la carpeta de planillas de rendición en SharePoint.", 502);

            var totalSalidas   = 0;
            var totalPlanillas = 0;

            foreach (var planilla in planillas)
            {
                string? pdfUrl = null, pdfItemId = null, pdfFilename = null;

                // Si la planilla ya está firmada no se vuelve a estampar: el documento es uno solo
                // y ya lleva la firma. Solo se mueven de estado las salidas que faltaban.
                if (string.IsNullOrWhiteSpace(planilla.PdfFirmadoUrl))
                {
                    byte[] original;
                    try
                    {
                        original = await _sharePointService.DownloadOneDriveFileByWebUrlAsync(planilla.PdfUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "No se pudo descargar la planilla {RendicionId} para firmarla.", planilla.RendicionId);
                        throw new AbrilException(
                            "No se pudo descargar la planilla de rendición desde SharePoint para firmarla.", 502);
                    }

                    byte[] firmado;
                    try
                    {
                        // Una planilla agrupa a varios trabajadores y cada grupo termina con su
                        // propia línea de firma de jefatura, así que la firma tiene que ir en todas
                        // las hojas: solo al pie de la última dejaría sin firma a todos los grupos
                        // menos el último.
                        firmado = SignaturePdfStamper.Stamp(original, firma.Bytes);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "No se pudo estampar la firma en la planilla {RendicionId}.", planilla.RendicionId);
                        throw new AbrilException("No se pudo generar la planilla firmada.", 500);
                    }

                    pdfFilename = Path.GetFileNameWithoutExtension(planilla.PdfFilename) + "-FIRMADO.pdf";
                    try
                    {
                        using var stream = new MemoryStream(firmado);
                        var subido = await _sharePointService.UploadToOneDriveFolderAsync(
                            carpeta.DriveId, carpeta.ItemId, pdfFilename, stream,
                            "application/pdf", autoRenameOnLock: true);

                        if (subido?.WebUrl is null)
                            throw new AbrilException("No se pudo subir la planilla firmada a SharePoint (respuesta vacía).", 502);

                        pdfUrl    = subido.WebUrl;
                        pdfItemId = subido.ItemId;
                    }
                    catch (AbrilException) { throw; }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Falló la subida de la planilla firmada {RendicionId}.", planilla.RendicionId);
                        throw new AbrilException("No se pudo guardar la planilla firmada en SharePoint.", 502);
                    }

                    totalPlanillas++;
                }

                await _repo.MarcarFirmadas(
                    planilla.RendicionId, planilla.SolicitudIds, userId, pdfUrl, pdfItemId, pdfFilename);
                totalSalidas += planilla.SolicitudIds.Count;
            }

            return new ReembolsoBulkResultDto
            {
                Procesadas        = totalSalidas,
                PlanillasFirmadas = totalPlanillas,
                Message           = $"{totalSalidas} salida(s) firmada(s).",
            };
        }

        // ── Visibilidad ──────────────────────────────────────────────────────

        /// <summary>
        /// Resuelve el alcance del usuario y lo escribe en el filtro. Es el MISMO criterio que
        /// Gestión de Salidas (recepción/GTH ven todo; el resto, su área hacia abajo) menos el modo
        /// Tesorería, que no vive acá: pagar es de Reembolsos.
        /// </summary>
        private async Task ApplyVisibilityAsync(GestionRendicionFiltersDto filters)
        {
            if (!filters.CurrentUserId.HasValue) return;

            if (filters.SeesAllOverride)
            {
                filters.SeesAll = true;
                return;
            }

            var vis = await _visibilityResolver.ResolveAsync(filters.CurrentUserId.Value);
            filters.SeesAll             = vis.SeesAll;
            filters.VisibleAreaScopeIds = vis.AreaScopeIds.ToList();
        }

        // ── Correos de la decisión ───────────────────────────────────────────

        /// <summary>
        /// Avisa al solicitante que su reembolso quedó aprobado o rechazado. Respeta la
        /// configuración de correos (Gestión Administrativa → Configuración → Correos): si el
        /// correo está apagado o no queda ningún destinatario, no se envía nada.
        /// </summary>
        private async Task NotificarDecisionReembolsoAsync(int solicitudId, bool aprobado)
        {
            try
            {
                var info = await _repo.GetReembolsoCorreoInfo(solicitudId);
                if (info == null) return;

                if (string.IsNullOrWhiteSpace(info.SolicitanteEmail))
                {
                    _logger.LogWarning(
                        "Reembolso {SolicitudId}: el solicitante no tiene correo registrado, no se avisó la decisión.",
                        solicitudId);
                    return;
                }

                var codigo = aprobado
                    ? CorreoEventoCodigos.ReembolsoAprobado
                    : CorreoEventoCodigos.ReembolsoRechazado;

                var envio = await _correoResolver.ResolveEnvioAsync(
                    codigo, new List<string> { info.SolicitanteEmail });

                if (!envio.Enviar)
                {
                    _logger.LogInformation(
                        "Correo {Codigo} no enviado para la salida {SolicitudId}: está apagado o sin destinatarios.",
                        codigo, solicitudId);
                    return;
                }

                var layout = SalidaEmailLayout.Desde(_configuration);
                var datos  = ToCorreoDatos(info);
                // El botón lleva a Mis Rendiciones: subsanar es volver a adjuntar el Consolidado
                // del S10, que es de la planilla. Solo cae a la salida si (por datos viejos) la
                // salida no tiene planilla, para no dejar el correo sin destino.
                var url    = info.RendicionId.HasValue
                    ? SalidaEnlaces.Rendiciones(_configuration, info.RendicionId.Value)
                    : SalidaEnlaces.Autoservicio(_configuration, solicitudId);

                var body = aprobado
                    ? ReembolsoEmailTemplates.Aprobado(layout, datos, url)
                    : ReembolsoEmailTemplates.Rechazado(layout, datos, url);

                var subject = aprobado
                    ? $"Reembolso APROBADO - salida del {info.FechaSalida:dd/MM/yyyy}"
                    : $"Reembolso RECHAZADO - salida del {info.FechaSalida:dd/MM/yyyy}";

                await _emailService.SendAsync(
                    to: envio.Para,
                    subject: subject,
                    body: body,
                    isHtml: true,
                    cc: envio.Copia.Count > 0 ? envio.Copia : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error avisando la decisión del reembolso de la salida {SolicitudId}", solicitudId);
            }
        }

        /// <summary>Pasa los datos del repositorio al shape que consumen las plantillas.</summary>
        private static ReembolsoCorreoDatos ToCorreoDatos(ReembolsoCorreoInfoDto info) =>
            new()
            {
                SolicitudId     = info.SolicitudId,
                Codigo          = info.Codigo,
                Trabajador      = info.Trabajador,
                TrabajadorEmail = info.SolicitanteEmail,
                Area            = info.Area,
                FechaSalida     = info.FechaSalida,
                NumeroPlanilla  = info.NumeroPlanilla,
                TrayectosCount  = info.TrayectosCount,
                MontoTotal      = info.MontoTotal,
                DecididoPor     = info.DecididoPor,
                Observacion     = info.ObservacionReembolso,
            };
    }
}
