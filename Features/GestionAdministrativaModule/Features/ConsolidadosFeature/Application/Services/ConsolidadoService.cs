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
        private readonly IConsolidadoS10Service         _consolidadoS10Service;
        private readonly ISalidaVisibilityResolver      _visibilityResolver;
        private readonly IFirmaPersonalRepository       _firmaRepository;
        private readonly IGraphSharePointService        _sharePointService;
        private readonly ICorreoSalidaRecipientResolver _correoResolver;
        private readonly IEmailService                  _emailService;
        private readonly IConfiguration                 _configuration;
        private readonly ILogger<ConsolidadoService>    _logger;

        public ConsolidadoService(
            IConsolidadoRepository repo,
            IConsolidadoS10Service consolidadoS10Service,
            ISalidaVisibilityResolver visibilityResolver,
            IFirmaPersonalRepository firmaRepository,
            IGraphSharePointService sharePointService,
            ICorreoSalidaRecipientResolver correoResolver,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<ConsolidadoService> logger)
        {
            _repo               = repo;
            _consolidadoS10Service = consolidadoS10Service;
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

                    case ConsolidadoCorreoAcciones.Reemplazo:
                    {
                        var plan = await _repo.GetReemplazoPlan(consolidadoId, scope, userId);
                        if (plan is { PuedeConsolidar: true } && plan.RendicionIdsAbiertas.Count > 0)
                            await AgregarAvisoAsync(
                                avisos, "A la jefatura", CorreoEventoCodigos.S10Revisor, plan.JefaturaEmails);
                        break;
                    }

                    default:
                    {
                        // Con una firma pendiente detrás —en obra el residente firma después del
                        // administrador— aprobar NO avisa al consolidador ni a Tesorería: el
                        // reembolso sigue Pendiente y el único correo que sale es el que le pasa el
                        // turno al que firma. Observar no firma nada, así que ahí no se pregunta.
                        var proxima = request.Aprobar
                            ? await _repo.GetProximaFirma(request.ConsolidadoIds, scope, userId)
                            : new ProximaFirmaDto { AlgunoSeCompleta = true };

                        if (proxima.AlgunoSeCompleta)
                        {
                            var consolidadores = await _repo.GetCorreosConsolidadorPorDecidir(
                                request.ConsolidadoIds, scope, userId);

                            await AgregarAvisoAsync(
                                avisos, "Al consolidador",
                                request.Aprobar ? CorreoEventoCodigos.ReembolsoAprobado : CorreoEventoCodigos.ReembolsoObservado,
                                consolidadores);

                            // Aprobar el reembolso ES firmar, y la firma completa es lo que mete la
                            // planilla en la bandeja de Tesorería: por eso dispara un segundo correo.
                            if (request.Aprobar)
                                await AgregarAvisoAsync(
                                    avisos, "A Tesorería",
                                    CorreoEventoCodigos.TesoreriaReembolso,
                                    await _repo.GetCorreosTesoreria());
                        }

                        if (proxima.Emails.Count > 0)
                            await AgregarAvisoAsync(
                                avisos, "A quien firma después",
                                CorreoEventoCodigos.S10Revisor, proxima.Emails);
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

            if (!aprobar)
            {
                var observadas = await _repo.ObservarReembolso(
                    ids, accion.Observacion ?? string.Empty, reviewerUserId);

                // El aviso al consolidador es best-effort: la decisión ya está guardada y no se
                // revierte porque un correo falle (mismo criterio que la aprobación de la salida).
                await NotificarDecisionAsync(observadas, aprobado: false);

                return new ReembolsoBulkResultDto
                {
                    Procesadas = observadas.Count,
                    Message    = $"{observadas.Count} reembolso(s) observado(s).",
                };
            }

            var firma = await AprobarFirmandoAsync(ids, reviewerUserId);

            // «Aprobado» se avisa solo cuando el reembolso quedó aprobado DE VERDAD: con la primera
            // de dos firmas el documento sigue esperando a la otra, y decirle al consolidador que
            // ya está —o a Tesorería que lo pague— sería falso.
            await NotificarDecisionAsync(firma.Completadas, aprobado: true);

            // El aviso a Tesorería es por CONSOLIDADO: es el documento que revisa y paga. Por planilla
            // le llegaba el mismo consolidado repetido tantas veces como planillas cubría.
            foreach (var consolidadoId in firma.ConsolidadosCompletados)
                await NotificarTesoreriaAsync(consolidadoId);

            // Las firmas van en cadena: al que sigue se le avisa recién ahora, con la anterior ya
            // puesta. Con el documento completo no queda nadie en turno y no sale ningún correo.
            if (firma.ConsolidadosFirmados.Count > 0)
            {
                // Quién acaba de firmar: es lo primero que el correo tiene que decirle al que sigue
                // («X ya firmó, falta tu firma»). Se resuelve una vez para todo el lote.
                var quienFirmo = (await _repo.GetFirmante(reviewerUserId)).Nombre;

                foreach (var consolidadoId in firma.ConsolidadosFirmados)
                    await AvisarSiguienteFirmanteAsync(consolidadoId, reviewerUserId, quienFirmo);
            }

            return new ReembolsoBulkResultDto
            {
                Procesadas        = firma.Completadas.Count,
                PlanillasFirmadas = firma.RendicionesCompletadas.Count,
                Message = firma.Completadas.Count > 0
                    ? $"{firma.Completadas.Count} reembolso(s) aprobado(s)."
                    : "Tu firma quedó estampada. El reembolso pasa a Tesorería cuando firme quien sigue.",
            };
        }

        /// <summary>
        /// Aprueba el reembolso FIRMANDO: estampa la firma del revisor —con su pie: puesto, nombre,
        /// fecha y hora— en todas las hojas de los documentos de cada planilla —su PDF, el
        /// Consolidado del S10 y la planilla grupal— y deja en "Firmado" —lo que Tesorería ve como
        /// pagable— las salidas cuyo documento ya no debe ninguna firma. Aprobar y firmar son el
        /// mismo acto: lo que el jefe respalda con su firma es justamente lo que está aprobando.
        ///
        /// En obra el consolidado lo firman DOS (el administrador y detrás el residente): con la
        /// primera firma el papel ya queda estampado pero el reembolso sigue Pendiente, esperando
        /// la otra.
        ///
        /// Los PDF se suben ANTES de escribir el estado: si algo falla en SharePoint no queda una
        /// salida aprobada sin su respaldo firmado (al revés solo deja archivos huérfanos, que no
        /// rompen nada).
        /// </summary>
        /// <returns>
        /// Qué quedó firmado y qué quedó además APROBADO: en obra el consolidado lleva dos firmas y
        /// con la primera el reembolso sigue Pendiente. Los avisos se disparan con eso.
        /// </returns>
        private async Task<ReembolsoFirmaResultDto> AprobarFirmandoAsync(List<int> ids, int userId)
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

            // El pie de la firma: puesto, nombre y el momento de la firma. Ese mismo momento es el
            // que queda guardado como fecha de firma, así el papel y la pantalla no discrepan.
            var firmante  = await _repo.GetFirmante(userId);
            var firmadoAt = DateTimeOffset.UtcNow;
            var pie = new SignaturePdfStamper.PieFirma(firmante.Nombre, firmante.Puesto, firmadoAt);

            var firmadas = new List<PlanillaFirmadaDto>(planillas.Count);
            // Un consolidado compartido por varias planillas de la selección se firma una sola vez,
            // junto con su planilla grupal.
            var consolidadosFirmados = new Dictionary<int, ConsolidadoFirmadoDto>();
            foreach (var p in planillas)
            {
                var firmada = new PlanillaFirmadaDto
                {
                    RendicionId  = p.RendicionId,
                    SolicitudIds = p.SolicitudIds,
                    // Lo que respalda a cada salida, también cuando no hay nada que estampar porque
                    // este usuario ya firmó ese consolidado: es contra eso que se mide si el
                    // documento reunió todas sus firmas.
                    ConsolidadoPorSolicitud = p.ConsolidadoPorSolicitud,
                    // Sin ningún consolidado que estampar tampoco hay nada que firmar en la
                    // planilla: este usuario ya la firmó en el mismo acto en que firmó el documento.
                    Planilla     = p.Consolidados.Count == 0
                        ? null
                        : await FirmarYSubirAsync(
                            carpeta, p.PlanillaUrl, p.PlanillaFilename, firma.Bytes, pie, p.PlanillaSlot),
                };

                foreach (var doc in p.Consolidados)
                {
                    if (!consolidadosFirmados.TryGetValue(doc.Id, out var firmado))
                    {
                        firmado = new ConsolidadoFirmadoDto
                        {
                            Slot = doc.Slot,
                            S10 = await FirmarYSubirAsync(
                                carpeta, doc.Url, doc.Filename, firma.Bytes, pie, doc.Slot),
                            Grupal = doc.GrupalUrl == null || doc.GrupalFilename == null
                                ? null
                                : await FirmarYSubirAsync(
                                    carpeta, doc.GrupalUrl, doc.GrupalFilename, firma.Bytes, pie, doc.GrupalSlot),
                        };
                        consolidadosFirmados[doc.Id] = firmado;
                    }
                    firmada.Consolidados[doc.Id] = firmado;
                }

                firmadas.Add(firmada);
            }

            return await _repo.AprobarReembolsoFirmado(firmadas, userId, firmadoAt);
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
        /// <param name="pie">Puesto, nombre y fecha que van impresos debajo de la firma.</param>
        /// <param name="slot">Lugar de la firma en la hoja (0 = la esquina de siempre).</param>
        private Task<ArchivoFirmadoDto> FirmarYSubirAsync(
            ShareLinkResolveDto carpeta, string pdfUrl, string pdfFilename, byte[] firmaPng,
            SignaturePdfStamper.PieFirma pie, int slot = 0)
            => FirmarYSubirAsync(carpeta, pdfUrl, pdfFilename, new[] { new Estampa(firmaPng, pie, slot) });

        /// <summary>Una firma a estampar: su imagen, su pie y el lugar que ocupa en la hoja.</summary>
        private sealed record Estampa(byte[] Firma, SignaturePdfStamper.PieFirma Pie, int Slot);

        /// <summary>
        /// Igual que la anterior pero con VARIAS firmas sobre el mismo PDF, en una sola bajada y una
        /// sola subida. Lo usa «Volver a firmar», que rehace la copia firmada desde el original y
        /// por eso tiene que volver a poner todas las firmas que el documento ya tenía.
        /// </summary>
        private async Task<ArchivoFirmadoDto> FirmarYSubirAsync(
            ShareLinkResolveDto carpeta, string pdfUrl, string pdfFilename,
            IReadOnlyList<Estampa> estampas)
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
                firmado = original;
                foreach (var e in estampas)
                    firmado = SignaturePdfStamper.Stamp(firmado, e.Firma, e.Pie, e.Slot);
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

        /// <summary>
        /// Vuelve a estampar la firma de este usuario sobre consolidados que ya firmó y que siguen
        /// esperando la de quien va detrás. Las copias firmadas se REHACEN desde el original, con
        /// las firmas que tenían y la suya al día: no se agrega una segunda estampa de la misma
        /// persona ni una fila de firma más, así que el documento sigue debiendo exactamente lo
        /// que debía y sus salidas no se mueven de estado.
        ///
        /// El guard (quién y hasta cuándo) vive en el repositorio, en el mismo lugar que decide el
        /// botón de la pantalla. Acá solo se arma el papel.
        /// </summary>
        public async Task<ReembolsoBulkResultDto> VolverAFirmar(
            ConsolidadoAccionDto accion, ConsolidadoFiltersDto scope, int userId)
        {
            if (accion.ConsolidadoIds.Count == 0)
                throw new AbrilException("Selecciona al menos un Consolidado del S10.", 400);

            await ApplyVisibilityAsync(scope);

            // Mismo criterio que aprobar: vale la firma del tipo que hoy pide Configuración → Firmas.
            var tiposHabilitados = (await _firmaRepository.GetTipos())
                .Where(t => t.Activo)
                .Select(t => t.Codigo)
                .ToList();

            var mia = await _firmaRepository.GetActiveBytesByUserId(userId, tiposHabilitados)
                ?? throw new AbrilException(
                    "Todavía no registraste tu firma. Regístrala una vez y vuelve a intentarlo.", 409);

            var documentos = await _repo.GetConsolidadosParaVolverAFirmar(accion.ConsolidadoIds, scope, userId);
            if (documentos.Count == 0)
                throw new AbrilException("No hay ningún consolidado que puedas volver a firmar.", 400);

            var carpeta   = await ResolverCarpetaRendicionesAsync();
            var firmante  = await _repo.GetFirmante(userId);
            var firmadoAt = DateTimeOffset.UtcNow;
            var miPie     = new SignaturePdfStamper.PieFirma(firmante.Nombre, firmante.Puesto, firmadoAt);

            var refirmados = new List<ConsolidadoRefirmadoDto>(documentos.Count);
            foreach (var doc in documentos)
            {
                // Como se parte del original hay que volver a poner TODAS las firmas vivas: la de
                // este usuario al día y las ajenas tal como estaban, con la fecha con la que se
                // firmaron. Si alguna no se puede recuperar se corta acá: rehacer el documento
                // perdiendo una firma sería peor que no rehacerlo.
                var estampas = new List<Estampa>(doc.Firmas.Count);
                foreach (var f in doc.Firmas)
                {
                    if (f.FirmadoPorId == userId)
                    {
                        estampas.Add(new Estampa(mia.Bytes, miPie, f.Slot));
                        continue;
                    }

                    var otra = await _firmaRepository.GetActiveBytesByUserId(f.FirmadoPorId, tiposHabilitados)
                        ?? throw new AbrilException(
                            "No se puede rehacer el documento: falta la firma registrada de alguien que ya lo firmó.",
                            409);

                    var quien = await _repo.GetFirmante(f.FirmadoPorId);
                    estampas.Add(new Estampa(
                        otra.Bytes,
                        new SignaturePdfStamper.PieFirma(quien.Nombre, quien.Puesto, f.FirmadoAt),
                        f.Slot));
                }

                var refirmado = new ConsolidadoRefirmadoDto
                {
                    ConsolidadoId = doc.Id,
                    S10 = await FirmarYSubirAsync(carpeta, doc.PdfUrl, doc.PdfFilename, estampas),
                    // La planilla grupal lleva las mismas firmas que el consolidado: se firman en
                    // el mismo acto.
                    Grupal = doc.GrupalUrl == null || doc.GrupalFilename == null
                        ? null
                        : await FirmarYSubirAsync(carpeta, doc.GrupalUrl, doc.GrupalFilename, estampas),
                };

                // Las planillas llevan las MISMAS firmas que su consolidado —se decide entero, así
                // que quien lo firma firma todas— y se rehacen igual: desde el original, con todas
                // las estampas en su lugar.
                foreach (var planilla in doc.Planillas)
                    refirmado.Planillas[planilla.RendicionId] = await FirmarYSubirAsync(
                        carpeta, planilla.PdfUrl, planilla.PdfFilename, estampas);

                refirmados.Add(refirmado);
            }

            var procesados = await _repo.RegistrarVolverAFirmar(refirmados, userId, firmadoAt);

            return new ReembolsoBulkResultDto
            {
                Procesadas        = procesados,
                PlanillasFirmadas = refirmados.Sum(r => r.Planillas.Count),
                Message = procesados == 1
                    ? "Tu firma se volvió a estampar."
                    : $"Tu firma se volvió a estampar en {procesados} consolidados.",
            };
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

            return await EnviarAvisoJefaturaAsync(info, consolidadoId, userId);
        }

        /// <summary>
        /// Manda el aviso «Reembolso por revisar» a quien tiene que firmar HOY —uno solo, porque las
        /// firmas van en cadena— y deja la marca de avisado en sus salidas. Lo comparten el botón
        /// del consolidador y el aviso automático que dispara la firma anterior, para que el correo
        /// y su marca sean los mismos por las dos vías.
        ///
        /// A diferencia de las decisiones, este aviso ES el correo: si está apagado se corta acá en
        /// vez de marcar un aviso que nunca salió.
        /// </summary>
        private async Task<string> EnviarAvisoJefaturaAsync(
            AvisoJefaturaInfoDto info, int consolidadoId, int userId)
        {
            var envio = await _correoResolver.ResolveEnvioAsync(
                CorreoEventoCodigos.S10Revisor, info.JefaturaEmails);

            if (!envio.Enviar)
                throw new AbrilException(
                    "El aviso a la jefatura está desactivado en Consolidados → Configuración → Correos.", 409);

            var url  = SalidaEnlaces.Consolidados(_configuration, consolidadoId);
            var body = ReembolsoEmailTemplates.ConsolidadoPorRevisar(
                SalidaEmailLayout.Desde(_configuration), info.Datos!, url);

            var numero = string.IsNullOrWhiteSpace(info.Datos!.NumeroReembolso)
                ? string.Empty
                : $" N.° {info.Datos.NumeroReembolso}";

            // El asunto dice de qué se trata antes de abrirlo: al que firma en segundo lugar le
            // llega «Falta tu firma» y no otro «por revisar» igual al que ya vio.
            var asunto = string.IsNullOrWhiteSpace(info.Datos.FirmoAntes)
                ? $"Reembolso por revisar - Consolidado del S10{numero}"
                : $"Falta tu firma - Consolidado del S10{numero}";

            await _emailService.SendAsync(
                to: envio.Para,
                subject: asunto,
                body: body,
                isHtml: true,
                cc: envio.Copia.Count > 0 ? envio.Copia : null);

            await _repo.MarcarJefaturaAvisada(info.SolicitudIds, userId);

            return $"Se le avisó a {ConsolidadoS10Agrupacion.Enumerar(info.JefaturaNombres)}.";
        }

        /// <summary>
        /// El aviso al SIGUIENTE firmante, que sale recién cuando el anterior firmó: en una obra el
        /// consolidado lo firman el administrador y después el residente, y al residente no se le
        /// avisa antes de que le toque —mismo criterio que las aprobaciones de GTH, donde el
        /// reemplazo le llega a GTH cuando el gerente del área ya lo aprobó—.
        ///
        /// Si el documento reunió todas sus firmas no queda nadie en turno y no sale nada. Es
        /// best-effort, igual que el resto de los avisos: la firma ya está guardada y estampada en
        /// el papel, así que un correo que no sale no la deshace.
        /// </summary>
        /// <param name="quienFirmo">
        /// Nombre del que acaba de firmar. Es lo que distingue este aviso del que manda el
        /// consolidador al adjuntar: al residente no le sirve enterarse de que el documento se
        /// adjuntó, sino de que el administrador de obra ya lo firmó y ahora le toca a él.
        /// </param>
        private async Task AvisarSiguienteFirmanteAsync(int consolidadoId, int userId, string? quienFirmo)
        {
            try
            {
                var info = await _repo.GetAvisoSiguienteFirmante(consolidadoId);
                if (info?.Datos == null || info.SolicitudIds.Count == 0 || info.JefaturaEmails.Count == 0)
                    return;

                info.Datos.FirmoAntes = quienFirmo;
                await EnviarAvisoJefaturaAsync(info, consolidadoId, userId);
            }
            catch (AbrilException ex)
            {
                _logger.LogInformation(
                    "Consolidado {ConsolidadoId} firmado, pero sin aviso al siguiente firmante: {Motivo}",
                    consolidadoId, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error avisando al siguiente firmante del consolidado {ConsolidadoId}", consolidadoId);
            }
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

        public async Task<ConsolidadoS10UploadResultDto> ReemplazarConsolidado(
            int consolidadoId, IFormFile file, decimal montoTotal, string numeroReembolso,
            ConsolidadoFiltersDto scope, int userId)
        {
            await ApplyVisibilityAsync(scope);

            var plan = await _repo.GetReemplazoPlan(consolidadoId, scope, userId)
                ?? throw new AbrilException("El Consolidado del S10 no existe o no está en tu alcance.", 404);

            if (!plan.PuedeConsolidar)
                throw new AbrilException(
                    "Solo el consolidador de estas rendiciones puede reemplazar el Consolidado del S10.", 403);

            if (plan.RendicionIdsAbiertas.Count == 0)
                throw new AbrilException(
                    "El reembolso de este consolidado ya está decidido: el Consolidado del S10 ya no se puede cambiar.", 409);

            // Las reglas del documento (primera revisión aprobada, monto contra las planillas
            // completas, herencia del código, reabrir lo observado y cerrar la corrección con el ERP)
            // son las mismas que al adjuntar: las aplica el servicio compartido.
            var consolidado = await _consolidadoS10Service.UploadParaRendiciones(
                plan.RendicionIdsAbiertas, file, montoTotal, numeroReembolso, userId);

            // Mismo aviso que al adjuntar desde Gestión de Rendiciones: nunca tumba el reemplazo.
            try
            {
                return new ConsolidadoS10UploadResultDto
                {
                    Consolidado     = consolidado,
                    JefaturaAvisada = true,
                    AvisoJefatura   = await NotificarJefatura(consolidado.Id, scope, userId),
                };
            }
            catch (AbrilException ex)
            {
                _logger.LogInformation(
                    "Consolidado {ConsolidadoId} reemplazado, pero sin aviso a la jefatura: {Motivo}",
                    consolidado.Id, ex.Message);
                return new ConsolidadoS10UploadResultDto
                {
                    Consolidado = consolidado, JefaturaAvisada = false, AvisoJefatura = ex.Message,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error avisando a la jefatura del consolidado {ConsolidadoId} recién reemplazado", consolidado.Id);
                return new ConsolidadoS10UploadResultDto
                {
                    Consolidado     = consolidado,
                    JefaturaAvisada = false,
                    AvisoJefatura   = "El consolidado quedó reemplazado, pero no se pudo avisar a la jefatura. "
                                    + "Puedes volver a intentarlo con «Avisar a la jefatura».",
                };
            }
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
            filters.SeesAll                = vis.SeesAll;
            filters.VisibleAreaScopeIds    = vis.AreaScopeIds.ToList();
            filters.TrabajadoresDeSusObras = vis.TrabajadoresDeSusObras.ToList();
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
        /// Avisa a Tesorería que un consolidado quedó firmado y su reembolso ya está en su bandeja
        /// (RF-TES-01): UN correo por consolidado, con el resumen del documento entero. Los
        /// destinatarios salen del rol TESORERO y no de una lista escrita a mano; los de
        /// <c>Configuración → Correos</c> se suman como copia. Best-effort, igual que el resto: la
        /// firma ya está guardada.
        /// </summary>
        private async Task NotificarTesoreriaAsync(int consolidadoId)
        {
            try
            {
                var info = await _repo.GetTesoreriaCorreoInfo(consolidadoId);
                if (info == null) return;

                var envio = await _correoResolver.ResolveEnvioAsync(
                    CorreoEventoCodigos.TesoreriaReembolso, info.Destinatarios);

                if (!envio.Enviar)
                {
                    _logger.LogInformation(
                        "Correo {Codigo} no enviado para el consolidado {ConsolidadoId}: está apagado, "
                        + "sin destinatarios configurados o sin nadie con el rol de Tesorería.",
                        CorreoEventoCodigos.TesoreriaReembolso, consolidadoId);
                    return;
                }

                var layout = SalidaEmailLayout.Desde(_configuration);
                // El botón abre el consolidado en Reembolsos: es la unidad de la bandeja de Tesorería.
                var url    = SalidaEnlaces.Reembolsos(_configuration, consolidadoId);

                // Los consolidados anteriores al código se nombran por su número de reembolso.
                var d = info.Datos;
                var nombre = !string.IsNullOrWhiteSpace(d.Codigo) ? $" - {d.Codigo}"
                           : !string.IsNullOrWhiteSpace(d.NumeroReembolso) ? $" - N.° {d.NumeroReembolso}"
                           : string.Empty;

                await _emailService.SendAsync(
                    to: envio.Para,
                    subject: $"Consolidado pendiente de revisión{nombre}",
                    body: ReembolsoEmailTemplates.ConsolidadoParaTesoreria(layout, d, url),
                    isHtml: true,
                    cc: envio.Copia.Count > 0 ? envio.Copia : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error avisando a Tesorería del consolidado firmado {ConsolidadoId}", consolidadoId);
            }
        }
    }
}
