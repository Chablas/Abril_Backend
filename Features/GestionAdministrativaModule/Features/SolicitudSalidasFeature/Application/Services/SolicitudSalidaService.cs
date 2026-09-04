using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Services
{
    public class SolicitudSalidaService : ISolicitudSalidaService
    {
        private readonly ISolicitudSalidaRepository    _repo;
        private readonly IJefeRevisorResolver          _revisorResolver;
        private readonly ICorreoSalidaRecipientResolver _correoResolver;
        private readonly ISolicitudSalidaTokenService  _tokenService;
        private readonly IEmailService                 _emailService;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IConfiguration                _configuration;
        private readonly ILogger<SolicitudSalidaService> _logger;
        private readonly IGraphSharePointService        _sharePointService;
        private readonly IConsolidadoS10Service         _consolidadoService;

        public SolicitudSalidaService(
            ISolicitudSalidaRepository repo,
            IJefeRevisorResolver revisorResolver,
            ICorreoSalidaRecipientResolver correoResolver,
            ISolicitudSalidaTokenService tokenService,
            IEmailService emailService,
            IDbContextFactory<AppDbContext> factory,
            IConfiguration configuration,
            ILogger<SolicitudSalidaService> logger,
            IGraphSharePointService sharePointService,
            IConsolidadoS10Service consolidadoService)
        {
            _repo             = repo;
            _revisorResolver  = revisorResolver;
            _correoResolver   = correoResolver;
            _tokenService     = tokenService;
            _emailService     = emailService;
            _factory          = factory;
            _configuration    = configuration;
            _logger           = logger;
            _sharePointService = sharePointService;
            _consolidadoService = consolidadoService;
        }

        public async Task<SolicitudSalidaFormDataDto> GetFormData(int? userId)
        {
            var data = await _repo.GetFormData();

            if (userId.HasValue)
            {
                try
                {
                    using var ctx = _factory.CreateDbContext();
                    var solicitante = await ctx.Worker
                        .Where(w => w.Person != null && w.Person.UserId == userId.Value)
                        .FirstOrDefaultAsync();
                    if (solicitante != null)
                    {
                        // Correo del revisor (best-effort): workers_revisores → fallback GTH.
                        var revisor = await _revisorResolver.ResolveAsync(solicitante.Id);
                        data.AprobadorEmail = revisor?.Email;

                        // Si el trabajador es TI, exponer el catálogo de trayectos para que el
                        // frontend muestre el monto automático al seleccionar origen+destino.
                        if (string.Equals(solicitante.Subarea, "Tecnología de la Información", StringComparison.OrdinalIgnoreCase))
                        {
                            data.EsTI = true;
                            data.TrayectosCatalogo = await ctx.GaTrayecto
                                .Where(g => g.Activo)
                                .Select(g => new TrayectoCatalogoOptionDto
                                {
                                    LugarOrigenId  = g.LugarOrigenId,
                                    LugarDestinoId = g.LugarDestinoId,
                                    Monto          = g.Monto,
                                })
                                .ToListAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo resolver el aprobador/catálogo para userId {UserId}", userId);
                }
            }

            return data;
        }

        public Task<List<SolicitudSalidaListItemDto>> GetByUserId(int userId, SolicitudSalidaFiltersDto? filters = null) =>
            _repo.GetByUserId(userId, filters);

        public async Task<SolicitudSalidaFilterDataDto> GetFilterData(int userId)
        {
            var data = await _repo.GetFilterData(userId);

            // Meses del desplegable "Mes a rendir" + números de las tarjetas. Van acá y no en el
            // listado porque son de TODAS las solicitudes del trabajador, no del filtro de la
            // tabla: si cambiaran al filtrar dejarían de servir como bandeja pendiente. La pantalla
            // vuelve a pedir filter-data después de cada acción que los mueve.
            var pendientes = await _repo.GetByUserId(userId, new SolicitudSalidaFiltersDto
            {
                EstadoAprobacion = EstadosSalida.Aprobacion.NombreAprobado,
                EstadoRendicion  = EstadosSalida.Rendicion.NombreNoRendido,
            });
            var aptas = pendientes.Where(x => x.AptaParaRendir).ToList();

            // Los meses vencidos no aparecen: `AptaParaRendir` ya los descarta fila por fila, así
            // que un periodo cerrado se queda sin aptas y cae solo del desplegable.
            var calendario = await _repo.GetCalendarioNoLaborable();
            data.MesesRendicion = aptas
                .GroupBy(x => (x.FechaSalida.Year, x.FechaSalida.Month))
                .Select(g => new MesRendicionDto
                {
                    Anio        = g.Key.Year,
                    Mes         = g.Key.Month,
                    Label       = EtiquetaMes(g.Key.Year, g.Key.Month),
                    Cantidad    = g.Count(),
                    FechaLimite = calendario.LimiteDeRendicion(g.Key.Year, g.Key.Month),
                })
                .OrderByDescending(m => m.Anio).ThenByDescending(m => m.Mes)
                .ToList();

            // El mes anterior se agrega aunque no tenga nada —es el periodo que se rinde por
            // defecto— pero SOLO si su plazo sigue abierto: ofrecer un periodo cerrado sería
            // ofrecer una acción que el backend va a rechazar.
            var (desdeMesAnterior, _) = MesAnteriorPeru.Rango();
            if (!calendario.PlazoVencido(desdeMesAnterior.Year, desdeMesAnterior.Month)
                && !data.MesesRendicion.Any(m => m.Anio == desdeMesAnterior.Year && m.Mes == desdeMesAnterior.Month))
            {
                data.MesesRendicion.Add(new MesRendicionDto
                {
                    Anio        = desdeMesAnterior.Year,
                    Mes         = desdeMesAnterior.Month,
                    Label       = EtiquetaMes(desdeMesAnterior.Year, desdeMesAnterior.Month),
                    Cantidad    = 0,
                    FechaLimite = calendario.LimiteDeRendicion(desdeMesAnterior.Year, desdeMesAnterior.Month),
                });
                data.MesesRendicion = data.MesesRendicion
                    .OrderByDescending(m => m.Anio).ThenByDescending(m => m.Mes)
                    .ToList();
            }

            // Las observadas están en otro eje de estado (reembolso rechazado sobre una salida ya
            // rendida), así que no salen del set anterior y se piden aparte, ya filtradas.
            var observadas = await _repo.GetByUserId(userId, new SolicitudSalidaFiltersDto
            {
                EstadoReembolso = EstadosSalida.Reembolso.NombreRechazado,
            });

            data.Resumen = new ResumenRendicionDto
            {
                AptasParaRendir     = aptas.Count,
                CapturasIncompletas = pendientes.Count(x => !x.PuedeRendirse),
                Observadas          = observadas.Count,
            };

            return data;
        }

        /// <summary>"Agosto 2026" — el nombre del mes en español, con la primera letra en mayúscula.</summary>
        private static string EtiquetaMes(int anio, int mes)
        {
            var cultura = CultureInfo.GetCultureInfo("es-PE");
            var nombre  = cultura.DateTimeFormat.GetMonthName(mes);
            return $"{char.ToUpper(nombre[0], cultura)}{nombre[1..]} {anio}";
        }

        public async Task<int> Create(SolicitudSalidaCreateDto dto, int? userId, IReadOnlyList<(int TrayectoIndex, IFormFile File)>? adjuntos = null)
        {
            if (dto.Trayectos == null || dto.Trayectos.Count == 0)
                throw new AbrilException("Debe registrar al menos un trayecto.", 400);

            // 1. Exigencias de los motivos elegidos (un solo roundtrip): qué trayectos
            //    necesitan documento adjunto, cuáles necesitan motivo adicional y cuáles no
            //    declaran horario ni lugares. Se carga ANTES de validar porque es el motivo el
            //    que decide qué campos son obligatorios.
            var exigencias = await CargarExigenciasMotivosAsync(dto);

            // Un motivo con pide_horas_lugares = false describe una ausencia de día completo
            // (ej. licencia sin goce de haber), no un desplazamiento: no lleva horas ni lugares y
            // no admite trayectos adicionales. El motivo libre ("Otro motivo") siempre los pide.
            bool PideHorasLugares(TrayectoCreateDto t) =>
                !t.MotivoId.HasValue
                || !exigencias.TryGetValue(t.MotivoId.Value, out var e)
                || e.PideHorasLugares;

            if (dto.Trayectos.Count > 1 && dto.Trayectos.Any(t => !PideHorasLugares(t)))
                throw new AbrilException(
                    "El motivo seleccionado no admite más de un trayecto: registra la solicitud con un solo trayecto.", 400);

            // Validar cada trayecto
            for (int i = 0; i < dto.Trayectos.Count; i++)
            {
                var t = dto.Trayectos[i];
                var pos = i + 1;

                var tieneMotivoId    = t.MotivoId.HasValue;
                var tieneMotivoLibre = !string.IsNullOrWhiteSpace(t.MotivoLibre);
                if (!tieneMotivoId && !tieneMotivoLibre)
                    throw new AbrilException($"Trayecto {pos}: debe indicar un motivo.", 400);

                // El horario y los lugares pertenecen al motivo: si el motivo no los pide, no se
                // guarda nada aunque el cliente los haya mandado (ej. cambió de motivo sin limpiar).
                if (!PideHorasLugares(t))
                {
                    t.HoraSalida        = null;
                    t.HoraRetorno       = null;
                    t.LugarOrigenId     = null;
                    t.LugarOrigenLibre  = null;
                    t.LugarDestinoId    = null;
                    t.LugarDestinoLibre = null;
                    continue;
                }

                if (!t.HoraSalida.HasValue)
                    throw new AbrilException($"Trayecto {pos}: debe indicar la hora de salida.", 400);
                if (t.HoraRetorno.HasValue && t.HoraRetorno.Value <= t.HoraSalida.Value)
                    throw new AbrilException($"Trayecto {pos}: la hora de retorno debe ser posterior a la hora de salida.", 400);

                var tieneOrigenId    = t.LugarOrigenId.HasValue;
                var tieneOrigenLibre = !string.IsNullOrWhiteSpace(t.LugarOrigenLibre);
                if (!tieneOrigenId && !tieneOrigenLibre)
                    throw new AbrilException($"Trayecto {pos}: debe indicar un lugar de origen.", 400);

                var tieneDestinoId    = t.LugarDestinoId.HasValue;
                var tieneDestinoLibre = !string.IsNullOrWhiteSpace(t.LugarDestinoLibre);
                if (!tieneDestinoId && !tieneDestinoLibre)
                    throw new AbrilException($"Trayecto {pos}: debe indicar un lugar de destino.", 400);

                if (tieneOrigenId && tieneDestinoId && t.LugarOrigenId == t.LugarDestinoId)
                    throw new AbrilException($"Trayecto {pos}: el lugar de origen y el lugar de destino no pueden ser iguales.", 400);
            }

            // 2. Motivo adicional: se valida antes de subir nada a SharePoint para que una
            //    solicitud inválida no deje archivos huérfanos en la carpeta.
            for (int i = 0; i < dto.Trayectos.Count; i++)
            {
                var t = dto.Trayectos[i];
                var exige = t.MotivoId.HasValue
                         && exigencias.TryGetValue(t.MotivoId.Value, out var e)
                         && e.RequiereMotivoAdicional;

                if (exige && string.IsNullOrWhiteSpace(t.MotivoAdicional))
                    throw new AbrilException($"Trayecto {i + 1}: el motivo seleccionado requiere que indiques un motivo adicional.", 400);

                // El detalle pertenece al motivo que lo exige: si el motivo no lo pide, no se
                // guarda nada aunque el cliente lo haya mandado (ej. cambió de motivo y no limpió).
                t.MotivoAdicional = exige ? t.MotivoAdicional!.Trim() : null;
            }

            // 3. Subir los documentos adjuntos ANTES de persistir: los motivos con
            //    requiere_adjunto exigen archivo, y así una falla de subida no deja
            //    solicitudes creadas sin su adjunto obligatorio.
            var adjuntosPorIndice = await SubirAdjuntosAsync(dto, adjuntos, exigencias);

            // 4. Persistir solicitud + trayectos (con las referencias de los adjuntos)
            var (solicitud, trayectos, solicitante) = await _repo.Create(dto, userId, adjuntosPorIndice);

            // 5. Resolver aprobador + enviar emails (best-effort)
            //    Hacemos UNA sola resolución de trayectos en memoria y la compartimos
            //    entre los dos correos (al aprobador y de confirmación al solicitante).
            try
            {
                using var ctx = _factory.CreateDbContext();
                var nombreSolicitante = await ctx.Person
                    .Where(p => p.PersonId == solicitante.PersonId)
                    .Select(p => p.FullName)
                    .FirstOrDefaultAsync() ?? "Trabajador";

                var (trayectosResueltos, mostrarRecordatorio) = await ResolveTrayectosForEmailAsync(ctx, trayectos);

                // 5a. Email al revisor resuelto (workers_revisores → fallback GTH). Se guarda
                //     el correo al que se envió (enviado_a_correo); el aprobador real
                //     (aprobador_worker_id / aprobador_area_scope_id) se llena recién al
                //     momento de la decisión. enviado_a_correo se guarda apenas se resuelve el
                //     revisor aunque su interruptor esté apagado y no le llegue el correo: sigue
                //     siendo el revisor asignado y de ahí sale su visibilidad en Gestión de Salidas.
                var revisor = await _revisorResolver.ResolveAsync(solicitante.Id);
                var aprobadorEmail = revisor?.Email;
                if (!string.IsNullOrWhiteSpace(aprobadorEmail))
                    await _repo.SetEnviadoACorreo(solicitud.Id, aprobadorEmail);
                else
                    _logger.LogWarning(
                        "No se pudo resolver revisor para solicitud {SolicitudId} (worker {WorkerId}): sin revisores en workers_revisores y sin correo GTH configurado en area_scope.email",
                        solicitud.Id, solicitante.Id);

                var enviadoRevisorA = await SendNotificacionAprobadorAsync(
                    solicitud, trayectosResueltos, mostrarRecordatorio, aprobadorEmail, nombreSolicitante, adjuntos);

                // 5b. Email de confirmación al solicitante (al mismo usuario que registró la solicitud)
                if (userId.HasValue)
                {
                    try
                    {
                        await SendConfirmacionSolicitanteAsync(ctx, solicitud, trayectosResueltos, mostrarRecordatorio, nombreSolicitante, userId.Value, aprobadorEmail, enviadoRevisorA);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error enviando confirmación al solicitante para solicitud {SolicitudId}", solicitud.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando emails para solicitud {SolicitudId}", solicitud.Id);
            }

            return solicitud.Id;
        }

        /// <summary>
        /// Lee de una sola vez lo que exigen los motivos del catálogo elegidos en la solicitud:
        /// documento adjunto, motivo adicional y/o horario y lugares. Devuelve un diccionario
        /// motivoId → exigencias (vacío si todos los trayectos usan "Otro motivo").
        /// </summary>
        private async Task<Dictionary<int, (bool RequiereAdjunto, bool RequiereMotivoAdicional, bool PideHorasLugares)>>
            CargarExigenciasMotivosAsync(SolicitudSalidaCreateDto dto)
        {
            var motivoIds = dto.Trayectos.Where(t => t.MotivoId.HasValue).Select(t => t.MotivoId!.Value).Distinct().ToList();
            if (motivoIds.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();
            var filas = await ctx.GaMotivoSalida
                .Where(m => motivoIds.Contains(m.Id))
                .Select(m => new { m.Id, m.RequiereAdjunto, m.RequiereMotivoAdicional, m.PideHorasLugares })
                .ToListAsync();

            return filas.ToDictionary(m => m.Id, m => (m.RequiereAdjunto, m.RequiereMotivoAdicional, m.PideHorasLugares));
        }

        /// <summary>
        /// Valida y sube los documentos adjuntos de la solicitud (N por trayecto cuyo motivo
        /// tenga requiere_adjunto = true) a la carpeta configurada de SharePoint/OneDrive
        /// (ga_adjunto_folder). Devuelve el resultado por índice de trayecto (0-based) —
        /// cada trayecto puede tener varios— o null si la solicitud no trae ningún adjunto ni lo necesita.
        /// </summary>
        private async Task<Dictionary<int, List<TrayectoAdjuntoSubidoDto>>?> SubirAdjuntosAsync(
            SolicitudSalidaCreateDto dto,
            IReadOnlyList<(int TrayectoIndex, IFormFile File)>? adjuntos,
            IReadOnlyDictionary<int, (bool RequiereAdjunto, bool RequiereMotivoAdicional, bool PideHorasLugares)> exigencias)
        {
            var files = (adjuntos ?? Array.Empty<(int, IFormFile)>())
                .Where(a => a.File != null && a.File.Length > 0)
                .ToList();

            if (files.Any(a => a.TrayectoIndex < 0 || a.TrayectoIndex >= dto.Trayectos.Count))
                throw new AbrilException("Adjunto asociado a un trayecto inexistente.", 400);

            // Agrupamos por índice de trayecto: un trayecto puede traer N documentos.
            var porIndice = files
                .GroupBy(a => a.TrayectoIndex)
                .ToDictionary(g => g.Key, g => g.Select(a => a.File).ToList());

            // Motivos que exigen adjunto: todo trayecto con uno de esos motivos debe traer al menos un archivo.
            for (int i = 0; i < dto.Trayectos.Count; i++)
            {
                var t = dto.Trayectos[i];
                if (t.MotivoId.HasValue &&
                    exigencias.TryGetValue(t.MotivoId.Value, out var e) && e.RequiereAdjunto &&
                    (!porIndice.TryGetValue(i, out var lst) || lst.Count == 0))
                    throw new AbrilException($"Trayecto {i + 1}: el motivo seleccionado requiere al menos un documento adjunto.", 400);
            }

            if (porIndice.Count == 0) return null;

            // Validar tipos permitidos (documento de prueba: PDF o imagen).
            var allowed = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };
            foreach (var f in porIndice.Values.SelectMany(l => l))
            {
                var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext))
                    throw new AbrilException($"Tipo de archivo no permitido: {f.FileName}. Solo PDF/JPG/PNG/WEBP.", 400);
            }

            // Carpeta destino configurada (Configuración → Carpeta Adjuntos).
            string driveId, folderId;
            using (var ctx = _factory.CreateDbContext())
            {
                var carpeta = await ctx.GaAdjuntoFolder
                    .Where(f => f.State && f.Active)
                    .OrderBy(f => f.GaAdjuntoFolderId)
                    .Select(f => new { f.DriveId, f.FolderId })
                    .FirstOrDefaultAsync()
                    ?? throw new AbrilException(
                        "No se ha configurado la carpeta donde guardar los documentos adjuntos. " +
                        "Pide al administrador configurarla en Configuración → Carpeta Adjuntos.", 409);
                driveId = carpeta.DriveId;
                folderId = carpeta.FolderId;
            }

            var resultado = new Dictionary<int, List<TrayectoAdjuntoSubidoDto>>();
            try
            {
                foreach (var (idx, lista) in porIndice.OrderBy(kv => kv.Key))
                {
                    var subidos = new List<TrayectoAdjuntoSubidoDto>(lista.Count);
                    for (int n = 0; n < lista.Count; n++)
                    {
                        var f        = lista[n];
                        var ext      = Path.GetExtension(f.FileName).ToLowerInvariant();
                        var safeName = SanitizeFilename(Path.GetFileNameWithoutExtension(f.FileName));
                        var stamp    = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                        var filename = $"adjunto_{stamp}_t{idx + 1}_{n + 1}_{safeName}{ext}";

                        using var stream = f.OpenReadStream();
                        var result = await _sharePointService.UploadToOneDriveFolderAsync(
                            driveId, folderId, filename, stream,
                            f.ContentType ?? "application/octet-stream",
                            autoRenameOnLock: true);

                        if (result?.WebUrl is null)
                            throw new AbrilException($"No se pudo subir el documento {f.FileName}.", 502);

                        subidos.Add(new TrayectoAdjuntoSubidoDto
                        {
                            Url      = result.WebUrl,
                            ItemId   = result.ItemId,
                            DriveId  = driveId,
                            Filename = result.FileName ?? filename,
                        });
                    }
                    resultado[idx] = subidos;
                }
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la subida de adjuntos de la solicitud de salida");
                throw new AbrilException("Error al subir los documentos adjuntos a SharePoint.", 502);
            }

            return resultado;
        }

        // ── Aprobar / Rechazar desde el email ────────────────────────────────

        public async Task<string> ProcessAprobarFromEmail(string token)
        {
            var payload = _tokenService.Validate(token);
            if (payload == null || payload.Action != SolicitudSalidaAction.Aprobar)
                return RenderResultPage("Enlace inválido o expirado", "El enlace ya no es válido. Solicita uno nuevo al trabajador.", isSuccess: false);

            var s = await _repo.Aprobar(payload.SolicitudId);
            if (s == null)
                return RenderResultPage("Solicitud ya procesada", "Esta solicitud ya había sido aprobada o rechazada anteriormente.", isSuccess: false);

            // Notificar al solicitante (best-effort, no rompe la respuesta HTML)
            await NotifySolicitanteAprobada(s.Id);

            return RenderResultPage("Solicitud aprobada", $"Has aprobado la solicitud de salida {Identificador(s)}.", isSuccess: true);
        }

        public async Task<string> ProcessRechazarFromEmail(string token, string? motivoRechazo)
        {
            var payload = _tokenService.Validate(token);
            if (payload == null || payload.Action != SolicitudSalidaAction.Rechazar)
                return RenderResultPage("Enlace inválido o expirado", "El enlace ya no es válido. Solicita uno nuevo al trabajador.", isSuccess: false);

            var s = await _repo.Rechazar(payload.SolicitudId, motivoRechazo);
            if (s == null)
                return RenderResultPage("Solicitud ya procesada", "Esta solicitud ya había sido aprobada o rechazada anteriormente.", isSuccess: false);

            // Notificar al solicitante (best-effort, no rompe la respuesta HTML)
            await NotifySolicitanteRechazada(s.Id);

            return RenderResultPage("Solicitud rechazada", $"Has rechazado la solicitud de salida {Identificador(s)}.", isSuccess: true);
        }

        /// <summary>
        /// Identificador de la solicitud para las páginas que abre el revisor desde el correo: su
        /// código SOL-AAAA-NNNN, o "#id" en las anteriores al código (ahí el id es lo único que hay
        /// a mano y el revisor no lo ve en ningún otro lado).
        /// </summary>
        private static string Identificador(GaSolicitudSalida s) =>
            string.IsNullOrWhiteSpace(s.Codigo) ? $"#{s.Id}" : s.Codigo;

        public string RenderRechazarForm(string token)
        {
            var payload = _tokenService.Validate(token);
            if (payload == null || payload.Action != SolicitudSalidaAction.Rechazar)
                return RenderResultPage("Enlace inválido o expirado", "El enlace ya no es válido.", isSuccess: false);

            var safeToken = WebUtility.HtmlEncode(token);
            return RenderPaginaRevisor(
                "Rechazar solicitud",
                $@"<p style=""margin:0 0 14px;font-size:15px"">Indica el motivo del rechazo (opcional):</p>
  <form method=""post"" action=""rechazar"">
    <input type=""hidden"" name=""token"" value=""{safeToken}"" />
    <textarea name=""motivoRechazo"" placeholder=""Ej: La fecha coincide con una reunión importante...""></textarea>
    <button type=""submit"">Confirmar rechazo</button>
  </form>",
                "#991B1B");
        }

        // ── Helpers internos ─────────────────────────────────────────────────

        /// <summary>
        /// Resuelve los trayectos de la entidad a tuplas listas para embeber en el cuerpo del email.
        /// Hace lookups por motivo y por lugar (origen/destino) para mostrar nombres legibles. Los
        /// motivos con motivo adicional se muestran como "Motivo — detalle": el correo tiene una
        /// sola fila de motivo y el detalle es justamente lo que el revisor necesita leer.
        /// Devuelve además si corresponde mostrar el recordatorio de recuperación de horas:
        /// solo se muestra cuando al menos un trayecto tiene un motivo del catálogo de hora
        /// exacta. Los motivos de hora estimada y el motivo libre (personalizado) quedan
        /// excluidos a propósito y nunca lo disparan — misma regla que el formulario.
        /// </summary>
        private static async Task<(List<(int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino)> Trayectos, bool MostrarRecordatorio)>
            ResolveTrayectosForEmailAsync(AppDbContext ctx, List<GaSolicitudTrayecto> trayectos)
        {
            var resueltos = new List<(int, string, string, string, string, string)>();
            var algunaHoraExacta = false;
            foreach (var t in trayectos.OrderBy(t => t.Orden))
            {
                string motivo;
                if (t.MotivoId.HasValue)
                {
                    var m = await ctx.GaMotivoSalida
                        .Where(m => m.Id == t.MotivoId.Value)
                        .Select(m => new { m.Descripcion, m.EsHoraEstimada, m.PideHorasLugares })
                        .FirstOrDefaultAsync();
                    motivo = m?.Descripcion ?? "—";
                    if (!string.IsNullOrWhiteSpace(t.MotivoAdicional))
                        motivo = $"{motivo} — {t.MotivoAdicional}";
                    // Un motivo que no declara horario tampoco genera horas que recuperar.
                    if (m == null || (!m.EsHoraEstimada && m.PideHorasLugares)) algunaHoraExacta = true;
                }
                else
                {
                    // Motivo personalizado (libre): NO dispara el recordatorio de recuperación
                    // de horas — se omite tanto en el formulario como en los correos.
                    motivo = t.MotivoLibre ?? "—";
                }

                // Los motivos que no piden horario ni lugares dejan estos campos en "": el
                // bloque del correo omite la fila entera en vez de mostrarla vacía o con un guión.
                var tieneOrigen  = t.LugarOrigenId.HasValue  || !string.IsNullOrWhiteSpace(t.LugarOrigenLibre);
                var tieneDestino = t.LugarDestinoId.HasValue || !string.IsNullOrWhiteSpace(t.LugarDestinoLibre);
                var origen  = tieneOrigen  ? await ResolveLugarDisplay(ctx, t.LugarOrigenId,  t.LugarOrigenLibre)  : "";
                var destino = tieneDestino ? await ResolveLugarDisplay(ctx, t.LugarDestinoId, t.LugarDestinoLibre) : "";
                var horaSal = t.HoraSalida.HasValue ? t.HoraSalida.Value.ToString("HH:mm") : "";
                var horaRet = t.HoraRetorno.HasValue ? t.HoraRetorno.Value.ToString("HH:mm")
                            : t.HoraSalida.HasValue  ? "Sin retorno"
                            : "";
                resueltos.Add((t.Orden + 1, horaSal, horaRet, motivo, origen, destino));
            }
            return (resueltos, algunaHoraExacta);
        }

        /// <summary>
        /// Envía el correo con los botones de aprobar/rechazar. El revisor resuelto es su
        /// destinatario principal, pero la configuración manda: si el correo está apagado, o si
        /// el revisor está apagado y no hay ningún destinatario configurado, no se envía nada.
        /// Devuelve los correos a los que realmente se envió (vacío si no se envió).
        /// </summary>
        private async Task<List<string>> SendNotificacionAprobadorAsync(
            GaSolicitudSalida solicitud,
            List<(int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino)> trayectos,
            bool mostrarRecordatorio,
            string? aprobadorEmail,
            string nombreSolicitante,
            IReadOnlyList<(int TrayectoIndex, IFormFile File)>? adjuntos = null)
        {
            // El revisor (To) nunca se excluye; las exclusiones solo aplican a los correos en copia.
            var envio = await _correoResolver.ResolveEnvioAsync(
                CorreoEventoCodigos.Revisor,
                string.IsNullOrWhiteSpace(aprobadorEmail) ? null : new List<string> { aprobadorEmail });

            if (!envio.Enviar)
            {
                _logger.LogInformation(
                    "Correo al revisor no enviado para solicitud {SolicitudId}: el correo está apagado o no quedó ningún destinatario configurado.",
                    solicitud.Id);
                return new List<string>();
            }

            // Los documentos adjuntos de la solicitud van SOLO en este correo (revisor); la
            // confirmación al solicitante y demás correos no los llevan. Se arman recién acá
            // para no leer los archivos si al final no se envía nada.
            var adjuntosEmail = await BuildAdjuntosEmailAsync(adjuntos);

            var tokenAprobar  = _tokenService.Generate(solicitud.Id, SolicitudSalidaAction.Aprobar,  TimeSpan.FromDays(30));
            var tokenRechazar = _tokenService.Generate(solicitud.Id, SolicitudSalidaAction.Rechazar, TimeSpan.FromDays(30));

            var backendUrl = (_configuration["BackendSettings:PublicUrl"] ?? "http://localhost:5236").TrimEnd('/');
            var basePath   = "/api/v1/gestion-administrativa/solicitud-salidas";
            var urlAprobar  = $"{backendUrl}{basePath}/aprobar?token={WebUtility.UrlEncode(tokenAprobar)}";
            var urlRechazar = $"{backendUrl}{basePath}/rechazar?token={WebUtility.UrlEncode(tokenRechazar)}";

            var datos = DatosCorreo(solicitud.Id, solicitud.Codigo, nombreSolicitante,
                                    solicitud.FechaSalida, trayectos, mostrarRecordatorio);
            var body    = SolicitudSalidaEmailTemplates.PorAprobar(
                SalidaEmailLayout.Desde(_configuration), datos,
                urlAprobar, urlRechazar, SalidaEnlaces.Gestion(_configuration, solicitud.Id));
            var subject = $"Solicitud de salida {datos.Codigo} - {nombreSolicitante} - {solicitud.FechaSalida:dd/MM/yyyy}";

            await _emailService.SendAsync(
                to: envio.Para,
                subject: subject,
                body: body,
                isHtml: true,
                cc: envio.Copia.Count > 0 ? envio.Copia : null,
                attachments: adjuntosEmail);

            return envio.Para;
        }

        /// <summary>
        /// Convierte los documentos adjuntos de la solicitud (motivos con requiere_adjunto)
        /// en adjuntos de correo. Null si la solicitud no trae ninguno.
        /// </summary>
        private static async Task<List<EmailAttachment>?> BuildAdjuntosEmailAsync(
            IReadOnlyList<(int TrayectoIndex, IFormFile File)>? adjuntos)
        {
            var files = (adjuntos ?? Array.Empty<(int, IFormFile)>())
                .Where(a => a.File != null && a.File.Length > 0)
                .OrderBy(a => a.TrayectoIndex)
                .ToList();
            if (files.Count == 0) return null;

            var result = new List<EmailAttachment>(files.Count);
            foreach (var (_, f) in files)
            {
                using var ms = new MemoryStream();
                using (var stream = f.OpenReadStream())
                    await stream.CopyToAsync(ms);
                result.Add(new EmailAttachment
                {
                    FileName    = Path.GetFileName(f.FileName),
                    ContentType = string.IsNullOrWhiteSpace(f.ContentType) ? "application/octet-stream" : f.ContentType,
                    Content     = ms.ToArray(),
                });
            }
            return result;
        }

        private async Task SendConfirmacionSolicitanteAsync(
            AppDbContext ctx,
            GaSolicitudSalida solicitud,
            List<(int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino)> trayectos,
            bool mostrarRecordatorio,
            string nombreSolicitante,
            int userId,
            string? aprobadorEmail,
            List<string> enviadoRevisorA)
        {
            var emailSolicitante = await ctx.User
                .Where(u => u.UserId == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(emailSolicitante))
            {
                _logger.LogWarning(
                    "No se envió email de confirmación para solicitud {SolicitudId}: el usuario {UserId} no tiene email registrado.",
                    solicitud.Id, userId);
                return;
            }

            var envio = await _correoResolver.ResolveEnvioAsync(
                CorreoEventoCodigos.Confirmacion,
                new List<string> { emailSolicitante },
                await GetRecepcionRole52Async(ctx));

            if (!envio.Enviar)
            {
                _logger.LogInformation(
                    "Correo de confirmación no enviado para solicitud {SolicitudId}: el correo está apagado o no quedó ningún destinatario configurado.",
                    solicitud.Id);
                return;
            }

            var codigoSolicitud = await GetCodigoSolicitudAsync(ctx, solicitud.WorkerId, solicitud.Id);

            var datos = DatosCorreo(solicitud.Id, codigoSolicitud, nombreSolicitante,
                                    solicitud.FechaSalida, trayectos, mostrarRecordatorio);
            var body    = SolicitudSalidaEmailTemplates.EnRevision(
                SalidaEmailLayout.Desde(_configuration), datos,
                SalidaEnlaces.Autoservicio(_configuration, solicitud.Id),
                enviadoRevisorA, aprobadorEmail);
            var subject = $"Tu solicitud de salida {codigoSolicitud} está en revisión - {solicitud.FechaSalida:dd/MM/yyyy}";

            await _emailService.SendAsync(
                to: envio.Para,
                subject: subject,
                body: body,
                isHtml: true,
                cc: envio.Copia.Count > 0 ? envio.Copia : null);
        }

        // ── CC recepción ────────────────────────────────────────────────────
        private const int RoleIdRecepcion = 52;

        /// <summary>
        /// Base dinámica del CC de los correos al solicitante: los <c>worker.email_corporativo</c> de
        /// todos los users con rol id 52 (USUARIO DE RECEPCIÓN). Los antiguos correos fijos
        /// (<c>recepcionnm@abril.pe</c> y GTH <c>gthnm@abril.pe</c>) ya no se hardcodean aquí: ahora son
        /// reglas editables en ga_correo_regla y los agrega <see cref="ICorreoSalidaRecipientResolver"/>.
        /// </summary>
        private static async Task<List<string>> GetRecepcionRole52Async(AppDbContext ctx)
        {
            return await (
                from ur in ctx.UserRole
                where ur.RoleId == RoleIdRecepcion && ur.State
                join p in ctx.Person  on (int?)ur.UserId  equals p.UserId
                join w in ctx.Worker  on (int?)p.PersonId equals w.PersonId
                where w.EmailCorporativo != null && w.EmailCorporativo != ""
                select w.EmailCorporativo!
            ).Distinct().ToListAsync();
        }

        /// <summary>
        /// Devuelve el número de orden de esta solicitud dentro del historial del propio
        /// solicitante (1 = primera solicitud del worker, 2 = segunda, ...). Se usa para
        /// mostrar un identificador estable y por-usuario en los correos del solicitante,
        /// en lugar del id global de la tabla.
        /// </summary>
        /// <summary>
        /// Identificador de la solicitud tal como lo ve el trabajador: su código SOL-AAAA-NNNN.
        /// Las solicitudes anteriores al código conservan el correlativo por trabajador con el que
        /// salieron sus correos ("#12"), para no cambiarle el número a un hilo ya enviado.
        /// </summary>
        private static async Task<string> GetCodigoSolicitudAsync(AppDbContext ctx, int workerId, int solicitudId)
        {
            var codigo = await ctx.GaSolicitudSalida
                .Where(s => s.Id == solicitudId)
                .Select(s => s.Codigo)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(codigo)) return codigo;

            var numero = await ctx.GaSolicitudSalida
                .Where(s => s.WorkerId == workerId && s.Id <= solicitudId)
                .CountAsync();
            return $"#{numero}";
        }

        private static async Task<string> ResolveLugarDisplay(AppDbContext ctx, int? lugarId, string? lugarLibre)
        {
            if (!string.IsNullOrWhiteSpace(lugarLibre)) return lugarLibre.Trim();
            if (!lugarId.HasValue) return "—";

            var lugar = await ctx.GaLugar
                .Where(l => l.Id == lugarId.Value)
                .Select(l => new { l.Tipo, l.Nombre, l.ProjectId })
                .FirstOrDefaultAsync();
            if (lugar == null) return "—";

            if (lugar.Tipo == "proyecto" && lugar.ProjectId.HasValue)
            {
                var proj = await ctx.Project
                    .Where(p => p.ProjectId == lugar.ProjectId.Value)
                    .Select(p => p.ProjectDescription)
                    .FirstOrDefaultAsync();
                return proj ?? "[Sin proyecto]";
            }
            return lugar.Nombre ?? "—";
        }

        /// <summary>
        /// Empaqueta lo que ya tiene el servicio en memoria para las plantillas de correo. Está
        /// acá y no en cada punto de envío porque los cuatro correos arman exactamente los mismos
        /// datos y una diferencia entre ellos se leería como dos solicitudes distintas.
        /// </summary>
        private static SalidaCorreoDatos DatosCorreo(
            int solicitudId, string? codigo, string solicitante, DateOnly fechaSalida,
            List<(int Orden, string HoraSalida, string HoraRetorno, string Motivo, string Origen, string Destino)> trayectos,
            bool mostrarRecordatorio) => new()
        {
            SolicitudId         = solicitudId,
            // El correo al revisor sale al crear la solicitud, así que el código siempre existe;
            // el fallback es por si un flujo futuro arma el correo sobre una fila anterior a la
            // columna, donde el id es el único identificador a mano.
            Codigo              = string.IsNullOrWhiteSpace(codigo) ? $"#{solicitudId}" : codigo,
            Solicitante         = solicitante,
            FechaSalida         = fechaSalida,
            Trayectos           = trayectos
                .Select(t => new SalidaCorreoTrayecto(t.Orden, t.HoraSalida, t.HoraRetorno, t.Motivo, t.Origen, t.Destino))
                .ToList(),
            MostrarRecordatorio = mostrarRecordatorio,
        };

        /// <summary>
        /// Página que ve el revisor después de aprobar o rechazar desde el correo. Es la única
        /// pantalla del flujo que no está en la intranet (el revisor llega sin sesión, con el token
        /// del correo), así que lleva el mismo azul y la misma barra lima del correo para que no
        /// parezca de otra aplicación.
        /// </summary>
        private string RenderResultPage(string title, string message, bool isSuccess) =>
            RenderPaginaRevisor(
                title,
                $@"<p style=""color:#3F4A44;line-height:1.6;margin:0;font-size:15px"">{WebUtility.HtmlEncode(message)}</p>",
                isSuccess ? "#166534" : "#991B1B");

        /// <summary>
        /// Chrome de las dos páginas del revisor: tarjeta blanca sobre el lienzo verdoso, título en
        /// el color del desenlace, barra lima y el logo al pie. Mismos colores que
        /// <see cref="AbrilEmailLayout"/> — están escritos acá y no tomados de esa clase porque
        /// esto es una página web normal (con &lt;style&gt;), no el HTML de tablas del correo.
        /// </summary>
        private string RenderPaginaRevisor(string titulo, string cuerpoHtml, string colorTitulo)
        {
            var assets = (_configuration["App:EmailAssetsUrl"]
                          ?? _configuration["App:FrontendUrl"]
                          ?? "https://intranet.abril.pe").TrimEnd('/');

            return $@"<!DOCTYPE html>
<html lang=""es""><head><meta charset=""utf-8"" /><meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
<title>{WebUtility.HtmlEncode(titulo)}</title>
<style>
  body {{ font-family:-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;background:#F4F8F4;margin:0;padding:40px 16px;color:#3F4A44; }}
  .card {{ max-width:560px;margin:0 auto;background:#fff;border:1px solid #E6EDE7;border-radius:16px;padding:36px 34px;text-align:center; }}
  h1 {{ color:{colorTitulo};margin:0 0 10px;font-size:24px;letter-spacing:-0.4px; }}
  .bar {{ width:84px;height:4px;background:#64BC04;border-radius:2px;margin:0 auto 22px; }}
  .body {{ text-align:left; }}
  .logo {{ display:block;width:150px;margin:28px auto 0; }}
  .pie {{ margin:12px 0 0;font-size:11px;color:#9AA8A0; }}
  textarea {{ width:100%;min-height:120px;padding:12px;border:1px solid #E6EDE7;border-radius:10px;font:inherit;box-sizing:border-box;color:#3F4A44; }}
  textarea:focus {{ outline:none;border-color:#005D9D; }}
  button {{ margin-top:16px;background:#991B1B;color:#fff;border:0;padding:13px 28px;border-radius:10px;cursor:pointer;font-size:15px;font-weight:700;font-family:inherit; }}
  button:hover {{ background:#7F1D1D; }}
</style></head><body>
<div class=""card"">
  <h1>{WebUtility.HtmlEncode(titulo)}</h1>
  <div class=""bar""></div>
  <div class=""body"">{cuerpoHtml}</div>
  <img class=""logo"" src=""{assets}/images/emails/abril-logo.png"" alt=""ABRIL Grupo Inmobiliario"" />
  <p class=""pie"">Correo automático de Abril One · Gestión Administrativa · Salidas.</p>
</div>
</body></html>";
        }

        // ── Detalle + capturas ──────────────────────────────────────────────

        public async Task<SolicitudSalidaDetalleDto> GetDetalle(int solicitudId, int userId)
        {
            var detalle = await _repo.GetDetalleForUser(solicitudId, userId);
            return detalle ?? throw new AbrilException("Solicitud no encontrada o no te pertenece.", 404);
        }

        public Task Cancelar(int solicitudId, int userId) => _repo.Cancelar(solicitudId, userId);

        public async Task<List<SolicitudSalidaCapturaDto>> UploadCapturasToTrayecto(int trayectoId, IEnumerable<(IFormFile File, decimal Monto)> items, int userId)
        {
            var trayecto = await _repo.GetTrayectoForUploadingCapturas(trayectoId, userId)
                ?? throw new AbrilException("No se pueden subir capturas: el trayecto no existe, no te pertenece, no está aprobado, o ya fue rendido.", 404);

            var lista = items?
                .Where(it => it.File != null && it.File.Length > 0)
                .ToList()
                ?? new();
            if (lista.Count == 0)
                throw new AbrilException("No se recibieron capturas.", 400);

            if (lista.Any(it => it.Monto < 0))
                throw new AbrilException("El monto no puede ser negativo.", 400);

            // Destino configurable desde BD (ga_captura_folder): se guarda el link de SharePoint tal
            // cual y se resuelve a driveId/folderId vía Graph. Editable sin redeploy y cada entorno
            // apunta a su propia biblioteca porque la config vive en su propia base de datos. Se
            // resuelve una sola vez para todo el lote. Mismo patrón que gth_sustento_folder.
            string? folderUrl;
            using (var ctx = _factory.CreateDbContext())
            {
                folderUrl = await ctx.GaCapturaFolder
                    .Where(c => c.State && c.Active)
                    .OrderBy(c => c.GaCapturaFolderId)
                    .Select(c => c.LinkUrl)
                    .FirstOrDefaultAsync();
            }
            if (string.IsNullOrWhiteSpace(folderUrl))
                throw new AbrilException(
                    "No se ha configurado la carpeta de SharePoint donde guardar las capturas de movilidad. " +
                    "Pide al administrador registrarla en la tabla ga_captura_folder.", 409);

            var carpeta = await _sharePointService.ResolveSharePointFolderUrlAsync(folderUrl);
            if (carpeta == null || !carpeta.IsFolder)
                throw new AbrilException("No se pudo resolver la carpeta de capturas en SharePoint.", 502);

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var subidos = new List<(string Url, string? ItemId, string Filename, decimal Monto)>();
            try
            {
                foreach (var it in lista)
                {
                    var f = it.File;
                    var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
                    if (!allowed.Contains(ext))
                        throw new AbrilException($"Tipo de archivo no permitido: {f.FileName}. Solo JPG/PNG/WEBP/GIF.", 400);

                    var safeName = SanitizeFilename(Path.GetFileNameWithoutExtension(f.FileName));
                    var stamp    = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                    var filename = $"s{trayecto.SolicitudId}_t{trayecto.Id}_{stamp}_{safeName}{ext}";

                    using var stream = f.OpenReadStream();
                    var result = await _sharePointService.UploadToOneDriveFolderAsync(
                        carpeta.DriveId, carpeta.ItemId, filename, stream,
                        f.ContentType ?? "application/octet-stream",
                        autoRenameOnLock: true);

                    if (result?.WebUrl is null)
                        throw new AbrilException($"No se pudo subir el archivo {f.FileName}.", 502);

                    subidos.Add((result.WebUrl, result.ItemId, filename, it.Monto));
                }
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló subida de capturas para trayecto {TrayectoId}", trayectoId);
                throw new AbrilException("Error al subir las capturas a SharePoint.", 502);
            }

            return await _repo.InsertCapturas(trayectoId, subidos, userId);
        }

        public async Task<List<int>> GetIdsRendiblesMes(int userId, int? anio, int? mes)
        {
            var (desde, hasta) = anio.HasValue && mes.HasValue
                ? MesAnteriorPeru.RangoDe(anio.Value, mes.Value)
                : MesAnteriorPeru.Rango();

            // El plazo se revisa ANTES de buscar: si el periodo cerró, `SoloAptas` devolvería cero
            // filas y el error diría "no tienes nada listo", que es cierto pero esconde el motivo real.
            var calendario = await _repo.GetCalendarioNoLaborable();
            var limite     = calendario.LimiteDeRendicion(desde.Year, desde.Month);
            if (MesAnteriorPeru.HoyPeru() > limite)
                throw new AbrilException(
                    $"El plazo para rendir las salidas de {desde:MM/yyyy} venció el {limite:dd/MM/yyyy} " +
                    "(7.º día hábil del mes siguiente). Ya no se pueden rendir.", 400);

            // GetByUserId ya acota al worker del usuario y calcula AptaParaRendir (captura por
            // trayecto, catálogo para TI, área con capturas opcionales y motivo reembolsable), así
            // que la elegibilidad sale de ahí sin duplicar reglas.
            var ids = (await _repo.GetByUserId(userId, new SolicitudSalidaFiltersDto
            {
                EstadoAprobacion = EstadosSalida.Aprobacion.NombreAprobado,
                EstadoRendicion  = EstadosSalida.Rendicion.NombreNoRendido,
                FechaSalidaDesde = desde,
                FechaSalidaHasta = hasta,
                SoloAptas        = true,
            })).Select(x => x.Id).ToList();

            if (ids.Count == 0)
                throw new AbrilException(
                    $"No tienes salidas listas para rendir entre el {desde:dd/MM/yyyy} y el {hasta:dd/MM/yyyy}. " +
                    "Deben estar aprobadas, sin rendir, con las capturas de todos sus trayectos y con un motivo reembolsable.", 400);

            return ids;
        }

        public Task<ConsolidadoS10Dto> UploadConsolidadoS10(int solicitudId, ConsolidadoS10Ambito ambito, IFormFile file, int userId)
            // ownerUserId = userId: en el autoservicio solo se adjunta a salidas propias.
            => _consolidadoService.Upload(solicitudId, ambito, file, userId, ownerUserId: userId);

        private static string SanitizeFilename(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "captura";
            var invalid = Path.GetInvalidFileNameChars().Concat(new[] { ' ', '#', '%', '&', '+' }).ToHashSet();
            var clean = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return clean.Length > 60 ? clean.Substring(0, 60) : clean;
        }

        // ── Aviso al revisor de que el S10 ya está adjunto ───────────────────

        public async Task<string> NotificarRevisorS10(int solicitudId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var info = await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                join r in ctx.GaRendicion on s.RendicionId equals (int?)r.Id into rGroup
                from r in rGroup.DefaultIfEmpty()
                where s.Id == solicitudId
                select new
                {
                    s.Id, s.WorkerId, WorkerInternalId = w.Id,
                    AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                    s.FechaSalida, s.EstadoRendicionId, s.EstadoReembolsoId, s.RendicionId,
                    Trabajador = per != null ? (per.FullName ?? "Trabajador") : "Trabajador",
                    EsPropia   = per != null && per.UserId == userId,
                    NumeroPlanilla = r != null ? r.NumeroPlanilla : null,
                }
            ).FirstOrDefaultAsync()
              ?? throw new AbrilException("La solicitud de salida no existe.", 404);

            if (!info.EsPropia)
                throw new AbrilException("Solo puedes avisar al revisor de tus propias salidas.", 403);

            if (info.EstadoRendicionId != EstadosSalida.Rendicion.Rendido)
                throw new AbrilException("La salida todavía no está rendida.", 400);

            if (info.EstadoReembolsoId != EstadosSalida.Reembolso.Pendiente
                && info.EstadoReembolsoId != EstadosSalida.Reembolso.Rechazado)
                throw new AbrilException(
                    "El reembolso de esta salida ya fue revisado: no hace falta volver a avisar.", 400);

            var consolidado = await _consolidadoService.GetForSolicitud(solicitudId);
            if (consolidado == null)
                throw new AbrilException(
                    "Primero adjunta el Consolidado del S10: es lo que el revisor tiene que mirar.", 400);

            var revisor = await _revisorResolver.ResolveAsync(info.WorkerInternalId);
            if (string.IsNullOrWhiteSpace(revisor?.Email))
                throw new AbrilException(
                    "No se pudo determinar el correo de tu jefe/revisor. Avisa a Gestión del Talento Humano.", 409);

            var envio = await _correoResolver.ResolveEnvioAsync(
                CorreoEventoCodigos.S10Revisor,
                new List<string> { revisor!.Email! });

            if (!envio.Enviar)
                throw new AbrilException(
                    "El aviso al revisor está desactivado en la configuración de correos de Gestión Administrativa.",
                    409);

            // Monto rendido y cantidad de trayectos, para que el correo diga de cuánto se trata.
            var trayectoIds = await ctx.GaSolicitudTrayecto
                .Where(t => t.SolicitudId == solicitudId)
                .Select(t => t.Id)
                .ToListAsync();
            var monto = trayectoIds.Count == 0
                ? 0m
                : await ctx.GaSolicitudCaptura
                    .Where(c => trayectoIds.Contains(c.TrayectoId))
                    .SumAsync(c => (decimal?)c.Monto) ?? 0m;

            var datos = new ReembolsoCorreoDatos
            {
                SolicitudId    = info.Id,
                Codigo         = await GetCodigoSolicitudAsync(ctx, info.WorkerId, info.Id),
                Trabajador     = info.Trabajador,
                Area           = await ResolveAreaNombreAsync(ctx, info.AreaScopeId),
                FechaSalida    = info.FechaSalida,
                NumeroPlanilla = info.NumeroPlanilla.HasValue ? $"TI: {info.NumeroPlanilla.Value:D6}" : null,
                TrayectosCount = trayectoIds.Count,
                MontoTotal     = monto,
            };

            var url  = SalidaEnlaces.Gestion(_configuration, solicitudId);
            var body = ReembolsoEmailTemplates.RevisionPendiente(SalidaEmailLayout.Desde(_configuration), datos, url);

            await _emailService.SendAsync(
                to: envio.Para,
                subject: $"Reembolso por revisar - {info.Trabajador} - {info.FechaSalida:dd/MM/yyyy}",
                body: body,
                isHtml: true,
                cc: envio.Copia.Count > 0 ? envio.Copia : null);

            var solicitud = await ctx.GaSolicitudSalida.FirstAsync(x => x.Id == solicitudId);
            solicitud.RevisorNotificadoAt    = DateTimeOffset.UtcNow;
            solicitud.RevisorNotificadoPorId = userId;
            solicitud.UpdatedAt              = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();

            var nombre = string.IsNullOrWhiteSpace(revisor.Nombre) ? "tu revisor" : revisor.Nombre;
            return $"Se le avisó a {nombre}.";
        }

        /// <summary>Nombre del área a la que apunta el nodo (el más bajo del árbol). Null si no tiene.</summary>
        private static async Task<string?> ResolveAreaNombreAsync(AppDbContext ctx, int? areaScopeId)
        {
            if (!areaScopeId.HasValue) return null;
            return await (
                from sc in ctx.AreaScope
                join it in ctx.AreaItem on sc.AreaItemId equals it.AreaItemId
                where sc.AreaScopeId == areaScopeId.Value
                select it.AreaItemName
            ).FirstOrDefaultAsync();
        }

        // ── Notificación de aprobación al solicitante ─────────────────────────

        public async Task NotifySolicitanteAprobada(int solicitudId)
        {
            try
            {
                using var ctx = _factory.CreateDbContext();

                // 1. Datos de cabecera + worker + email del solicitante en una query
                var info = await (
                    from s in ctx.GaSolicitudSalida
                    join w in ctx.Worker on s.WorkerId equals w.Id
                    join p in ctx.Person on w.PersonId equals (int?)p.PersonId into pg
                    from p in pg.DefaultIfEmpty()
                    join u in ctx.User on (p != null ? p.UserId : null) equals (int?)u.UserId into ug
                    from u in ug.DefaultIfEmpty()
                    where s.Id == solicitudId
                    select new
                    {
                        s.Id, s.FechaSalida, s.EstadoAprobacionId, s.WorkerId,
                        Nombre = p != null ? (p.FullName ?? "Trabajador") : "Trabajador",
                        Email  = u != null ? u.Email : null,
                    }
                ).FirstOrDefaultAsync();

                if (info == null)
                {
                    _logger.LogWarning("NotifySolicitanteAprobada: solicitud {SolicitudId} no encontrada.", solicitudId);
                    return;
                }
                if (info.EstadoAprobacionId != EstadosSalida.Aprobacion.Aprobado)
                {
                    _logger.LogWarning("NotifySolicitanteAprobada: solicitud {SolicitudId} no está en estado Aprobado (estado actual: {Estado}). Email no enviado.", solicitudId, EstadosSalida.Aprobacion.Nombre(info.EstadoAprobacionId));
                    return;
                }
                if (string.IsNullOrWhiteSpace(info.Email))
                {
                    _logger.LogWarning("NotifySolicitanteAprobada: solicitud {SolicitudId} — el solicitante no tiene email registrado.", solicitudId);
                    return;
                }

                // 2. Trayectos para mostrar en el email
                var trayectos = await ctx.GaSolicitudTrayecto
                    .Where(t => t.SolicitudId == solicitudId)
                    .OrderBy(t => t.Orden)
                    .ToListAsync();

                var (resueltos, mostrarRecordatorio) = await ResolveTrayectosForEmailAsync(ctx, trayectos);

                var codigoSolicitud = await GetCodigoSolicitudAsync(ctx, info.WorkerId, info.Id);

                var envio = await _correoResolver.ResolveEnvioAsync(
                    CorreoEventoCodigos.Aprobada,
                    new List<string> { info.Email },
                    await GetRecepcionRole52Async(ctx));

                if (!envio.Enviar)
                {
                    _logger.LogInformation(
                        "Correo de aprobación no enviado para solicitud {SolicitudId}: el correo está apagado o no quedó ningún destinatario configurado.",
                        solicitudId);
                    return;
                }

                var datos   = DatosCorreo(info.Id, codigoSolicitud, info.Nombre,
                                          info.FechaSalida, resueltos, mostrarRecordatorio);
                var body    = SolicitudSalidaEmailTemplates.Aprobada(
                    SalidaEmailLayout.Desde(_configuration), datos,
                    SalidaEnlaces.Autoservicio(_configuration, info.Id));
                var subject = $"Solicitud de salida {codigoSolicitud} APROBADA - {info.FechaSalida:dd/MM/yyyy}";

                await _emailService.SendAsync(
                    to: envio.Para,
                    subject: subject,
                    body: body,
                    isHtml: true,
                    cc: envio.Copia.Count > 0 ? envio.Copia : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email de aprobación al solicitante para solicitud {SolicitudId}", solicitudId);
            }
        }

        public async Task NotifySolicitanteRechazada(int solicitudId)
        {
            try
            {
                using var ctx = _factory.CreateDbContext();

                // 1. Datos de cabecera + worker + email del solicitante en una query
                var info = await (
                    from s in ctx.GaSolicitudSalida
                    join w in ctx.Worker on s.WorkerId equals w.Id
                    join p in ctx.Person on w.PersonId equals (int?)p.PersonId into pg
                    from p in pg.DefaultIfEmpty()
                    join u in ctx.User on (p != null ? p.UserId : null) equals (int?)u.UserId into ug
                    from u in ug.DefaultIfEmpty()
                    where s.Id == solicitudId
                    select new
                    {
                        s.Id, s.FechaSalida, s.EstadoAprobacionId, s.WorkerId, s.MotivoRechazo,
                        Nombre = p != null ? (p.FullName ?? "Trabajador") : "Trabajador",
                        Email  = u != null ? u.Email : null,
                    }
                ).FirstOrDefaultAsync();

                if (info == null)
                {
                    _logger.LogWarning("NotifySolicitanteRechazada: solicitud {SolicitudId} no encontrada.", solicitudId);
                    return;
                }
                if (info.EstadoAprobacionId != EstadosSalida.Aprobacion.Rechazado)
                {
                    _logger.LogWarning("NotifySolicitanteRechazada: solicitud {SolicitudId} no está en estado Rechazado (estado actual: {Estado}). Email no enviado.", solicitudId, EstadosSalida.Aprobacion.Nombre(info.EstadoAprobacionId));
                    return;
                }
                if (string.IsNullOrWhiteSpace(info.Email))
                {
                    _logger.LogWarning("NotifySolicitanteRechazada: solicitud {SolicitudId} — el solicitante no tiene email registrado.", solicitudId);
                    return;
                }

                // 2. Trayectos para mostrar en el email
                var trayectos = await ctx.GaSolicitudTrayecto
                    .Where(t => t.SolicitudId == solicitudId)
                    .OrderBy(t => t.Orden)
                    .ToListAsync();

                var (resueltos, _) = await ResolveTrayectosForEmailAsync(ctx, trayectos);

                var codigoSolicitud = await GetCodigoSolicitudAsync(ctx, info.WorkerId, info.Id);

                var envio = await _correoResolver.ResolveEnvioAsync(
                    CorreoEventoCodigos.Rechazada,
                    new List<string> { info.Email },
                    await GetRecepcionRole52Async(ctx));

                if (!envio.Enviar)
                {
                    _logger.LogInformation(
                        "Correo de rechazo no enviado para solicitud {SolicitudId}: el correo está apagado o no quedó ningún destinatario configurado.",
                        solicitudId);
                    return;
                }

                var datos   = DatosCorreo(info.Id, codigoSolicitud, info.Nombre,
                                          info.FechaSalida, resueltos, mostrarRecordatorio: false);
                var body    = SolicitudSalidaEmailTemplates.Rechazada(
                    SalidaEmailLayout.Desde(_configuration), datos, info.MotivoRechazo,
                    SalidaEnlaces.Autoservicio(_configuration, info.Id));
                var subject = $"Solicitud de salida {codigoSolicitud} RECHAZADA - {info.FechaSalida:dd/MM/yyyy}";

                await _emailService.SendAsync(
                    to: envio.Para,
                    subject: subject,
                    body: body,
                    isHtml: true,
                    cc: envio.Copia.Count > 0 ? envio.Copia : null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email de rechazo al solicitante para solicitud {SolicitudId}", solicitudId);
            }
        }

    }
}
