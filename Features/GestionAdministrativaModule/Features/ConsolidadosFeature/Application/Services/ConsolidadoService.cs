using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Consolidados.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Firma.Interfaces;
using Abril_Backend.Shared.Services.Pdf;
using Abril_Backend.Shared.Services.SharePoint.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Services
{
    /// <summary>
    /// Implementación de <see cref="IConsolidadoService"/>. Ver ahí por qué la decisión del
    /// reembolso vive en esta pantalla y no en Gestión de Rendiciones, y quién hace qué.
    /// </summary>
    public class ConsolidadoService : IConsolidadoService
    {
        /// <summary>Largo de la columna <c>ga_correccion_s10.motivo</c>.</summary>
        private const int MotivoCorreccionMaxLength = 2000;

        private readonly IConsolidadoRepository         _repo;
        private readonly ISalidaVisibilityResolver      _visibilityResolver;
        private readonly IFirmaPersonalRepository       _firmaRepository;
        private readonly IGraphSharePointService        _sharePointService;
        private readonly ICorreoSalidaRecipientResolver _correoResolver;
        private readonly IEmailService                  _emailService;
        private readonly IConfiguration                 _configuration;
        private readonly ILogger<ConsolidadoService>    _logger;

        public ConsolidadoService(
            IConsolidadoRepository repo,
            ISalidaVisibilityResolver visibilityResolver,
            IFirmaPersonalRepository firmaRepository,
            IGraphSharePointService sharePointService,
            ICorreoSalidaRecipientResolver correoResolver,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<ConsolidadoService> logger)
        {
            _repo               = repo;
            _visibilityResolver = visibilityResolver;
            _firmaRepository    = firmaRepository;
            _sharePointService  = sharePointService;
            _correoResolver     = correoResolver;
            _emailService       = emailService;
            _configuration      = configuration;
            _logger             = logger;
        }

        public async Task<ConsolidadoListResultDto> GetAll(ConsolidadoFiltersDto filters)
        {
            await ApplyVisibilityAsync(filters);
            var data = await _repo.GetAll(filters);
            return new ConsolidadoListResultDto
            {
                Data    = data,
                Resumen = ResumenConsolidadosDto.De(data),
            };
        }

        public async Task<ConsolidadoFilterDataDto> GetFilterData(ConsolidadoFiltersDto scope)
        {
            await ApplyVisibilityAsync(scope);
            return await _repo.GetFilterData(scope);
        }

        public async Task<ConsolidadoDetalleDto> GetDetalle(int consolidadoId, ConsolidadoFiltersDto scope)
        {
            await ApplyVisibilityAsync(scope);
            return await _repo.GetDetalle(consolidadoId, scope)
                ?? throw new AbrilException("El Consolidado del S10 no existe o no está en tu alcance.", 404);
        }

        public async Task<SolicitudSalidaDetalleDto> GetSalidaDetalle(int solicitudId, ConsolidadoFiltersDto scope)
        {
            await ApplyVisibilityAsync(scope);
            return await _repo.GetSalidaDetalle(solicitudId, scope)
                ?? throw new AbrilException("La salida no existe o no está en tu alcance.", 404);
        }

        /// <summary>
        /// Qué correos dispararía la acción sobre la selección indicada y a quién le llegarían. Se
        /// resuelve con las MISMAS llamadas que hacen los envíos, así que la confirmación no puede
        /// prometer un correo que la configuración dejó fuera ni decir que no le llega a nadie cuando
        /// sí está activo.
        ///
        /// Best-effort: ante un error devuelve una lista vacía en vez de romper la confirmación.
        /// Que el preview falle no puede impedir la acción.
        /// </summary>
        public async Task<List<CorreoAvisoPreviewDto>> GetCorreoPreview(
            ConsolidadoCorreoPreviewRequestDto request, ConsolidadoFiltersDto scope)
        {
            try
            {
                await ApplyVisibilityAsync(scope);

                var userId = scope.CurrentUserId ?? 0;
                var avisos = new List<CorreoAvisoPreviewDto>();
                var consolidadoId = request.ConsolidadoIds.FirstOrDefault();

                switch (request.Accion)
                {
                    case ConsolidadoCorreoAcciones.AvisoJefatura:
                    {
                        var aviso = await _repo.GetAvisoJefatura(consolidadoId, scope, userId);
                        if (aviso is { PuedeConsolidar: true } && aviso.SolicitudIds.Count > 0)
                            await AgregarAvisoAsync(
                                avisos, "A la jefatura", CorreoEventoCodigos.S10Revisor, aviso.JefaturaEmails);
                        break;
                    }

                    case ConsolidadoCorreoAcciones.CorreccionErp:
                    {
                        var plan = await _repo.GetCorreccionPlan(consolidadoId, scope, userId);
                        if (plan is { PuedeConsolidar: true, HayCorreccionEnCurso: false })
                            await AgregarAvisoAsync(
                                avisos, "Al Coordinador ERP", CorreoEventoCodigos.CorreccionS10Solicitada,
                                await _repo.GetCorreosCoordinadorErp());
                        break;
                    }

                    default:
                    {
                        var consolidadores = await _repo.GetCorreosConsolidadorPorDecidir(
                            request.ConsolidadoIds, scope, userId);

                        await AgregarAvisoAsync(
                            avisos, "Al consolidador",
                            request.Aprobar ? CorreoEventoCodigos.ReembolsoAprobado : CorreoEventoCodigos.ReembolsoObservado,
                            consolidadores);

                        // Aprobar el reembolso ES firmar, y la firma es lo que mete la planilla en la
                        // bandeja de Tesorería: por eso esa acción dispara un segundo correo.
                        if (request.Aprobar)
                            await AgregarAvisoAsync(
                                avisos, "A Tesorería",
                                CorreoEventoCodigos.TesoreriaReembolso,
                                await _repo.GetCorreosTesoreria());
                        break;
                    }
                }

                return avisos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolviendo el preview de correos de Consolidados");
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

        // ══ La decisión de la jefatura ══════════════════════════════════════

        public async Task<ReembolsoBulkResultDto> DecidirReembolso(
            ConsolidadoAccionDto accion, bool aprobar, ConsolidadoFiltersDto scope, int reviewerUserId)
        {
            if (accion.ConsolidadoIds.Count == 0)
                throw new AbrilException("Selecciona al menos un Consolidado del S10.", 400);

            await ApplyVisibilityAsync(scope);

            var ids = await _repo.ResolverSolicitudIds(accion.ConsolidadoIds, scope);
            if (ids.Count == 0)
                throw new AbrilException("No hay salidas en la selección dentro de tu alcance.", 400);

            var rendicionesFirmadas = new List<int>();
            var decididas = aprobar
                ? await AprobarFirmandoAsync(ids, reviewerUserId, rendicionesFirmadas)
                : await _repo.ObservarReembolso(ids, accion.Observacion ?? string.Empty, reviewerUserId);

            // El aviso al consolidador es best-effort: la decisión ya está guardada y no se revierte
            // porque un correo falle (mismo criterio que la aprobación de la salida).
            await NotificarDecisionAsync(decididas, aprobar);

            // Y el aviso a Tesorería, que es por PLANILLA: lo que se paga es el documento entero.
            if (decididas.Count > 0)
                foreach (var rendicionId in rendicionesFirmadas.Distinct())
                    await NotificarTesoreriaAsync(rendicionId);

            return new ReembolsoBulkResultDto
            {
                Procesadas        = decididas.Count,
                PlanillasFirmadas = rendicionesFirmadas.Distinct().Count(),
                Message = aprobar
                    ? $"{decididas.Count} reembolso(s) aprobado(s)."
                    : $"{decididas.Count} reembolso(s) observado(s).",
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
        /// aviso a Tesorería es por planilla mientras que la decisión es por salida: sin esta lista
        /// habría que volver a la base a agrupar lo mismo.
        /// </param>
        private async Task<List<int>> AprobarFirmandoAsync(
            List<int> ids, int userId, List<int> rendicionesFirmadas)
        {
            // Solo valen las firmas del tipo que hoy pide Consolidados → Configuración → Firmas: si
            // la configuración pide imagen, una firma dibujada no sirve para firmar acá (aunque la
            // persona la tenga registrada desde Contabilidad). Entre varias válidas gana la imagen.
            var tiposHabilitados = (await _firmaRepository.GetTipos())
                .Where(t => t.Activo)
                .Select(t => t.Codigo)
                .ToList();

            var firma = await _firmaRepository.GetActiveBytesByUserId(userId, tiposHabilitados)
                // 409 y no 400: la pantalla lo distingue para abrir el modal donde el usuario
                // registra su firma en el momento en vez de mandarlo a Configuración.
                ?? throw new AbrilException(
                    "Todavía no registraste tu firma. Regístrala una vez y vuelve a aprobar.", 409);

            // Aplica los mismos guards que la escritura (elegibilidad y que sea su jefatura).
            var planillas = await _repo.GetPlanillasParaAprobarReembolso(ids, userId);
            if (planillas.Count == 0)
                throw new AbrilException(
                    "Ninguna de las salidas seleccionadas tiene un reembolso por decidir.", 400);

            var carpeta = await ResolverCarpetaRendicionesAsync();

            var firmadas = new List<PlanillaFirmadaDto>(planillas.Count);
            // Un consolidado compartido por varias planillas de la selección se firma una sola vez.
            var consolidadosFirmados = new Dictionary<int, ArchivoFirmadoDto>();
            foreach (var p in planillas)
            {
                var firmada = new PlanillaFirmadaDto
                {
                    RendicionId  = p.RendicionId,
                    SolicitudIds = p.SolicitudIds,
                    Planilla     = await FirmarYSubirAsync(carpeta, p.PlanillaUrl, p.PlanillaFilename, firma.Bytes),
                };

                foreach (var doc in p.Consolidados)
                {
                    if (!consolidadosFirmados.TryGetValue(doc.Id, out var archivo))
                    {
                        archivo = await FirmarYSubirAsync(carpeta, doc.Url, doc.Filename, firma.Bytes, doc.Slot);
                        consolidadosFirmados[doc.Id] = archivo;
                    }
                    firmada.Consolidados[doc.Id] = archivo;
                }

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
        /// <param name="pdfUrl">
        /// PDF sobre el que se estampa. Para un consolidado compartido que otro jefe ya firmó es su
        /// copia firmada: la firma nueva se suma (en <paramref name="slot"/>) y la copia se reemplaza.
        /// </param>
        /// <param name="pdfFilename">Nombre del ORIGINAL: la copia se llama igual, con -FIRMADO.</param>
        /// <param name="slot">Lugar de la firma en la hoja (0 = la esquina de siempre).</param>
        private async Task<ArchivoFirmadoDto> FirmarYSubirAsync(
            ShareLinkResolveDto carpeta, string pdfUrl, string pdfFilename, byte[] firmaPng, int slot = 0)
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
                firmado = SignaturePdfStamper.Stamp(original, firmaPng, slot);
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

        // ══ Los trámites del consolidador ═══════════════════════════════════

        public async Task<string> NotificarJefatura(int consolidadoId, ConsolidadoFiltersDto scope, int userId)
        {
            await ApplyVisibilityAsync(scope);

            var info = await _repo.GetAvisoJefatura(consolidadoId, scope, userId)
                ?? throw new AbrilException("El Consolidado del S10 no existe o no está en tu alcance.", 404);

            if (!info.PuedeConsolidar)
                throw new AbrilException(
                    "Solo el consolidador de estas rendiciones puede avisarle a la jefatura.", 403);

            if (info.SolicitudIds.Count == 0 || info.Datos == null)
                throw new AbrilException(
                    "Este consolidado no tiene reembolsos esperando a la jefatura: no hace falta avisar.", 400);

            if (info.JefaturaEmails.Count == 0)
                throw new AbrilException(
                    "No se pudo determinar el correo de la jefatura de estos trabajadores. Avisa a Gestión del Talento Humano.",
                    409);

            // A diferencia de las decisiones, este aviso ES el correo: si no le llega a nadie se corta
            // acá en vez de marcar un aviso que nunca salió.
            var envio = await _correoResolver.ResolveEnvioAsync(
                CorreoEventoCodigos.S10Revisor, info.JefaturaEmails);

            if (!envio.Enviar)
                throw new AbrilException(
                    "El aviso a la jefatura está desactivado en Consolidados → Configuración → Correos.", 409);

            var url  = SalidaEnlaces.Consolidados(_configuration, consolidadoId);
            var body = ReembolsoEmailTemplates.ConsolidadoPorRevisar(
                SalidaEmailLayout.Desde(_configuration), info.Datos, url);

            var numero = string.IsNullOrWhiteSpace(info.Datos.NumeroReembolso)
                ? string.Empty
                : $" N.° {info.Datos.NumeroReembolso}";

            await _emailService.SendAsync(
                to: envio.Para,
                subject: $"Reembolso por revisar - Consolidado del S10{numero}",
                body: body,
                isHtml: true,
                cc: envio.Copia.Count > 0 ? envio.Copia : null);

            await _repo.MarcarJefaturaAvisada(info.SolicitudIds, userId);

            return $"Se le avisó a {ConsolidadoS10Agrupacion.Enumerar(info.JefaturaNombres)}.";
        }

        public async Task<string> SolicitarCorreccionS10(
            int consolidadoId, string motivo, ConsolidadoFiltersDto scope, int userId)
        {
            var texto = (motivo ?? string.Empty).Trim();
            if (texto.Length == 0)
                throw new AbrilException(
                    "Escribe el motivo: es lo que el Coordinador ERP va a leer para saber qué corregir.", 400);
            if (texto.Length > MotivoCorreccionMaxLength)
                throw new AbrilException(
                    $"El motivo no puede pasar de {MotivoCorreccionMaxLength} caracteres.", 400);

            await ApplyVisibilityAsync(scope);

            var plan = await _repo.GetCorreccionPlan(consolidadoId, scope, userId)
                ?? throw new AbrilException("El Consolidado del S10 no existe o no está en tu alcance.", 404);

            if (!plan.PuedeConsolidar)
                throw new AbrilException(
                    "Solo el consolidador de estas rendiciones puede pedirle la corrección al Coordinador ERP.", 403);

            if (plan.HayCorreccionEnCurso)
                throw new AbrilException("Ya hay una corrección en curso para este consolidado.", 409);

            if (plan.RendicionIdsObservadas.Count == 0)
                throw new AbrilException(
                    "Solo se puede pedir una corrección al ERP cuando el reembolso del consolidado está observado.", 400);

            // El correo se resuelve ANTES de escribir: una corrección que el ERP nunca ve deja al
            // consolidador esperando algo que no va a pasar. Es la excepción al best-effort del resto
            // de los avisos, y por eso corta con 409 en vez de seguir.
            var envio = await _correoResolver.ResolveEnvioAsync(
                CorreoEventoCodigos.CorreccionS10Solicitada,
                await _repo.GetCorreosCoordinadorErp());

            if (!envio.Enviar || envio.Para.Count == 0)
                throw new AbrilException(
                    "No hay ningún Coordinador ERP con correo al que enviarle la solicitud. Avisa al "
                    + "administrador del sistema.", 409);

            var creadas = await _repo.CrearCorrecciones(
                consolidadoId, plan.NumeroReembolso, plan.RendicionIdsObservadas, texto, userId);

            // La corrección ya quedó registrada (y visible en la bandeja del ERP): si el correo falla
            // se loguea en vez de responder un error que invitaría a pedirla de nuevo.
            try
            {
                var primera = creadas[0];
                var d = plan.Datos;
                var datos = new CorreccionS10CorreoDatos
                {
                    CorreccionId     = primera.Id,
                    RendicionId      = primera.RendicionId,
                    Codigo           = d != null && d.Rendiciones.Count > 0
                                        ? string.Join(", ", d.Rendiciones)
                                        : $"#{primera.RendicionId}",
                    RendicionesCount = d?.Rendiciones.Count ?? 1,
                    Trabajador       = d != null && d.Trabajadores.Count > 0
                                        ? string.Join(", ", d.Trabajadores)
                                        : "Colaborador",
                    SolicitadaPor    = plan.Solicitante,
                    Periodo          = d?.Periodo,
                    NumeroReembolso  = plan.NumeroReembolso,
                    MontoTotal       = d?.MontoTotal ?? 0m,
                    Motivo           = texto,
                    MotivoJefatura   = primera.MotivoJefatura,
                    MotivoOrigen     = EstadosSalida.OrigenObservacionReembolso.Nombre(primera.MotivoOrigenId),
                };

                // El botón abre la bandeja del ERP en esta corrección: es donde marca el check.
                var url  = SalidaEnlaces.CorreccionesS10(_configuration, primera.Id);
                var body = CorreccionS10EmailTemplates.Solicitada(
                    SalidaEmailLayout.Desde(_configuration), datos, url);

                var numero = string.IsNullOrWhiteSpace(plan.NumeroReembolso)
                    ? string.Empty
                    : $" N.° {plan.NumeroReembolso}";

                await _emailService.SendAsync(
                    to: envio.Para,
                    subject: $"Corrección del S10 solicitada - Consolidado del S10{numero}",
                    body: body,
                    isHtml: true,
                    cc: envio.Copia.Count > 0 ? envio.Copia : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error avisando al Coordinador ERP la corrección del consolidado {ConsolidadoId}", consolidadoId);
            }

            return "Solicitud enviada al Coordinador ERP.";
        }

        // ── Visibilidad ──────────────────────────────────────────────────────

        /// <summary>
        /// Resuelve el alcance del usuario y lo escribe en el filtro. Mismo criterio que las otras
        /// bandejas (recepción/GTH ven todo; el resto, su área hacia abajo) pero en el ámbito
        /// CONSOLIDADOS: ver las planillas en Gestión de Rendiciones y ver los consolidados acá se
        /// configuran por separado.
        /// </summary>
        private async Task ApplyVisibilityAsync(ConsolidadoFiltersDto filters)
        {
            if (!filters.CurrentUserId.HasValue) return;

            if (filters.SeesAllOverride)
            {
                filters.SeesAll = true;
                return;
            }

            var vis = await _visibilityResolver.ResolveAsync(
                filters.CurrentUserId.Value, VisibilidadAmbitoIds.Consolidados);
            filters.SeesAll             = vis.SeesAll;
            filters.VisibleAreaScopeIds = vis.AreaScopeIds.ToList();
        }

        // ── Correos de la decisión ───────────────────────────────────────────

        /// <summary>
        /// Avisa al consolidador —quien adjuntó el consolidado— que la jefatura aprobó u observó su
        /// reembolso. Va UN correo por consolidado y no uno por salida: lo que se decidió y lo que hay
        /// que subsanar es el documento. Respeta la configuración de correos (Consolidados →
        /// Configuración → Correos): si está apagado o sin destinatarios, no se envía nada.
        /// </summary>
        private async Task NotificarDecisionAsync(List<int> solicitudIds, bool aprobado)
        {
            if (solicitudIds.Count == 0) return;

            try
            {
                var codigo = aprobado
                    ? CorreoEventoCodigos.ReembolsoAprobado
                    : CorreoEventoCodigos.ReembolsoObservado;

                var layout = SalidaEmailLayout.Desde(_configuration);

                foreach (var d in await _repo.GetConsolidadoCorreoDatos(solicitudIds))
                {
                    if (string.IsNullOrWhiteSpace(d.ConsolidadorEmail))
                    {
                        _logger.LogWarning(
                            "Consolidado {ConsolidadoId}: quien lo adjuntó no tiene correo registrado, no se avisó la decisión.",
                            d.ConsolidadoId);
                        continue;
                    }

                    var envio = await _correoResolver.ResolveEnvioAsync(
                        codigo, new List<string> { d.ConsolidadorEmail });

                    if (!envio.Enviar)
                    {
                        _logger.LogInformation(
                            "Correo {Codigo} no enviado para el consolidado {ConsolidadoId}: está apagado o sin destinatarios.",
                            codigo, d.ConsolidadoId);
                        return; // la configuración es del correo, no del destinatario: no hay caso de seguir
                    }

                    var url  = SalidaEnlaces.Consolidados(_configuration, d.ConsolidadoId);
                    var body = aprobado
                        ? ReembolsoEmailTemplates.ConsolidadoAprobado(layout, d, url)
                        : ReembolsoEmailTemplates.ConsolidadoObservado(layout, d, url);

                    var numero = string.IsNullOrWhiteSpace(d.NumeroReembolso) ? string.Empty : $" N.° {d.NumeroReembolso}";
                    var subject = aprobado
                        ? $"Reembolso APROBADO - Consolidado del S10{numero}"
                        : $"Reembolso OBSERVADO - Consolidado del S10{numero}";

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
                _logger.LogError(ex, "Error avisando la decisión del reembolso de las salidas {Ids}",
                    string.Join(",", solicitudIds));
            }
        }

        /// <summary>
        /// Avisa a Tesorería que una planilla quedó firmada y su reembolso ya está en su bandeja
        /// (RF-TES-01). Los destinatarios salen del rol TESORERO y no de una lista escrita a mano;
        /// los de <c>Configuración → Correos</c> se suman como copia. Best-effort, igual que el
        /// resto: la firma ya está guardada.
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
                        + "sin destinatarios configurados o sin nadie con el rol de Tesorería.",
                        CorreoEventoCodigos.TesoreriaReembolso, rendicionId);
                    return;
                }

                var layout = SalidaEmailLayout.Desde(_configuration);
                // El botón abre el CONSOLIDADO: es la unidad de la bandeja de Tesorería, la misma
                // que se acaba de firmar. Sin consolidado vigente se cae a la bandeja sin abrir nada.
                var url    = info.ConsolidadoId is int consolidadoId
                    ? SalidaEnlaces.Reembolsos(_configuration, consolidadoId)
                    : SalidaEnlaces.Reembolsos(_configuration);

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
    }
}
