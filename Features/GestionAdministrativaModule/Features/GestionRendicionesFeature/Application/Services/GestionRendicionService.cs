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
using Abril_Backend.Shared.Services.SharePoint.Dtos;
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

        /// <summary>
        /// Qué correos dispararía una de las cuatro decisiones de la pantalla sobre la selección
        /// indicada, y a quién le llegarían. Se resuelve con las MISMAS llamadas que hacen los
        /// envíos (<see cref="NotificarPrimeraRevisionAsync"/>,
        /// <see cref="NotificarDecisionReembolsoAsync"/> y <see cref="NotificarTesoreriaAsync"/>),
        /// así que la confirmación no puede prometer un correo que la configuración dejó fuera ni
        /// decir que no le llega a nadie cuando sí está activo.
        ///
        /// Lo consumen tanto los botones masivos de la tabla como los del modal de detalle: en los
        /// dos casos el conjunto lo resuelve el servidor con el recorte de visibilidad y la
        /// elegibilidad de la escritura, así que el preview no anuncia a nadie a quien la acción no
        /// vaya a tocar.
        ///
        /// Best-effort: ante un error devuelve una lista vacía en vez de romper la confirmación.
        /// Que el preview falle no puede impedir decidir.
        /// </summary>
        public async Task<List<CorreoAvisoPreviewDto>> GetCorreoPreview(
            CorreoPreviewRequestDto request, GestionRendicionFiltersDto scope)
        {
            try
            {
                await ApplyVisibilityAsync(scope);

                var esPrimeraRevision = string.Equals(
                    request.Accion, CorreoPreviewAcciones.PrimeraRevision, StringComparison.OrdinalIgnoreCase);

                var solicitantes = esPrimeraRevision
                    ? await _repo.GetCorreosSolicitantesPrimeraRevision(request.RendicionIds, scope)
                    : await _repo.GetCorreosSolicitantesPorDecidir(
                        request.RendicionIds, request.SolicitudIds, scope);

                var codigo = esPrimeraRevision
                    ? (request.Aprobar
                        ? CorreoEventoCodigos.RendicionPrimeraAprobada
                        : CorreoEventoCodigos.RendicionPrimeraObservada)
                    : (request.Aprobar
                        ? CorreoEventoCodigos.ReembolsoAprobado
                        : CorreoEventoCodigos.ReembolsoRechazado);

                var avisos = new List<CorreoAvisoPreviewDto>();
                await AgregarAvisoAsync(avisos, "Al solicitante", codigo, solicitantes);

                // Aprobar el reembolso ES firmar, y la firma es lo que mete la planilla en la
                // bandeja de Tesorería: por eso esa acción dispara un segundo correo.
                if (!esPrimeraRevision && request.Aprobar)
                    await AgregarAvisoAsync(
                        avisos, "A Tesorería",
                        CorreoEventoCodigos.TesoreriaReembolso,
                        await _repo.GetCorreosTesoreria());

                return avisos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error resolviendo el preview de correos de la acción {Accion}", request.Accion);
                return new List<CorreoAvisoPreviewDto>();
            }
        }

        /// <summary>
        /// Agrega un correo al preview solo si hoy se enviaría. Un aviso sin destinatarios no entra
        /// en la lista: la pantalla distingue "no sale ningún correo" de "sale a estas direcciones".
        /// </summary>
        private async Task AgregarAvisoAsync(
            List<CorreoAvisoPreviewDto> avisos, string etiqueta, string eventoCodigo, List<string> principal)
        {
            var envio = await _correoResolver.ResolveEnvioAsync(eventoCodigo, principal);
            if (!envio.Enviar || envio.Para.Count == 0) return;

            avisos.Add(new CorreoAvisoPreviewDto
            {
                Etiqueta = etiqueta,
                Para     = envio.Para,
                Copia    = envio.Copia,
            });
        }

        public async Task<ReembolsoBulkResultDto> DecidirPrimeraRevision(
            PrimeraRevisionAccionDto accion, bool aprobar, GestionRendicionFiltersDto scope, int reviewerUserId)
        {
            if (accion.RendicionIds.Count == 0)
                throw new AbrilException("Selecciona al menos una rendición.", 400);

            await ApplyVisibilityAsync(scope);

            var decididas = await _repo.DecidirPrimeraRevision(
                accion.RendicionIds, aprobar, accion.Observacion, scope, reviewerUserId);

            // El aviso al solicitante es best-effort: la decisión ya está guardada y no se revierte
            // porque un correo falle (mismo criterio que la decisión del reembolso).
            foreach (var rendicionId in decididas)
                await NotificarPrimeraRevisionAsync(rendicionId, aprobar);

            return new ReembolsoBulkResultDto
            {
                Procesadas = decididas.Count,
                Message = aprobar
                    ? $"{decididas.Count} rendición(es) aprobada(s) en primera revisión."
                    : $"{decididas.Count} rendición(es) observada(s).",
            };
        }

        public async Task<ConsolidadoS10Dto> UploadConsolidadoS10(
            int rendicionId, IFormFile file, decimal montoTotal, string numeroGuia, int userId)
            // Sin guard de propiedad: el revisor lo sube en nombre del trabajador. El alcance ya lo
            // recorta la pantalla — solo ve las planillas que le competen.
            => await _consolidadoService.UploadParaRendicion(
                rendicionId, file, montoTotal, numeroGuia, userId);

        public async Task<ReembolsoBulkResultDto> DecidirReembolso(
            ReembolsoAccionDto accion, bool aprobar, GestionRendicionFiltersDto scope, int reviewerUserId)
        {
            await ApplyVisibilityAsync(scope);

            var ids = await _repo.ResolverSolicitudIds(accion.RendicionIds, accion.SolicitudIds, scope);
            if (ids.Count == 0)
                throw new AbrilException("No hay salidas en la selección dentro de tu alcance.", 400);

            var rendicionesFirmadas = new List<int>();
            var decididas = aprobar
                ? await AprobarFirmandoAsync(ids, reviewerUserId, rendicionesFirmadas)
                : await _repo.RechazarReembolso(ids, accion.Observacion ?? string.Empty, reviewerUserId);

            // El aviso al solicitante es best-effort: la decisión ya está guardada y no se revierte
            // porque un correo falle (mismo criterio que la aprobación de la salida).
            foreach (var id in decididas)
                await NotificarDecisionReembolsoAsync(id, aprobar);

            // Y el aviso a Tesorería, que es por PLANILLA: lo que se paga es el documento entero.
            if (decididas.Count > 0)
                foreach (var rendicionId in rendicionesFirmadas.Distinct())
                    await NotificarTesoreriaAsync(rendicionId);

            return new ReembolsoBulkResultDto
            {
                Procesadas = decididas.Count,
                Message = aprobar
                    ? $"{decididas.Count} reembolso(s) aprobado(s)."
                    : $"{decididas.Count} reembolso(s) rechazado(s).",
            };
        }

        /// <summary>
        /// Aprueba el reembolso FIRMANDO: estampa la firma del revisor en todas las hojas de los
        /// documentos de cada planilla —su PDF y el Consolidado del S10— y deja las salidas en
        /// "Firmado", que es lo que Tesorería ve como pagable. Aprobar y firmar son el mismo acto:
        /// lo que el jefe respalda con su firma es justamente lo que está aprobando.
        ///
        /// Los PDF se suben ANTES de escribir el estado: si algo falla en SharePoint no queda una
        /// salida aprobada sin su respaldo firmado (al revés solo deja archivos huérfanos, que no
        /// rompen nada).
        /// </summary>
        /// <param name="rendicionesFirmadas">
        /// Se llena con las planillas que se firmaron. Sale por acá y no en el retorno porque el
        /// aviso a Tesorería es por planilla mientras que la decisión (y su correo al solicitante)
        /// es por salida: sin esta lista habría que volver a la base a agrupar lo mismo.
        /// </param>
        private async Task<List<int>> AprobarFirmandoAsync(
            List<int> ids, int userId, List<int> rendicionesFirmadas)
        {
            var firma = await _firmaRepository.GetActiveBytesByUserId(userId)
                // 409 y no 400: la pantalla lo distingue para abrir el modal donde el usuario dibuja
                // su firma en el momento en vez de mandarlo a Configuración.
                ?? throw new AbrilException(
                    "Todavía no registraste tu firma. Dibújala una vez y vuelve a aprobar.", 409);

            // Aplica los mismos guards que la escritura (elegibilidad y "nadie decide lo suyo").
            var planillas = await _repo.GetPlanillasParaAprobarReembolso(ids, userId);
            if (planillas.Count == 0)
                throw new AbrilException(
                    "Ninguna de las salidas seleccionadas tiene un reembolso por decidir.", 400);

            var carpeta = await ResolverCarpetaRendicionesAsync();

            var firmadas = new List<PlanillaFirmadaDto>(planillas.Count);
            foreach (var p in planillas)
            {
                var firmada = new PlanillaFirmadaDto
                {
                    RendicionId  = p.RendicionId,
                    SolicitudIds = p.SolicitudIds,
                    Planilla     = await FirmarYSubirAsync(carpeta, p.PlanillaUrl, p.PlanillaFilename, firma.Bytes),
                };

                foreach (var doc in p.Consolidados)
                    firmada.Consolidados[doc.Id] =
                        await FirmarYSubirAsync(carpeta, doc.Url, doc.Filename, firma.Bytes);

                firmadas.Add(firmada);
            }

            var decididas = await _repo.AprobarReembolsoFirmado(firmadas, userId);
            if (decididas.Count > 0)
                rendicionesFirmadas.AddRange(firmadas.Select(f => f.RendicionId));

            return decididas;
        }

        /// <summary>
        /// Descarga un PDF de SharePoint, le estampa la firma en TODAS sus hojas y sube la copia
        /// firmada al lado del original. El original nunca se pisa: la copia lleva el sufijo
        /// -FIRMADO y es la que queda referenciada como respaldo.
        /// </summary>
        private async Task<ArchivoFirmadoDto> FirmarYSubirAsync(
            ShareLinkResolveDto carpeta, string pdfUrl, string pdfFilename, byte[] firmaPng)
        {
            byte[] original;
            try
            {
                original = await _sharePointService.DownloadOneDriveFileByWebUrlAsync(pdfUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo descargar {Archivo} para firmarlo.", pdfFilename);
                throw new AbrilException(
                    $"No se pudo descargar {pdfFilename} desde SharePoint para firmarlo.", 502);
            }

            byte[] firmado;
            try
            {
                // Una planilla agrupa a varios trabajadores y cada grupo termina con su propia
                // línea de firma, así que la firma va en TODAS las hojas: solo al pie de la última
                // dejaría sin firma a todos los grupos menos el último.
                firmado = SignaturePdfStamper.Stamp(original, firmaPng);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo estampar la firma en {Archivo}.", pdfFilename);
                throw new AbrilException($"No se pudo generar la versión firmada de {pdfFilename}.", 500);
            }

            var filename = Path.GetFileNameWithoutExtension(pdfFilename) + "-FIRMADO.pdf";
            try
            {
                using var stream = new MemoryStream(firmado);
                var subido = await _sharePointService.UploadToOneDriveFolderAsync(
                    carpeta.DriveId, carpeta.ItemId, filename, stream,
                    "application/pdf", autoRenameOnLock: true);

                if (subido?.WebUrl is null)
                    throw new AbrilException($"No se pudo subir {filename} a SharePoint (respuesta vacía).", 502);

                return new ArchivoFirmadoDto
                {
                    Url      = subido.WebUrl,
                    ItemId   = subido.ItemId,
                    Filename = filename,
                };
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la subida de {Archivo}.", filename);
                throw new AbrilException($"No se pudo guardar {filename} en SharePoint.", 502);
            }
        }

        /// <summary>Carpeta de SharePoint donde viven las planillas y sus copias firmadas.</summary>
        private async Task<ShareLinkResolveDto> ResolverCarpetaRendicionesAsync()
        {
            var folderUrl = await _repo.GetRendicionFolderUrl();
            if (string.IsNullOrWhiteSpace(folderUrl))
                throw new AbrilException(
                    "No se ha configurado la carpeta de SharePoint donde guardar las planillas de rendición. " +
                    "Pide al administrador registrarla en la tabla ga_rendicion_folder.", 409);

            var carpeta = await _sharePointService.ResolveSharePointFolderUrlAsync(folderUrl);
            if (carpeta == null || !carpeta.IsFolder)
                throw new AbrilException("No se pudo resolver la carpeta de planillas de rendición en SharePoint.", 502);

            return carpeta;
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

        // ── Correos de la primera revisión ───────────────────────────────────

        /// <summary>
        /// Le avisa a cada dueño de las salidas de la planilla cómo quedó su primera revisión: si
        /// se aprobó, que ya puede cargar el Consolidado del S10; si se observó, con qué
        /// comentario. Respeta la configuración de correos (Gestión Administrativa →
        /// Configuración → Correos): si está apagado o sin destinatarios, no se envía nada.
        ///
        /// Sale un correo POR TRABAJADOR y no uno por planilla: el documento puede agrupar a varias
        /// personas y cada una tiene que ver sus propios números para poder contrastarlos.
        /// </summary>
        private async Task NotificarPrimeraRevisionAsync(int rendicionId, bool aprobada)
        {
            try
            {
                var destinatarios = await _repo.GetPrimeraRevisionCorreoInfo(rendicionId);
                if (destinatarios.Count == 0) return;

                var codigo = aprobada
                    ? CorreoEventoCodigos.RendicionPrimeraAprobada
                    : CorreoEventoCodigos.RendicionPrimeraObservada;

                var layout = SalidaEmailLayout.Desde(_configuration);
                // El botón lleva a Mis Rendiciones: lo que el trabajador tiene que hacer después de
                // la decisión —cargar el Consolidado del S10, o corregir y volver a generar— vive ahí.
                var url = SalidaEnlaces.Rendiciones(_configuration, rendicionId);

                foreach (var info in destinatarios)
                {
                    if (string.IsNullOrWhiteSpace(info.SolicitanteEmail))
                    {
                        _logger.LogWarning(
                            "Rendición {RendicionId}: {Trabajador} no tiene correo registrado, no se le avisó la primera revisión.",
                            rendicionId, info.Trabajador);
                        continue;
                    }

                    var envio = await _correoResolver.ResolveEnvioAsync(
                        codigo, new List<string> { info.SolicitanteEmail! });

                    if (!envio.Enviar)
                    {
                        _logger.LogInformation(
                            "Correo {Codigo} no enviado para la rendición {RendicionId}: está apagado o sin destinatarios.",
                            codigo, rendicionId);
                        return; // la configuración es del correo, no del destinatario: no hay caso de seguir
                    }

                    var datos = new RendicionRevisionCorreoDatos
                    {
                        RendicionId    = info.RendicionId,
                        Codigo         = info.Codigo,
                        Trabajador     = info.Trabajador,
                        Area           = info.Area,
                        Periodo        = info.Periodo,
                        SalidasCount   = info.SalidasCount,
                        TramosCount    = info.TramosCount,
                        MontoTotal     = info.MontoTotal,
                        NumeroPlanilla = info.NumeroPlanilla,
                        DecididoPor    = info.DecididoPor,
                        Observacion    = info.Observacion,
                    };

                    var body = aprobada
                        ? RendicionRevisionEmailTemplates.Aprobada(layout, datos, url)
                        : RendicionRevisionEmailTemplates.Observada(layout, datos, url);

                    var subject = aprobada
                        ? $"Rendición {info.Codigo} APROBADA en primera revisión"
                        : $"Rendición {info.Codigo} OBSERVADA en primera revisión";

                    await _emailService.SendAsync(
                        to: envio.Para,
                        subject: subject,
                        body: body,
                        isHtml: true,
                        cc: envio.Copia.Count > 0 ? envio.Copia : null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error avisando la decisión de la primera revisión de la rendición {RendicionId}", rendicionId);
            }
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

        /// <summary>
        /// Avisa a Tesorería que una planilla quedó firmada y su reembolso ya está en su bandeja
        /// (RF-TES-01). Los destinatarios salen del puesto (categoría Tesorero) y no de una lista
        /// escrita a mano; los de <c>Configuración → Correos</c> se suman como copia. Best-effort,
        /// igual que el resto: la firma ya está guardada.
        /// </summary>
        private async Task NotificarTesoreriaAsync(int rendicionId)
        {
            try
            {
                var info = await _repo.GetTesoreriaCorreoInfo(rendicionId);
                if (info == null) return;

                var envio = await _correoResolver.ResolveEnvioAsync(
                    CorreoEventoCodigos.TesoreriaReembolso, info.Destinatarios);

                if (!envio.Enviar)
                {
                    _logger.LogInformation(
                        "Correo {Codigo} no enviado para la rendición {RendicionId}: está apagado, "
                        + "sin destinatarios configurados o sin nadie con puesto de Tesorería.",
                        CorreoEventoCodigos.TesoreriaReembolso, rendicionId);
                    return;
                }

                var layout = SalidaEmailLayout.Desde(_configuration);
                var url    = SalidaEnlaces.Reembolsos(_configuration, rendicionId);

                await _emailService.SendAsync(
                    to: envio.Para,
                    subject: $"Reembolso por pagar - rendición {info.Datos.Codigo}",
                    body: ReembolsoEmailTemplates.PorPagarTesoreria(layout, info.Datos, url),
                    isHtml: true,
                    cc: envio.Copia.Count > 0 ? envio.Copia : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error avisando a Tesorería de la rendición firmada {RendicionId}", rendicionId);
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
