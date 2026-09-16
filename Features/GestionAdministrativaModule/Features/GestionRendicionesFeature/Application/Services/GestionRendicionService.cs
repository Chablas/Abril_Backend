using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Consolidadores.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Services
{
    public class GestionRendicionService : IGestionRendicionService
    {
        private readonly IGestionRendicionRepository    _repo;
        private readonly ISalidaVisibilityResolver      _visibilityResolver;
        private readonly IConsolidadorResolver          _consolidadorResolver;
        private readonly IConsolidadoS10Service         _consolidadoService;
        private readonly ICorreoSalidaRecipientResolver _correoResolver;
        private readonly IEmailService                  _emailService;
        private readonly IConfiguration                 _configuration;
        private readonly ILogger<GestionRendicionService> _logger;

        public GestionRendicionService(
            IGestionRendicionRepository repo,
            ISalidaVisibilityResolver visibilityResolver,
            IConsolidadorResolver consolidadorResolver,
            IConsolidadoS10Service consolidadoService,
            ICorreoSalidaRecipientResolver correoResolver,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<GestionRendicionService> logger)
        {
            _repo               = repo;
            _visibilityResolver = visibilityResolver;
            _consolidadorResolver = consolidadorResolver;
            _consolidadoService = consolidadoService;
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

        public async Task<SolicitudSalidaDetalleDto> GetSalidaDetalle(int solicitudId, GestionRendicionFiltersDto scope)
        {
            await ApplyVisibilityAsync(scope);
            return await _repo.GetSalidaDetalle(solicitudId, scope)
                ?? throw new AbrilException("La salida no existe o no está en tu alcance.", 404);
        }

        /// <summary>
        /// Qué correos dispararía la decisión de la primera revisión sobre la selección indicada, y
        /// a quién le llegarían. Se resuelve con las MISMAS llamadas que hace el envío
        /// (<see cref="NotificarPrimeraRevisionAsync"/>), así que la confirmación no puede prometer
        /// un correo que la configuración dejó fuera ni decir que no le llega a nadie cuando sí
        /// está activo.
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

                var preview = await _repo.GetPreviewPrimeraRevision(
                    request.RendicionIds, scope, conTrabajadores: request.Aprobar);

                var codigo = request.Aprobar
                    ? CorreoEventoCodigos.RendicionPrimeraAprobada
                    : CorreoEventoCodigos.RendicionPrimeraObservada;

                var avisos = new List<CorreoAvisoPreviewDto>();
                await AgregarAvisoAsync(avisos, "Al solicitante", codigo, preview.CorreosSolicitantes);

                // Aprobar además les avisa a los consolidadores del área (NotificarConsolidadoresAsync).
                // Sin planillas elegibles no sale ese correo, así que tampoco se anuncia.
                if (request.Aprobar && preview.WorkerIds.Count > 0)
                    await AgregarAvisoAsync(
                        avisos, "Al consolidador", CorreoEventoCodigos.RendicionPrimeraAprobadaConsolidador,
                        CorreosDeConsolidadores(
                            preview.WorkerIds,
                            await _consolidadorResolver.ResolveManyAsync(preview.WorkerIds)));

                return avisos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error resolviendo el preview de correos de la primera revisión");
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

            // Los avisos son best-effort: la decisión ya está guardada y no se revierte porque un
            // correo falle (mismo criterio que la decisión del reembolso). Los datos de cada planilla
            // se cargan una vez y sirven para sus solicitantes y, al aprobar, para sus consolidadores.
            var planillas = new List<List<PrimeraRevisionCorreoInfoDto>>(decididas.Count);
            foreach (var rendicionId in decididas)
            {
                var porTrabajador = await CargarCorreoInfoAsync(rendicionId);
                planillas.Add(porTrabajador);
                await NotificarPrimeraRevisionAsync(rendicionId, porTrabajador, aprobar);
            }

            if (aprobar)
                await NotificarConsolidadoresAsync(planillas);

            return new ReembolsoBulkResultDto
            {
                Procesadas = decididas.Count,
                Message = aprobar
                    ? $"{decididas.Count} rendición(es) aprobada(s) en primera revisión."
                    : $"{decididas.Count} rendición(es) observada(s).",
            };
        }

        public async Task<ConsolidadoS10Dto> UploadConsolidadoS10(
            IReadOnlyCollection<int> rendicionIds, IFormFile file, decimal montoTotal, string numeroReembolso, int userId)
        {
            var ids = rendicionIds?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0)
                throw new AbrilException("Selecciona al menos una rendición.", 400);

            // Ver las planillas no alcanza para consolidarlas: hay que ser consolidador de TODOS sus
            // trabajadores (el consolidado cubre los documentos enteros). El resolver es el mismo
            // que apaga el botón en la pantalla, así que acá no puede pasar nada que la UI no muestre.
            // El propio trabajador no consolida lo suyo: después de la primera revisión, el trámite
            // del S10 es del consolidador de su área.
            var workerIds = await _repo.GetWorkerIdsDePlanillas(ids);
            if (workerIds.Count == 0)
                throw new AbrilException(
                    ids.Count == 1
                        ? "La planilla de rendición no existe."
                        : "Las planillas de rendición seleccionadas no existen.", 404);

            var habilitado = await _consolidadorResolver.FiltrarQuePuedeConsolidarAsync(userId, workerIds);
            if (workerIds.Any(id => !habilitado.Contains(id)))
                throw new AbrilException(
                    (ids.Count == 1
                        ? "No estás habilitado para adjuntar el Consolidado del S10 de esta planilla. "
                        : "No estás habilitado para consolidar por todos los trabajadores de estas planillas. ")
                    + "Solo pueden hacerlo los consolidadores de su área (Consolidados → Configuración).", 403);

            return await _consolidadoService.UploadParaRendiciones(
                ids, file, montoTotal, numeroReembolso, userId);
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

            var vis = await _visibilityResolver.ResolveAsync(
                filters.CurrentUserId.Value, VisibilidadAmbitoIds.Rendiciones);
            filters.SeesAll             = vis.SeesAll;
            filters.VisibleAreaScopeIds = vis.AreaScopeIds.ToList();
        }

        // ── Correos de la primera revisión ───────────────────────────────────

        /// <summary>
        /// Lo que necesitan los correos de la decisión sobre una planilla, una entrada por
        /// trabajador. Best-effort como los envíos: si no se puede cargar, esa planilla se queda sin
        /// avisos en vez de tumbar una decisión que ya está guardada.
        /// </summary>
        private async Task<List<PrimeraRevisionCorreoInfoDto>> CargarCorreoInfoAsync(int rendicionId)
        {
            try
            {
                return await _repo.GetPrimeraRevisionCorreoInfo(rendicionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error cargando los datos de los correos de la primera revisión de la rendición {RendicionId}",
                    rendicionId);
                return new List<PrimeraRevisionCorreoInfoDto>();
            }
        }

        /// <summary>
        /// Le avisa a cada dueño de las salidas de la planilla cómo quedó su primera revisión: si
        /// se aprobó, que su rendición sigue con el consolidador de su área; si se observó, con qué
        /// comentario. Respeta la configuración de correos (Gestión de Rendiciones →
        /// Configuración → Correos): si está apagado o sin destinatarios, no se envía nada.
        ///
        /// Sale un correo POR TRABAJADOR y no uno por planilla: el documento puede agrupar a varias
        /// personas y cada una tiene que ver sus propios números para poder contrastarlos.
        /// </summary>
        private async Task NotificarPrimeraRevisionAsync(
            int rendicionId, List<PrimeraRevisionCorreoInfoDto> destinatarios, bool aprobada)
        {
            try
            {
                if (destinatarios.Count == 0) return;

                var codigo = aprobada
                    ? CorreoEventoCodigos.RendicionPrimeraAprobada
                    : CorreoEventoCodigos.RendicionPrimeraObservada;

                var layout = SalidaEmailLayout.Desde(_configuration);
                // El botón lleva a Mis Rendiciones: es donde el trabajador sigue su planilla y, si se
                // observó, corrige y la vuelve a generar.
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
                        TrayectosCount = info.TrayectosCount,
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

        /// <summary>
        /// Les avisa a los consolidadores del área que las planillas aprobadas se suman a las
        /// disponibles para el Consolidado del S10. Es informativo: el consolidador junta varias
        /// rendiciones y las consolida cuando le toca, así que el correo no le pide nada.
        ///
        /// Los consolidadores salen de <see cref="IConsolidadorResolver"/> —el mismo que después lo
        /// habilita a adjuntar el consolidado— por TODOS los trabajadores de cada planilla. Sale UN
        /// correo por grupo de destinatarios y no uno por planilla: aprobar en bloque varias planillas
        /// de la misma área le llegaría repetido al mismo consolidador.
        /// </summary>
        private async Task NotificarConsolidadoresAsync(List<List<PrimeraRevisionCorreoInfoDto>> planillas)
        {
            var conDatos = planillas.Where(p => p.Count > 0).ToList();
            if (conDatos.Count == 0) return;

            try
            {
                var consolidadores = await _consolidadorResolver.ResolveManyAsync(
                    conDatos.SelectMany(p => p.Select(i => i.WorkerId)).Distinct().ToList());

                var grupos = conDatos
                    .Select(p => new
                    {
                        Datos   = DatosDePlanilla(p),
                        Correos = CorreosDeConsolidadores(p.Select(i => i.WorkerId), consolidadores),
                    })
                    .GroupBy(x => string.Join(";", x.Correos.Select(c => c.ToLowerInvariant())))
                    .ToList();

                var layout = SalidaEmailLayout.Desde(_configuration);

                foreach (var grupo in grupos)
                {
                    var rendiciones = grupo.Select(x => x.Datos).OrderBy(d => d.Codigo, StringComparer.Ordinal).ToList();

                    var envio = await _correoResolver.ResolveEnvioAsync(
                        CorreoEventoCodigos.RendicionPrimeraAprobadaConsolidador, grupo.First().Correos);

                    if (!envio.Enviar)
                    {
                        _logger.LogInformation(
                            "Correo {Codigo} no enviado para las rendiciones {Codigos}: está apagado o sin destinatarios "
                            + "(sin consolidador resuelto ni destinatarios configurados).",
                            CorreoEventoCodigos.RendicionPrimeraAprobadaConsolidador,
                            string.Join(", ", rendiciones.Select(r => r.Codigo)));
                        continue;
                    }

                    var una = rendiciones.Count == 1;
                    var url = una
                        ? SalidaEnlaces.GestionRendiciones(_configuration, rendiciones[0].RendicionId)
                        : SalidaEnlaces.GestionRendiciones(_configuration);

                    var subject = una
                        ? $"Rendición disponible para consolidar - {rendiciones[0].Codigo} - {rendiciones[0].Trabajador}"
                        : $"{rendiciones.Count} rendiciones disponibles para consolidar";

                    await _emailService.SendAsync(
                        to: envio.Para,
                        subject: subject,
                        body: RendicionRevisionEmailTemplates.DisponiblesParaConsolidar(layout, rendiciones, url),
                        isHtml: true,
                        cc: envio.Copia.Count > 0 ? envio.Copia : null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error avisando a los consolidadores la aprobación de las rendiciones {Ids}",
                    string.Join(",", conDatos.Select(p => p[0].RendicionId)));
            }
        }

        /// <summary>
        /// Una planilla entera para el correo del consolidador: a él le importa el documento que va
        /// a consolidar, no el desglose por trabajador que recibe cada solicitante.
        /// </summary>
        private static RendicionRevisionCorreoDatos DatosDePlanilla(List<PrimeraRevisionCorreoInfoDto> porTrabajador)
        {
            var primera = porTrabajador[0];

            static string? Unir(IEnumerable<string?> valores)
            {
                var distintos = valores
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v!.Trim())
                    .Distinct()
                    .ToList();
                return distintos.Count == 0 ? null : string.Join(", ", distintos);
            }

            return new RendicionRevisionCorreoDatos
            {
                RendicionId    = primera.RendicionId,
                Codigo         = primera.Codigo,
                NumeroPlanilla = primera.NumeroPlanilla,
                Trabajador     = Unir(porTrabajador.Select(i => i.Trabajador)) ?? "Trabajador",
                Area           = Unir(porTrabajador.Select(i => i.Area)),
                Periodo        = Unir(porTrabajador.Select(i => i.Periodo)),
                SalidasCount   = porTrabajador.Sum(i => i.SalidasCount),
                TrayectosCount = porTrabajador.Sum(i => i.TrayectosCount),
                MontoTotal     = porTrabajador.Sum(i => i.MontoTotal),
                DecididoPor    = primera.DecididoPor,
            };
        }

        /// <summary>
        /// Correos de los consolidadores de esos trabajadores, sin repetir y en orden estable: el
        /// orden importa porque agrupa los envíos, y lo usa también el preview para anunciarlos.
        /// </summary>
        private static List<string> CorreosDeConsolidadores(
            IEnumerable<int> workerIds, IReadOnlyDictionary<int, List<ConsolidadorElegido>> consolidadores) =>
            workerIds
                .Distinct()
                .SelectMany(id => consolidadores.TryGetValue(id, out var lista) ? lista : new List<ConsolidadorElegido>())
                .Select(c => (c.Email ?? string.Empty).Trim())
                .Where(e => e.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
