using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Pdf;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using ClosedXML.Excel;
using Humanizer;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Services
{
    public class GestionSalidaService : IGestionSalidaService
    {
        private readonly IGestionSalidaRepository _repo;
        private readonly IGraphSharePointService _sharePointService;
        private readonly ISolicitudSalidaService _solicitudSalidaService;
        private readonly ISalidaVisibilityResolver _visibilityResolver;
        private readonly ICorreoSalidaRecipientResolver _correoResolver;
        private readonly ILogger<GestionSalidaService> _logger;

        public GestionSalidaService(
            IGestionSalidaRepository repo,
            IGraphSharePointService sharePointService,
            ISolicitudSalidaService solicitudSalidaService,
            ISalidaVisibilityResolver visibilityResolver,
            ICorreoSalidaRecipientResolver correoResolver,
            ILogger<GestionSalidaService> logger)
        {
            _repo = repo;
            _sharePointService = sharePointService;
            _solicitudSalidaService = solicitudSalidaService;
            _visibilityResolver = visibilityResolver;
            _correoResolver = correoResolver;
            _logger = logger;
        }

        public async Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters)
        {
            await ApplyVisibilityAsync(filters);
            return await _repo.GetAll(filters);
        }

        public async Task<GestionSalidaPagedDto> GetPaged(GestionSalidaFiltersDto filters)
        {
            await ApplyVisibilityAsync(filters);
            return await _repo.GetPaged(filters);
        }

        public async Task<GestionSalidaFilterDataDto> GetFilterData(int? currentUserId, bool seesAllOverride)
        {
            // Resuelve el alcance del usuario y recorta trabajadores + árbol de áreas a ese alcance
            // (área del usuario hacia abajo). Recepción / GTH / sin usuario → ve todo, sin recorte.
            //   • El árbol: los nodos tope del conjunto visible (cuyo padre queda fuera) los toma el
            //     frontend como raíces del cascada, así un jefe arranca en su área y un gerente en su
            //     gerencia sin lógica adicional en el cliente.
            //   • Los trabajadores: solo los de las áreas visibles (más el propio usuario).
            bool seesAll = seesAllOverride || !currentUserId.HasValue;
            var visibleIds = new List<int>();

            if (!seesAll)
            {
                var vis = await _visibilityResolver.ResolveAsync(currentUserId!.Value, VisibilidadAmbitoIds.Salidas);
                seesAll = vis.SeesAll;
                visibleIds = vis.AreaScopeIds.ToList();
            }

            var data = await _repo.GetFilterData(seesAll, visibleIds, currentUserId);

            // Meses del desplegable "Mes a rendir". Van acá —y no en el listado, como las
            // tarjetas— porque son las opciones de un control: se arman con el alcance completo del
            // usuario, si no el propio filtro de mes iría borrando los meses que ofrece. La
            // pantalla vuelve a pedir filter-data después de cada acción que los mueve.
            data.MesesRendicion = await MesesRendicionAsync(new GestionSalidaFiltersDto
            {
                CurrentUserId       = currentUserId,
                SeesAll             = seesAll,
                VisibleAreaScopeIds = visibleIds,
            });

            return data;
        }

        /// <summary>
        /// Arma los meses del desplegable a partir de la única bandeja que importa: lo aprobado sin
        /// rendir, de donde salen las salidas aptas.
        ///
        /// Se apoya en <c>_repo.GetAll</c> en vez de armar SQL propio para que la definición de
        /// "apta para rendir" viva en un solo lugar. Es una consulta acotada a trabajo pendiente,
        /// no a la tabla entera.
        /// </summary>
        private async Task<List<MesRendicionDto>> MesesRendicionAsync(GestionSalidaFiltersDto scope)
        {
            var pendientes = await _repo.GetAll(new GestionSalidaFiltersDto
            {
                CurrentUserId       = scope.CurrentUserId,
                SeesAll             = scope.SeesAll,
                VisibleAreaScopeIds = scope.VisibleAreaScopeIds,
                EstadoAprobacion    = EstadosSalida.Aprobacion.NombreAprobado,
                EstadoRendicion     = EstadosSalida.Rendicion.NombreNoRendido,
            });

            var aptas = pendientes.Where(x => x.AptaParaRendir).ToList();

            // Los meses vencidos no aparecen: `AptaParaRendir` ya los descarta fila por fila, así
            // que un periodo cerrado se queda sin aptas y cae solo del desplegable.
            var calendario = await _repo.GetCalendarioNoLaborable();
            var meses = aptas
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
                && !meses.Any(m => m.Anio == desdeMesAnterior.Year && m.Mes == desdeMesAnterior.Month))
            {
                meses.Add(new MesRendicionDto
                {
                    Anio        = desdeMesAnterior.Year,
                    Mes         = desdeMesAnterior.Month,
                    Label       = EtiquetaMes(desdeMesAnterior.Year, desdeMesAnterior.Month),
                    Cantidad    = 0,
                    FechaLimite = calendario.LimiteDeRendicion(desdeMesAnterior.Year, desdeMesAnterior.Month),
                });
                meses = meses.OrderByDescending(m => m.Anio).ThenByDescending(m => m.Mes).ToList();
            }

            return meses;
        }

        /// <summary>"Agosto 2026" — el nombre del mes en español, con la primera letra en mayúscula.</summary>
        private static string EtiquetaMes(int anio, int mes)
        {
            var cultura = CultureInfo.GetCultureInfo("es-PE");
            var nombre  = cultura.DateTimeFormat.GetMonthName(mes);
            return $"{char.ToUpper(nombre[0], cultura)}{nombre[1..]} {anio}";
        }

        /// <summary>
        /// Resuelve el alcance de visibilidad del usuario actual y lo escribe en el filtro
        /// (SeesAll / VisibleAreaScopeIds). Sin CurrentUserId no se aplica restricción.
        /// </summary>
        private async Task ApplyVisibilityAsync(GestionSalidaFiltersDto filters)
        {
            if (!filters.CurrentUserId.HasValue) return;

            // USUARIO DE RECEPCIÓN se sobrepone al alcance por área: ve todo, sin resolver.
            if (filters.SeesAllOverride)
            {
                filters.SeesAll = true;
                return;
            }

            var vis = await _visibilityResolver.ResolveAsync(
                filters.CurrentUserId.Value, VisibilidadAmbitoIds.Salidas);
            filters.SeesAll = vis.SeesAll;
            filters.VisibleAreaScopeIds = vis.AreaScopeIds.ToList();
        }

        public async Task<byte[]> GetExcel(GestionSalidaFiltersDto filters)
        {
            await ApplyVisibilityAsync(filters);
            var salidas = await _repo.GetAll(filters);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Gestión de Salidas");

            string[] headers =
            [
                "#", "Trabajador", "Área", "Revisor", "Fecha salida", "Hora salida", "Hora retorno",
                "Motivo", "Origen", "Destino", "Aprobación", "Rendición", "Reembolso", "Registrada",
            ];

            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.FromHtml("#64BC04");
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E5F7D1");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            for (int r = 0; r < salidas.Count; r++)
            {
                var s   = salidas[r];
                int row = r + 2;

                ws.Cell(row, 1).Value  = r + 1;
                ws.Cell(row, 2).Value  = s.Trabajador;
                ws.Cell(row, 3).Value  = s.Area          ?? "—";
                ws.Cell(row, 4).Value  = s.RevisorNombre ?? "—";
                ws.Cell(row, 5).Value  = s.FechaSalida.ToString("dd/MM/yyyy");
                ws.Cell(row, 6).Value  = s.HoraSalida.HasValue ? s.HoraSalida.Value.ToString("HH:mm") : "—";
                ws.Cell(row, 7).Value  = s.HoraRetorno.HasValue ? s.HoraRetorno.Value.ToString("HH:mm") : "—";
                ws.Cell(row, 8).Value  = s.Motivo;
                ws.Cell(row, 9).Value  = s.LugarOrigen  ?? "—";
                ws.Cell(row, 10).Value = s.LugarDestino ?? "—";
                ws.Cell(row, 11).Value = s.EstadoAprobacion;
                ws.Cell(row, 12).Value = s.EstadoRendicion;
                // El reembolso solo tiene sentido cuando ya hay algo que revisar (salida rendida y
                // Consolidado del S10 adjunto): antes de eso la celda va vacía en vez de decir
                // "Pendiente", que se leería como si estuviera esperando a alguien.
                ws.Cell(row, 13).Value = s.ReembolsoRevisable || s.EstadoReembolso != "Pendiente"
                    ? s.EstadoReembolso
                    : "";
                ws.Cell(row, 14).Value = s.CreatedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm");

                var rowRange = ws.Range(row, 1, row, headers.Length);
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
                rowRange.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;

                if (r % 2 == 0)
                    rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFAFA");

                var aprobacionCell = ws.Cell(row, 11);
                aprobacionCell.Style.Font.Bold = true;
                aprobacionCell.Style.Font.FontColor = s.EstadoAprobacion switch
                {
                    "Aprobado"  => XLColor.FromHtml("#009C87"),
                    "Rechazado" => XLColor.FromHtml("#D30000"),
                    _           => XLColor.FromHtml("#92400E"),
                };

                var rendicionCell = ws.Cell(row, 12);
                rendicionCell.Style.Font.Bold = true;
                rendicionCell.Style.Font.FontColor = s.EstadoRendicion == "Rendido"
                    ? XLColor.FromHtml("#0086A5")
                    : XLColor.FromHtml("#9CA3AF");

                var reembolsoCell = ws.Cell(row, 13);
                reembolsoCell.Style.Font.Bold = true;
                reembolsoCell.Style.Font.FontColor = s.EstadoReembolso switch
                {
                    "Aprobado"  => XLColor.FromHtml("#009C87"),
                    "Rechazado" => XLColor.FromHtml("#D30000"),
                    "Firmado"   => XLColor.FromHtml("#4338CA"),
                    "Pagado"    => XLColor.FromHtml("#15803D"),
                    _           => XLColor.FromHtml("#92400E"),
                };
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        /// <summary>
        /// Qué correo saldría al aprobar o rechazar las salidas seleccionadas, y a quién. Se
        /// resuelve con la MISMA llamada que hace el envío (NotifySolicitanteAprobada /
        /// NotifySolicitanteRechazada), así que la confirmación no promete un aviso que la
        /// configuración dejó fuera. Lo usan el botón masivo y el del modal de detalle.
        ///
        /// Best-effort: ante un error devuelve una lista vacía y la confirmación sale sin correos.
        /// Que el preview falle no puede impedir decidir.
        /// </summary>
        public async Task<List<CorreoAvisoPreviewDto>> GetCorreoPreview(
            CorreoPreviewRequestDto request, GestionSalidaFiltersDto scope)
        {
            try
            {
                await ApplyVisibilityAsync(scope);

                var solicitantes = await _repo.GetCorreosSolicitantes(request.SolicitudIds, scope);
                var envio = await _correoResolver.ResolveEnvioAsync(
                    request.Aprobar ? CorreoEventoCodigos.Aprobada : CorreoEventoCodigos.Rechazada,
                    solicitantes);

                if (!envio.Enviar || envio.Para.Count == 0) return new();

                return new List<CorreoAvisoPreviewDto>
                {
                    new()
                    {
                        Etiqueta = "Al solicitante",
                        Para     = envio.Para,
                        Copia    = envio.Copia,
                    },
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolviendo el preview de correos de la decisión de la salida");
                return new();
            }
        }

        public async Task Aprobar(int id, int reviewerUserId)
        {
            await _repo.Aprobar(id, reviewerUserId);
            // Email de confirmación al solicitante (best-effort, no rompe el flujo si falla)
            await _solicitudSalidaService.NotifySolicitanteAprobada(id);
        }

        public async Task Rechazar(int id, int reviewerUserId, string? motivoRechazo)
        {
            await _repo.Rechazar(id, reviewerUserId, motivoRechazo);
            // Email de rechazo al solicitante (best-effort, no rompe el flujo si falla)
            await _solicitudSalidaService.NotifySolicitanteRechazada(id);
        }

        // El solicitante cancela su propia salida. Misma lógica que el autoservicio de Solicitud
        // de Salidas: no duplicamos el guard de propiedad/estado, lo delegamos.
        public Task Cancelar(int id, int userId) => _solicitudSalidaService.Cancelar(id, userId);

        public Task SetHoraSalidaReal(int id, TimeOnly? hora, int registradaPorUserId)
            => _repo.SetHoraSalidaReal(id, hora, registradaPorUserId);

        public Task SetHoraRetornoReal(int id, TimeOnly? hora, int registradaPorUserId)
            => _repo.SetHoraRetornoReal(id, hora, registradaPorUserId);

        public async Task<(byte[] Pdf, int Count, int RendicionId, string Codigo)> RendirYGenerarPlanilla(
            IEnumerable<int> ids, int userId, int? ownerUserId = null)
        {
            var idsList = ids?.Distinct().ToList() ?? new List<int>();

            // 0. Guard de propiedad: cuando la rendición la dispara el propio trabajador desde su
            //    autoservicio, solo puede rendir solicitudes suyas.
            if (ownerUserId.HasValue)
            {
                var ajenas = await _repo.GetIdsNotOwnedByUser(idsList, ownerUserId.Value);
                if (ajenas.Count > 0)
                    throw new AbrilException("Solo puedes rendir tus propias solicitudes de salida.", 403);
            }

            // 1. Pre-flight: ¿cuáles serían marcables? — sin tocar BD.
            var elegiblesIds = await _repo.GetEligibleIdsForRendicion(idsList);
            if (elegiblesIds.Count == 0)
                throw new AbrilException("No hay solicitudes elegibles para rendir (deben estar aprobadas y no rendidas).", 400);

            // 1.b. Bloqueo: cada trayecto REEMBOLSABLE de cada solicitud debe estar cubierto (los
            //       que no generan reembolso no entran en la planilla, así que no se les pide nada).
            //       Regla normal: trayecto con al menos 1 captura.
            //       Área con capturas opcionales (Configuración → Capturas): no se exige ninguna.
            //       Regla TI (Tecnología de la Información): captura O match contra ga_trayecto.
            var sinCapturas = await _repo.GetIdsConTrayectosSinCapturas(elegiblesIds);
            if (sinCapturas.Count > 0)
                throw new AbrilException(
                    $"No se puede rendir: {sinCapturas.Count} solicitud(es) tienen trayectos sin cubrir (IDs: {string.Join(", ", sinCapturas)}). " +
                    "Cada trayecto reembolsable debe tener al menos una captura con monto, salvo que el área del trabajador tenga las capturas en opcional " +
                    "(o, para trabajadores de Tecnología de la Información, que el trayecto esté registrado en el catálogo).",
                    400);

            // 1.b.bis. Bloqueo: una planilla de rendición es de UN SOLO MES. Mezclar meses rompe la
            //          rendición mensual (el plazo, el correlativo y el propio documento son por
            //          periodo), así que se corta acá aunque la pantalla ya lo impida al seleccionar.
            var meses = await _repo.GetMesesDeSolicitudes(elegiblesIds);
            if (meses.Count > 1)
            {
                var listado = string.Join(", ", meses.Select(m => $"{m.Mes:D2}/{m.Anio}"));
                throw new AbrilException(
                    $"No se pueden rendir salidas de meses distintos en una sola planilla (se seleccionaron {listado}). " +
                    "Rinde un mes a la vez.", 400);
            }

            // 1.b.quater. Bloqueo: quiénes pueden compartir una planilla. No se mezclan jefaturas
            //          (JEFE, SUB GERENTE, RESIDENTE) con el resto del equipo —a una jefatura la
            //          firma su gerencia, y en un documento compartido acabaría firmando uno donde
            //          ella misma está incluida— y todos tienen que colgar de un mismo nodo de área
            //          por debajo de la gerencia. La regla es la MISMA que aplica el Consolidado del
            //          S10 al juntar planillas: vive en AgrupacionRendicionRule para que los dos
            //          extremos no puedan discrepar.
            await _repo.ValidarAgrupacionDeSolicitudes(elegiblesIds);

            // 1.b.ter. Bloqueo: el plazo del mes tiene que seguir abierto — los primeros días
            //          hábiles del mes siguiente (cuántos lo define Mis Rendiciones →
            //          Configuración → Días reembolsables), sin sábados, domingos ni los feriados
            //          de Configuración → Feriados. Hasta cuántos meses hacia atrás llega ese
            //          plazo, y si además hay un alcance que vale todo el mes, sale de esa misma
            //          configuración. Vencido, la salida solo se puede ver.
            // El calendario se carga una sola vez: lo usan el plazo del mes y, más abajo, el
            // reparto de fechas de la planilla (que además necesita el tope que viene con él).
            var calendario = await _repo.GetCalendarioNoLaborable();

            if (meses.Count == 1)
            {
                var limite = calendario.LimiteDeRendicion(meses[0].Anio, meses[0].Mes);
                if (MesAnteriorPeru.HoyPeru() > limite)
                    throw new AbrilException(
                        $"El plazo para rendir las salidas de {meses[0].Mes:D2}/{meses[0].Anio} venció el " +
                        $"{limite:dd/MM/yyyy} ({calendario.TextoDelLimite}). Ya no se pueden rendir.", 400);
            }

            // 1.c. Bloqueo: la salida tiene que llevar al menos un trayecto con gasto que rendir.
            //       Sin eso la planilla saldría sin una sola fila de esa salida: no se imprimen ni
            //       los trayectos sin reembolso ni los que resuelven a S/ 0.00.
            var noReembolsables = await _repo.GetIdsNoReembolsables(elegiblesIds);
            if (noReembolsables.Count > 0)
                throw new AbrilException(
                    $"No se puede rendir: {noReembolsables.Count} solicitud(es) no tienen ningún trayecto con gasto que rendir " +
                    $"(IDs: {string.Join(", ", noReembolsables)}). Solo se rinden los trayectos cuyo motivo está marcado " +
                    "como reembolsable en Configuración → Motivos, cuyo recorrido no está excluido en Configuración → " +
                    "Trayectos y cuyo importe es mayor a S/ 0.00.",
                    400);

            // 2. Cargar info, consumir el correlativo de planilla y generar PDF en memoria.
            //    Las fechas que imprime el PDF no son la fecha_salida cruda: lo que un día no
            //    aguanta se imputa al siguiente (RG-42). La solicitud no se toca.
            var datos          = await _repo.GetRendicionData(elegiblesIds);

            // Red de seguridad: GetRendicionData solo devuelve trayectos reembolsables, así que si
            // no queda ninguno no hay planilla que generar. Los guards de arriba ya lo impiden —
            // esto evita subir un PDF en blanco a SharePoint si alguna vez discreparan.
            if (datos.Count == 0)
                throw new AbrilException(
                    "No se puede rendir: ninguna de las salidas seleccionadas tiene trayectos reembolsables que imprimir.", 400);

            var fechas         = await ImputarFechasPlanillaAsync(datos, calendario, Array.Empty<int>());
            var numeroPlanilla = await _repo.GetNextNumeroPlanillaAsync();
            var numeroLabel    = $"TI: {numeroPlanilla:D6}";
            var pdf            = GenerarPlanillaPdf(datos, numeroLabel, fechas);

            // 3. Subir a SharePoint ANTES de marcar como rendidas.
            //    Si el upload falla, no se modifica nada en BD (estricto).
            var (pdfUrl, pdfItemId, filename) = await SubirPlanillaAsync(pdf, userId);

            // 4. Persistir GaRendicion + marcar solicitudes (transacción interna).
            var (rendicionId, codigo, rendidasIds) = await _repo.CrearRendicionYMarcarBulk(
                elegiblesIds, userId, pdfUrl, pdfItemId, filename, numeroPlanilla);

            return (pdf, rendidasIds.Count, rendicionId, codigo);
        }

        public async Task RegenerarPlanilla(int rendicionId, int userId)
        {
            // El PDF cubre la planilla entera, así que se regenera con TODAS sus salidas: acotarlo
            // a las del trabajador que subsana dejaría fuera a los demás grupos del documento.
            var planilla = await _repo.GetRendicionParaRegenerar(rendicionId)
                ?? throw new AbrilException("La planilla de rendición no existe.", 404);

            if (planilla.SolicitudIds.Count == 0)
                throw new AbrilException(
                    "La planilla no tiene salidas asociadas: no hay nada que volver a generar.", 409);

            // Mismo bloqueo que al rendir: cada trayecto tiene que seguir cubierto. Al subsanar se
            // pueden QUITAR capturas, así que sin esto una planilla podría volver a jefatura con un
            // trayecto sin sustento — justo lo contrario de lo que se pidió corregir.
            var sinCapturas = await _repo.GetIdsConTrayectosSinCapturas(planilla.SolicitudIds);
            if (sinCapturas.Count > 0)
                throw new AbrilException(
                    $"No se puede volver a generar: {sinCapturas.Count} salida(s) quedaron con trayectos sin " +
                    "cubrir. Cada trayecto necesita al menos una captura con monto, salvo que el área del " +
                    "trabajador tenga las capturas en opcional.", 400);

            var datos = await _repo.GetRendicionData(planilla.SolicitudIds);

            // Igual que al rendir: sin trayectos reembolsables no hay papel que regenerar. Pasa si
            // el catálogo cambió después de rendir (un motivo dejó de ser reembolsable), y es mejor
            // decirlo que reemplazar la planilla observada por un PDF vacío.
            if (datos.Count == 0)
                throw new AbrilException(
                    "No se puede volver a generar: las salidas de esta planilla ya no tienen trayectos reembolsables.", 409);

            // Las fechas se vuelven a repartir con los montos corregidos: si la subsanación bajó
            // un importe, lo que se había ido al día siguiente puede volver a caber en el suyo.
            var calendario = await _repo.GetCalendarioNoLaborable();
            var fechas     = await ImputarFechasPlanillaAsync(datos, calendario, new[] { rendicionId });

            // El número de planilla se reusa: el correlativo del papel no se consume otra vez
            // porque es el mismo documento corregido, no uno nuevo.
            var numeroLabel = PlanillaRendicionHelper.NumeroPlanilla(planilla.NumeroPlanilla) ?? string.Empty;
            var pdf         = GenerarPlanillaPdf(datos, numeroLabel, fechas);

            var (pdfUrl, pdfItemId, filename) = await SubirPlanillaAsync(pdf, userId);

            // El archivo anterior NO se borra de SharePoint: era el documento que el jefe observó y
            // queda como respaldo. Lo que cambia es a cuál apunta la planilla.
            await _repo.ReemplazarPdfRendicion(rendicionId, pdfUrl, pdfItemId, filename);
        }

        /// <summary>
        /// Sube el PDF de una planilla a la carpeta configurada y devuelve dónde quedó. La carpeta
        /// destino es configurable desde BD (<c>ga_rendicion_folder</c>): se guarda el link tal cual
        /// y se resuelve a driveId/folderId vía Graph, así es editable sin redeploy y cada entorno
        /// apunta a su propia biblioteca (mismo patrón que <c>ga_captura_folder</c>).
        ///
        /// Compartido por la rendición y por la regeneración de una planilla observada: las dos
        /// tienen que caer en la misma biblioteca y fallar igual si no está configurada.
        /// </summary>
        private async Task<(string PdfUrl, string? PdfItemId, string Filename)> SubirPlanillaAsync(
            byte[] pdf, int userId)
        {
            var folderUrl = await _repo.GetRendicionFolderUrl();
            if (string.IsNullOrWhiteSpace(folderUrl))
                throw new AbrilException(
                    "No se ha configurado la carpeta de SharePoint donde guardar las planillas de rendición. " +
                    "Pide al administrador registrarla en la tabla ga_rendicion_folder.", 409);

            var carpeta = await _sharePointService.ResolveSharePointFolderUrlAsync(folderUrl);
            if (carpeta == null || !carpeta.IsFolder)
                throw new AbrilException("No se pudo resolver la carpeta de planillas de rendición en SharePoint.", 502);

            var filename = $"Planilla_Rendicion_{DateTime.Now:yyyyMMdd_HHmmss}_u{userId}.pdf";
            try
            {
                using var pdfStream = new MemoryStream(pdf);
                var result = await _sharePointService.UploadToOneDriveFolderAsync(
                    carpeta.DriveId, carpeta.ItemId, filename, pdfStream,
                    "application/pdf",
                    autoRenameOnLock: true);

                if (result?.WebUrl is null)
                    throw new AbrilException("No se pudo subir la planilla a SharePoint (respuesta vacía).", 502);

                return (result.WebUrl, result.ItemId, filename);
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló upload de planilla a SharePoint (filename={Filename}). Rendición abortada.", filename);
                throw new AbrilException(
                    "No se pudo guardar la planilla en SharePoint. La rendición fue cancelada — vuelve a intentarlo.",
                    502);
            }
        }

        public async Task<(byte[] Pdf, int Count, int RendicionId, string Codigo)> RendirMes(
            GestionSalidaFiltersDto filters, int? anio, int? mes, int userId)
        {
            // El estado y el rango los fija la acción; los filtros de trabajador/área/proyecto que
            // trae la pantalla se respetan tal cual (se rinde lo que el usuario está viendo).
            var (desde, hasta) = anio.HasValue && mes.HasValue
                ? MesAnteriorPeru.RangoDe(anio.Value, mes.Value)
                : MesAnteriorPeru.Rango();

            // El plazo se revisa ANTES de buscar: si el periodo cerró, `SoloAptas` devolvería cero
            // filas y el error diría "no hay nada listo", que es cierto pero esconde el motivo real.
            var calendario = await _repo.GetCalendarioNoLaborable();
            var limite     = calendario.LimiteDeRendicion(desde.Year, desde.Month);
            if (MesAnteriorPeru.HoyPeru() > limite)
                throw new AbrilException(
                    $"El plazo para rendir las salidas de {desde:MM/yyyy} venció el {limite:dd/MM/yyyy} " +
                    $"({calendario.TextoDelLimite}). Ya no se pueden rendir.", 400);

            filters.SoloHoy          = false;
            filters.RendicionAnio    = null;
            filters.RendicionMes     = null;
            filters.FechaSalidaDesde = desde;
            filters.FechaSalidaHasta = hasta;
            filters.EstadoAprobacion = EstadosSalida.Aprobacion.NombreAprobado;
            filters.EstadoRendicion  = EstadosSalida.Rendicion.NombreNoRendido;
            filters.SoloAptas        = true;

            // GetAll ya resuelve la visibilidad y calcula AptaParaRendir (capturas / catálogo TI /
            // motivo reembolsable), así que la elegibilidad sale de ahí sin duplicar reglas.
            var ids = (await GetAll(filters)).Select(x => x.Id).ToList();
            if (ids.Count == 0)
                throw new AbrilException(
                    $"No hay salidas listas para rendir entre el {desde:dd/MM/yyyy} y el {hasta:dd/MM/yyyy}. " +
                    "Deben estar aprobadas, sin rendir, con las capturas de sus trayectos reembolsables y con al menos un trayecto reembolsable.", 400);

            return await RendirYGenerarPlanilla(ids, userId);
        }

        public Task<GestionSalidaDetalleDto?> GetDetalle(int id, int? currentUserId)
            => _repo.GetDetalle(id, currentUserId);


        // ── Generación de la planilla de gasto por movilidad (QuestPDF) ──────

        public async Task<byte[]> GenerarPlanillaGrupal(
            IReadOnlyCollection<int> rendicionIds, PlanillaReembolsoCabeceraDto cabecera)
        {
            var ids = rendicionIds.Distinct().ToList();
            if (ids.Count == 0)
                throw new AbrilException("No hay planillas con las que armar la planilla de reembolso.", 400);

            // Cada salida con el código de su planilla: es la columna RENDICIÓN.
            var rendicionPorSolicitud = await _repo.GetCodigoRendicionPorSolicitud(ids);
            if (rendicionPorSolicitud.Count == 0)
                throw new AbrilException(
                    "Las planillas del consolidado no tienen salidas: no hay planilla de reembolso que armar.", 409);

            var datos = await _repo.GetRendicionData(rendicionPorSolicitud.Keys.ToList());

            // Mismo corte que al rendir y al regenerar: GetRendicionData solo devuelve trayectos
            // reembolsables. Sin ninguno no hay papel que armar, y decirlo es mejor que subir un
            // documento en blanco al lado del Consolidado del S10.
            if (datos.Count == 0)
                throw new AbrilException(
                    "Las planillas del consolidado ya no tienen trayectos reembolsables: no se puede "
                    + "armar la planilla de reembolso.", 409);

            var calendario = await _repo.GetCalendarioNoLaborable();

            // Se excluyen TODAS las planillas del consolidado: sus salidas son las que se están
            // imputando, no un periodo ajeno ya rendido.
            var fechas = await ImputarFechasPlanillaAsync(datos, calendario, ids);

            return GenerarPlanillaReembolsoPdf(datos, cabecera, fechas, rendicionPorSolicitud);
        }

        /// <summary>
        /// Con qué FECHA sale impreso cada trayecto. La regla vive en
        /// <see cref="ImputacionMovilidadPlanilla"/>; acá se resuelve solo de dónde salen sus datos
        /// y qué pasa cuando el mes no alcanza.
        ///
        /// Se resuelve en dos pasadas a propósito. La primera no permite retroceder, que es el caso
        /// de siempre, y así no se paga la consulta de periodos ya rendidos en cada rendición. Solo
        /// si algo no entró hacia adelante se pregunta qué semanas o quincenas ya se rindieron y se
        /// vuelve a repartir, ahora sí pudiendo ir hacia atrás.
        /// </summary>
        /// <param name="rendicionIds">
        /// Las planillas que se están (re)generando, para no tomar sus propias salidas como un
        /// periodo ajeno. Vacío cuando se está rindiendo (la planilla todavía no existe); con
        /// varias cuando lo que se arma es la planilla grupal del consolidado.
        /// </param>
        private async Task<Dictionary<int, DateOnly>> ImputarFechasPlanillaAsync(
            List<RendicionItemDto> items, CalendarioNoLaborable calendario,
            IReadOnlyCollection<int> rendicionIds)
        {
            if (items.Count == 0) return new();

            var trayectos = items
                .Select(i => new ImputacionMovilidadPlanilla.Trayecto(
                    i.Id, i.WorkerId, i.FechaSalida, i.SolicitudId, i.Orden, i.Importe))
                .ToList();

            var imputacion = ImputacionMovilidadPlanilla.Resolver(trayectos, calendario);

            if (imputacion.SinUbicar.Count > 0)
            {
                var desde = trayectos.Min(t => t.FechaSalida);
                var hasta = trayectos.Max(t => t.FechaSalida);

                var periodos = await _repo.GetPeriodosRendidos(
                    trayectos.Select(t => t.WorkerId).Distinct().ToList(),
                    new DateOnly(desde.Year, desde.Month, 1),
                    new DateOnly(hasta.Year, hasta.Month, 1).AddMonths(1).AddDays(-1),
                    rendicionIds);

                imputacion = ImputacionMovilidadPlanilla.Resolver(trayectos, calendario, periodos);
            }

            if (imputacion.SinUbicar.Count > 0)
            {
                // El techo del mes es tope × días imputables. Llegar acá significa que ni
                // desplazando hacia adelante ni retrocediendo a lo que todavía no se rindió queda
                // un día libre: no es algo que la pantalla pueda arreglar sola, así que el mensaje
                // dice el número exacto contra el que se chocó.
                var sinUbicar  = imputacion.SinUbicar[0];
                var trabajador = items.First(i => i.WorkerId == sinUbicar.WorkerId).TrabajadorNombre;
                var dias       = calendario.DiasImputablesDelMes(
                    sinUbicar.FechaSalida.Year, sinUbicar.FechaSalida.Month);

                throw new AbrilException(
                    $"No se puede generar la planilla: la movilidad de {trabajador} en " +
                    $"{sinUbicar.FechaSalida:MM/yyyy} no entra en el mes. Con un tope de " +
                    $"S/ {calendario.LimiteMovilidad:N2} por día y {dias} días válidos (sin domingos ni " +
                    $"feriados), el máximo del mes es S/ {calendario.LimiteMovilidad * dias:N2}.", 400);
            }

            return imputacion.FechaPorTrayecto;
        }

        /// <summary>
        /// Fecha con la que el trayecto sale impreso. El fallback a <c>FechaSalida</c> no debería
        /// darse —la imputación cubre todos los trayectos o corta antes—, pero deja al PDF sin un
        /// hueco si alguna vez faltara uno.
        /// </summary>
        private static DateOnly FechaImpresa(
            RendicionItemDto it, IReadOnlyDictionary<int, DateOnly> fechas) =>
            fechas.TryGetValue(it.Id, out var fecha) ? fecha : it.FechaSalida;

        private const int FilasPorPagina = 15;

        /// <summary>Tamaño de letra de las celdas de la tabla — reducido para que entren 15 filas/página.</summary>
        private const float TablaFontSize = 7.5f;

        /// <summary>Máximo de líneas que puede ocupar el texto de una celda de la tabla.</summary>
        private const int TablaMaxLineas = 2;

        /// <summary>
        /// Margen de la hoja, salvo el inferior (ver <see cref="MargenInferiorPt"/>) y el derecho,
        /// que es el de <see cref="SignaturePdfStamper.SignatureMarginPt"/> para que la línea de
        /// firma termine donde termina la firma estampada.
        /// </summary>
        private const float MargenPt = 25f;

        /// <summary>
        /// Margen inferior de la hoja: el de <see cref="SignaturePdfStamper.PieMargenInferiorPt"/>.
        /// Debajo de la línea de firma se reserva el pie (<see cref="SignaturePdfStamper.PieAltoPt"/>)
        /// y la línea tiene que quedar exactamente donde el estampador apoya la firma al aprobar el
        /// reembolso: ahí el pie firmado —cargo, nombre, fecha— tapa la leyenda de la planilla. El
        /// número de registro compensa esta diferencia con un padding para seguir cayendo a
        /// <see cref="MargenPt"/> del borde.
        /// </summary>
        private const float MargenInferiorPt = (float)SignaturePdfStamper.PieMargenInferiorPt;

        /// <summary>
        /// Texto de la columna MOTIVO de la planilla: el motivo del catálogo con su detalle pegado
        /// cuando el motivo lo exige (requiere_motivo_adicional), que es lo que justifica el gasto.
        /// La celda recorta a <see cref="TablaMaxLineas"/> líneas, así que el detalle se captura
        /// acotado en el formulario para que entre junto al motivo.
        /// </summary>
        private static string MotivoConDetalle(string? motivo, string? motivoAdicional) =>
            string.IsNullOrWhiteSpace(motivoAdicional)
                ? (motivo ?? "")
                : $"{motivo} — {motivoAdicional}";

        private static string LogoPath() => Path.Combine(
            AppContext.BaseDirectory,
            "Features", "GestionAdministrativaModule", "Features", "GestionSalidasFeature",
            "Templates", "logo-abril.jpg");

        private static byte[]? _logoBytes;
        private static byte[]? GetLogoBytes()
        {
            if (_logoBytes != null) return _logoBytes;
            var path = LogoPath();
            if (!File.Exists(path)) return null;
            _logoBytes = File.ReadAllBytes(path);
            return _logoBytes;
        }

        private static byte[] GenerarPlanillaPdf(
            List<RendicionItemDto> items, string numeroLabel, IReadOnlyDictionary<int, DateOnly> fechas)
        {
            // Las filas salen ordenadas por la fecha IMPRESA, no por la real: un trayecto que se
            // corrió al día siguiente tiene que leerse en su día, no junto al que lo desplazó.
            var grupos = items
                .GroupBy(x => x.WorkerId)
                .Select(g => g
                    .OrderBy(x => FechaImpresa(x, fechas))
                    .ThenBy(x => x.FechaSalida)
                    .ThenBy(x => x.SolicitudId)
                    .ThenBy(x => x.Orden)
                    .ToList())
                .ToList();
            if (grupos.Count == 0) grupos.Add(new List<RendicionItemDto>());

            var paginas = new List<(List<RendicionItemDto> trabajadorItems, List<RendicionItemDto> pageItems, bool isLast, int pageNum, int totalPages)>();
            foreach (var g in grupos)
            {
                int totalPages = g.Count == 0 ? 1 : (int)Math.Ceiling(g.Count / (double)FilasPorPagina);
                for (int p = 0; p < totalPages; p++)
                {
                    var pageItems = g.Skip(p * FilasPorPagina).Take(FilasPorPagina).ToList();
                    paginas.Add((g, pageItems, p == totalPages - 1, p + 1, totalPages));
                }
            }

            var logo = GetLogoBytes();

            var doc = Document.Create(container =>
            {
                foreach (var pag in paginas)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.MarginTop(MargenPt);
                        page.MarginLeft(MargenPt);
                        page.MarginRight((float)SignaturePdfStamper.SignatureMarginPt);
                        page.MarginBottom(MargenInferiorPt);
                        page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(10));

                        page.Content().Element(c => RenderPagina(c, pag.trabajadorItems, pag.pageItems, pag.isLast, pag.pageNum, pag.totalPages, logo, numeroLabel, fechas));

                        // Pie de página común a todas las páginas — número de registro y "Página X
                        // de Y" cuando aplique (izq) y, al cerrar cada trabajador, su línea de
                        // firma pegada al borde inferior derecho.
                        var pageNum_    = pag.pageNum;
                        var totalPages_ = pag.totalPages;
                        var isLast_     = pag.isLast;
                        page.Footer().Element(f => PiePagina(f, numeroLabel, pageNum_, totalPages_, isLast_));
                    });
                }
            });

            return doc.GeneratePdf();
        }

        /// <summary>
        /// El pie de cada hoja, igual en la planilla individual y en la de reembolso: el número del
        /// documento con "Página X de Y" a la izquierda y, en la hoja que lo cierra, la línea de firma
        /// pegada al borde inferior derecho.
        /// </summary>
        private static void PiePagina(IContainer footer, string numeroLabel, int pageNum, int totalPages, bool conLineaFirma)
        {
            footer.PaddingTop(4).Row(footerRow =>
            {
                // Se compensa el margen inferior recortado para que el número de registro siga
                // cayendo a MargenPt del borde. La paginación va pegada a él y no a la derecha: esa
                // esquina es de las firmas, y cuando firma más de una jefatura la segunda se estampa
                // a la izquierda de la primera.
                footerRow.RelativeItem()
                    .PaddingBottom(MargenPt - MargenInferiorPt)
                    .AlignBottom()
                    .AlignLeft()
                    .Text(t =>
                    {
                        t.Span(numeroLabel).FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                        if (totalPages > 1)
                            t.Span($"      Página {pageNum} de {totalPages}")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                    });

                // El ancho es el mismo de la firma estampada y el bloque termina en el margen
                // derecho: así la línea queda justo debajo de la firma en vez de al lado.
                footerRow.ConstantItem((float)SignaturePdfStamper.SignatureWidthPt)
                    .Element(c => { if (conLineaFirma) LineaFirma(c); });
            });
        }

        // ── Planilla de reembolso (la planilla grupal del consolidado) ──────────

        /// <summary>Lo que la cabecera de la planilla de reembolso resume de sus filas.</summary>
        private sealed record ResumenReembolso(string Periodo, int Rendiciones, decimal Importe);

        /// <summary>Ancho de la etiqueta en la cabecera a dos columnas de la planilla de reembolso.</summary>
        private const float ReembolsoEtiquetaPt = 135f;

        /// <summary>
        /// La PLANILLA DE REEMBOLSO: los trayectos de todas las planillas del consolidado en una sola
        /// tabla —con la columna RENDICIÓN diciendo de cuál sale cada fila—, el código de la
        /// rendición grupal en el título y la cabecera del consolidador. Una sola línea de firma, al
        /// pie de la última hoja: la firma de la jefatura la estampa en todas.
        /// </summary>
        private static byte[] GenerarPlanillaReembolsoPdf(
            List<RendicionItemDto> items,
            PlanillaReembolsoCabeceraDto cabecera,
            IReadOnlyDictionary<int, DateOnly> fechas,
            IReadOnlyDictionary<int, string> rendicionPorSolicitud)
        {
            string RendicionDe(RendicionItemDto it) =>
                rendicionPorSolicitud.TryGetValue(it.SolicitudId, out var codigo) ? codigo : string.Empty;

            // Juntas las filas de cada rendición —es lo que distingue la columna nueva— y dentro de
            // cada una por la fecha IMPRESA, igual que la planilla individual.
            var filas = items
                .OrderBy(RendicionDe, StringComparer.Ordinal)
                .ThenBy(x => FechaImpresa(x, fechas))
                .ThenBy(x => x.FechaSalida)
                .ThenBy(x => x.SolicitudId)
                .ThenBy(x => x.Orden)
                .ToList();

            var resumen = new ResumenReembolso(
                Periodo: filas.Count > 0
                    ? $"{filas.Min(i => FechaImpresa(i, fechas)):dd/MM/yyyy}   AL   {filas.Max(i => FechaImpresa(i, fechas)):dd/MM/yyyy}"
                    : "",
                Rendiciones: filas.Select(RendicionDe).Where(c => c.Length > 0).Distinct().Count(),
                Importe: filas.Sum(i => i.Importe));

            var totalPages = Math.Max(1, (int)Math.Ceiling(filas.Count / (double)FilasPorPagina));
            var logo = GetLogoBytes();

            var doc = Document.Create(container =>
            {
                for (var p = 0; p < totalPages; p++)
                {
                    var pageItems = filas.Skip(p * FilasPorPagina).Take(FilasPorPagina).ToList();
                    var pageNum   = p + 1;
                    var isLast    = pageNum == totalPages;

                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.MarginTop(MargenPt);
                        page.MarginLeft(MargenPt);
                        page.MarginRight((float)SignaturePdfStamper.SignatureMarginPt);
                        page.MarginBottom(MargenInferiorPt);
                        page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(10));

                        page.Content().Element(c => RenderPaginaReembolso(
                            c, cabecera, resumen, pageItems, isLast, logo, fechas, RendicionDe, filas));

                        page.Footer().Element(f => PiePagina(f, cabecera.Codigo, pageNum, totalPages, isLast));
                    });
                }
            });

            return doc.GeneratePdf();
        }

        private static void RenderPaginaReembolso(
            IContainer container,
            PlanillaReembolsoCabeceraDto cabecera,
            ResumenReembolso resumen,
            List<RendicionItemDto> pageItems,
            bool isLastPage,
            byte[]? logo,
            IReadOnlyDictionary<int, DateOnly> fechas,
            Func<RendicionItemDto, string> rendicionDe,
            List<RendicionItemDto> todas)
        {
            var pe = CultureInfo.GetCultureInfo("es-PE");

            container.Column(col =>
            {
                col.Spacing(6);

                // ── Header: logo izquierda + caja título derecha ─────────────
                col.Item().Row(row =>
                {
                    row.ConstantItem(160).Height(45).Element(c =>
                    {
                        if (logo != null)
                            c.AlignLeft().AlignMiddle().Image(logo).FitArea();
                    });

                    row.RelativeItem(); // spacer

                    row.ConstantItem(400).Height(32).Row(titleRow =>
                    {
                        titleRow.RelativeItem(5).Border(1).AlignCenter().AlignMiddle()
                            .Text("PLANILLA DE REEMBOLSO").FontSize(11).Bold();
                        titleRow.RelativeItem(3).Border(1).PaddingHorizontal(4).AlignCenter().AlignMiddle()
                            .Text(cabecera.Codigo).FontSize(10).Bold();
                    });
                });

                // ── Cabecera: el consolidador (izq) y el consolidado (der) ────────
                col.Item().PaddingTop(3).Row(info =>
                {
                    info.RelativeItem().Column(izq =>
                    {
                        izq.Spacing(2);
                        izq.Item().Element(c => InfoLine(c, "RAZÓN SOCIAL:", cabecera.RazonSocial ?? "", ReembolsoEtiquetaPt));
                        izq.Item().Element(c => InfoLine(c, "RUC DEL CONSOLIDADOR:", cabecera.Ruc ?? "", ReembolsoEtiquetaPt));
                        izq.Item().Element(c => InfoLine(c, "CONSOLIDADOR:", cabecera.Consolidador ?? "", ReembolsoEtiquetaPt));
                        izq.Item().Element(c => InfoLine(c, "ÁREA:", cabecera.Area ?? "", ReembolsoEtiquetaPt));
                    });

                    info.ConstantItem(30);

                    info.RelativeItem().Column(der =>
                    {
                        der.Spacing(2);
                        der.Item().Element(c => InfoLine(c, "N.° DE REEMBOLSO:", cabecera.NumeroReembolso ?? "", ReembolsoEtiquetaPt));
                        der.Item().Element(c => InfoLine(c, "PERIODO DEL:", resumen.Periodo, ReembolsoEtiquetaPt));
                        der.Item().Element(c => InfoLine(c, "RENDICIONES:", resumen.Rendiciones.ToString(pe), ReembolsoEtiquetaPt));
                        der.Item().Element(c => InfoLine(c, "IMPORTE TOTAL:", $"S/ {resumen.Importe.ToString("N2", pe)}", ReembolsoEtiquetaPt));
                    });
                });

                // ── Tabla ────────────────────────────────────────────────────
                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(70);   // FECHA
                        c.ConstantColumn(95);   // RENDICIÓN
                        c.ConstantColumn(200);  // MOTIVO
                        c.ConstantColumn(160);  // ORIGEN
                        c.ConstantColumn(175);  // DESTINO
                        c.ConstantColumn(90);   // IMPORTE S/
                    });

                    table.Header(h =>
                    {
                        static IContainer Th(IContainer c) => c.Border(1).Background(Colors.Grey.Lighten4)
                            .PaddingVertical(3).AlignCenter().AlignMiddle();
                        h.Cell().Element(Th).Text("FECHA").Bold().FontSize(TablaFontSize);
                        h.Cell().Element(Th).Text("RENDICIÓN").Bold().FontSize(TablaFontSize);
                        h.Cell().Element(Th).Text("MOTIVO").Bold().FontSize(TablaFontSize);
                        h.Cell().Element(Th).Text("ORIGEN").Bold().FontSize(TablaFontSize);
                        h.Cell().Element(Th).Text("DESTINO").Bold().FontSize(TablaFontSize);
                        h.Cell().Element(Th).Text("IMPORTE S/").Bold().FontSize(TablaFontSize);
                    });

                    static IContainer Td(IContainer c) => c.Border(1).PaddingVertical(2).PaddingHorizontal(4).AlignMiddle();

                    // Texto de celda recortado a un máximo de 2 líneas (evita "textazos").
                    static void CeldaTexto(IContainer c, string value, bool center = false) =>
                        (center ? c.AlignCenter() : c)
                            .Text(value ?? "").FontSize(TablaFontSize).ClampLines(TablaMaxLineas);

                    foreach (var it in pageItems)
                    {
                        table.Cell().Element(Td).Element(c => CeldaTexto(c, FechaImpresa(it, fechas).ToString("dd/MM/yyyy"), center: true));
                        table.Cell().Element(Td).Element(c => CeldaTexto(c, rendicionDe(it)));
                        table.Cell().Element(Td).Element(c => CeldaTexto(c, MotivoConDetalle(it.Motivo, it.MotivoAdicional)));
                        table.Cell().Element(Td).Element(c => CeldaTexto(c, it.LugarOrigen ?? ""));
                        table.Cell().Element(Td).Element(c => CeldaTexto(c, it.LugarDestino ?? ""));
                        // Mismo criterio que la planilla individual: el importe del catálogo se
                        // muestra aunque sea 0.00; sin ninguna fuente, la celda queda vacía.
                        table.Cell().Element(Td).AlignRight().Text(
                            (it.EsCatalogo || it.Importe > 0)
                                ? it.Importe.ToString("N2", pe)
                                : "").FontSize(TablaFontSize);
                    }

                    if (isLastPage)
                    {
                        var totalGeneral = todas.Sum(i => i.Importe);
                        table.Cell().ColumnSpan(5).Border(1).PaddingVertical(7).PaddingHorizontal(8).AlignMiddle()
                            .Text(text =>
                            {
                                text.Span("TOTAL EN LETRAS: ").Bold();
                                text.Span(MontoEnLetrasSoles(totalGeneral));
                            });
                        table.Cell().Border(1).PaddingVertical(7).PaddingHorizontal(4).AlignMiddle().AlignRight()
                            .Text(totalGeneral.ToString("N2", pe)).Bold();
                    }
                });
            });
        }

        private static void RenderPagina(
            IContainer container,
            List<RendicionItemDto> trabajadorItems,
            List<RendicionItemDto> pageItems,
            bool isLastPage,
            int pageNum,
            int totalPages,
            byte[]? logo,
            string numeroLabel,
            IReadOnlyDictionary<int, DateOnly> fechas)
        {
            var first       = trabajadorItems.FirstOrDefault();
            var trabajador  = first?.TrabajadorNombre ?? "";
            var dni         = first?.TrabajadorDni    ?? "";
            var area        = first?.Area             ?? "";
            var razonSocial = first?.RazonSocial      ?? "";
            var ruc         = first?.Ruc              ?? "";
            // Label del documento: DNI (tipo 1) | CE (tipo 2) | DNI por defecto.
            var documentoLabel = first?.TrabajadorDocumentTypeId switch
            {
                2 => "CE:",
                _ => "DNI:",
            };
            // El periodo se lee de las fechas IMPRESAS para que coincida con la primera y la última
            // fila del documento: si un trayecto se corrió al día siguiente, el periodo lo abarca.
            string periodo  = trabajadorItems.Count > 0
                ? $"{trabajadorItems.Min(i => FechaImpresa(i, fechas)):dd/MM/yyyy}   AL   {trabajadorItems.Max(i => FechaImpresa(i, fechas)):dd/MM/yyyy}"
                : "";

            container.Column(col =>
            {
                col.Spacing(6);

                // ── Header: logo izquierda + caja título derecha ─────────────
                col.Item().Row(row =>
                {
                    row.ConstantItem(160).Height(45).Element(c =>
                    {
                        if (logo != null)
                            c.AlignLeft().AlignMiddle().Image(logo).FitArea();
                    });

                    row.RelativeItem(); // spacer

                    row.ConstantItem(380).Height(25).Row(titleRow =>
                    {
                        titleRow.RelativeItem(3).Border(1).AlignCenter().AlignMiddle()
                            .Text("PLANILLA DE GASTO POR MOVILIDAD Nº")
                            .FontSize(11).Bold();
                        titleRow.RelativeItem(1).Border(1).AlignCenter().AlignMiddle()
                            .Text(numeroLabel).FontSize(10).Bold();
                    });
                });

                // ── Info section ─────────────────────────────────────────────
                col.Item().PaddingTop(3).Column(info =>
                {
                    info.Spacing(2);

                    info.Item().Element(c => InfoLine(c, "RAZÓN SOCIAL:", razonSocial));

                    info.Item().Row(r =>
                    {
                        r.RelativeItem().Element(c => InfoLine(c, "RUC:", ruc));
                        r.RelativeItem().Element(c => InfoLine(c, "NOMBRE DEL ÁREA/PROYECTO:", area));
                    });

                    info.Item().Element(c => InfoLine(c, "NOMBRES Y APELLIDOS:", trabajador));

                    info.Item().Row(r =>
                    {
                        r.RelativeItem().Element(c => InfoLine(c, documentoLabel, dni));
                        r.RelativeItem().Element(c => InfoLine(c, "PERIODO DEL:", periodo));
                    });
                });

                // ── Tabla ────────────────────────────────────────────────────
                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(70);   // FECHA
                        c.ConstantColumn(200);  // MOTIVO
                        c.ConstantColumn(180);  // ORIGEN
                        c.ConstantColumn(200);  // DESTINO
                        c.ConstantColumn(90);   // IMPORTE S/
                    });

                    table.Header(h =>
                    {
                        static IContainer Th(IContainer c) => c.Border(1).Background(Colors.Grey.Lighten4)
                            .PaddingVertical(3).AlignCenter().AlignMiddle();
                        h.Cell().Element(Th).Text("FECHA").Bold().FontSize(TablaFontSize);
                        h.Cell().Element(Th).Text("MOTIVO").Bold().FontSize(TablaFontSize);
                        h.Cell().Element(Th).Text("ORIGEN").Bold().FontSize(TablaFontSize);
                        h.Cell().Element(Th).Text("DESTINO").Bold().FontSize(TablaFontSize);
                        h.Cell().Element(Th).Text("IMPORTE S/").Bold().FontSize(TablaFontSize);
                    });

                    static IContainer Td(IContainer c) => c.Border(1).PaddingVertical(2).PaddingHorizontal(4).AlignMiddle();

                    // Texto de celda recortado a un máximo de 2 líneas (evita "textazos").
                    static void CeldaTexto(IContainer c, string value, bool center = false) =>
                        (center ? c.AlignCenter() : c)
                            .Text(value ?? "").FontSize(TablaFontSize).ClampLines(TablaMaxLineas);

                    foreach (var it in pageItems)
                    {
                        table.Cell().Element(Td).Element(c => CeldaTexto(c, FechaImpresa(it, fechas).ToString("dd/MM/yyyy"), center: true));
                        table.Cell().Element(Td).Element(c => CeldaTexto(c, MotivoConDetalle(it.Motivo, it.MotivoAdicional)));
                        table.Cell().Element(Td).Element(c => CeldaTexto(c, it.LugarOrigen ?? ""));
                        table.Cell().Element(Td).Element(c => CeldaTexto(c, it.LugarDestino ?? ""));
                        // Importe: mostrar siempre que venga del catálogo (incluso si es 0.00)
                        // o cuando la suma de capturas sea > 0. Si el trayecto no tiene
                        // ninguna fuente, dejar la celda vacía.
                        table.Cell().Element(Td).AlignRight().Text(
                            (it.EsCatalogo || it.Importe > 0)
                                ? it.Importe.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))
                                : "").FontSize(TablaFontSize);
                    }

                    if (isLastPage)
                    {
                        var totalGeneral = trabajadorItems.Sum(i => i.Importe);
                        var totalEnLetras = MontoEnLetrasSoles(totalGeneral);
                        table.Cell().ColumnSpan(4).Border(1).PaddingVertical(7).PaddingHorizontal(8).AlignMiddle()
                            .Text(text =>
                            {
                                text.Span("TOTAL EN LETRAS: ").Bold();
                                text.Span(totalEnLetras);
                            });
                        table.Cell().Border(1).PaddingVertical(7).PaddingHorizontal(4).AlignMiddle().AlignRight()
                            .Text(totalGeneral.ToString("N2", CultureInfo.GetCultureInfo("es-PE"))).Bold();
                    }
                });

                // (La línea de firma va en el pie de la página, no acá: tiene que quedar
                //  siempre al borde inferior derecho, que es donde se estampa la firma.)
            });
        }

        /// <summary>
        /// Línea de firma con su leyenda debajo, pensada para el pie derecho de la hoja. Al aprobar
        /// el reembolso, <see cref="SignaturePdfStamper"/> estampa la firma apoyada sobre esa misma
        /// línea y su pie —cargo, nombre y fecha— en el espacio reservado debajo, tapando la
        /// leyenda: la planilla firmada dice quién firmó en vez de "Firma de Jefatura / Gerencia".
        /// </summary>
        private static void LineaFirma(IContainer container)
        {
            container.AlignBottom().Column(fc =>
            {
                fc.Item().LineHorizontal(SignaturePdfStamper.LineaFirmaGrosorPt);
                // Alto fijo: es el mismo que ocupa el pie firmado, así la línea queda exactamente
                // donde el estampador dibuja la suya. La leyenda va en el renglón del cargo.
                fc.Item().Height((float)SignaturePdfStamper.PieAltoPt).PaddingTop(2).AlignCenter()
                    .Text(SignaturePdfStamper.LeyendaSinCargo).FontSize(7.5f).Italic();
            });
        }

        /// <param name="labelWidth">
        /// Ancho de la etiqueta. La planilla individual usa el de siempre; la de reembolso, que lleva
        /// la cabecera a dos columnas, uno menor.
        /// </param>
        private static void InfoLine(IContainer container, string label, string value, float labelWidth = 155f)
        {
            container.Row(r =>
            {
                r.ConstantItem(labelWidth).AlignMiddle().Text(label).Bold().FontSize(9);
                r.RelativeItem().BorderBottom(0.6f).BorderColor(Colors.Grey.Darken1)
                    .PaddingBottom(1).AlignMiddle()
                    .Text(value ?? "").FontSize(9);
            });
        }

        /// <summary>
        /// Convierte un monto a su representación en letras estilo peruano
        /// (ej. 350.50 → "TRESCIENTOS CINCUENTA CON 50/100 SOLES").
        /// </summary>
        private static string MontoEnLetrasSoles(decimal monto)
        {
            var abs       = Math.Abs(monto);
            var entero    = (long)Math.Truncate(abs);
            var centavos  = (int)Math.Round((abs - entero) * 100m);
            if (centavos == 100) { entero++; centavos = 0; }

            var esCulture = new CultureInfo("es");
            var palabras  = entero.ToWords(esCulture);
            var signo     = monto < 0 ? "MENOS " : string.Empty;
            return $"{signo}{palabras} CON {centavos:D2}/100 SOLES".ToUpperInvariant();
        }
    }
}
