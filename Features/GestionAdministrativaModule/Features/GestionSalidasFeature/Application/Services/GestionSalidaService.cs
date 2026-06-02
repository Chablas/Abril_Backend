using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Options;
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
        private readonly SharePointSiteRef _site;
        private readonly string _solicitudSalidasLibraryId;
        private readonly ILogger<GestionSalidaService> _logger;

        private const string CarpetaSolicitudesRendidas = "Solicitudes rendidas";

        public GestionSalidaService(
            IGestionSalidaRepository repo,
            IGraphSharePointService sharePointService,
            ISolicitudSalidaService solicitudSalidaService,
            IConfiguration configuration,
            ILogger<GestionSalidaService> logger)
        {
            _repo = repo;
            _sharePointService = sharePointService;
            _solicitudSalidaService = solicitudSalidaService;
            _logger = logger;
            _site = SharePointSiteRef.FromConfig(configuration, "CostosYPresupuestos");
            _solicitudSalidasLibraryId = configuration["SharePoint:Sites:CostosYPresupuestos:SolicitudSalidasLibraryId"]
                ?? throw new InvalidOperationException("SharePoint:Sites:CostosYPresupuestos:SolicitudSalidasLibraryId no está configurado.");
        }

        public Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters)
            => _repo.GetAll(filters);

        public Task<GestionSalidaFilterDataDto> GetFilterData()
            => _repo.GetFilterData();

        public async Task<byte[]> GetExcel(GestionSalidaFiltersDto filters)
        {
            var salidas = await _repo.GetAll(filters);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Gestión de Salidas");

            string[] headers =
            [
                "#", "Trabajador", "Fecha salida", "Hora salida", "Hora retorno",
                "Motivo", "Origen", "Destino", "Aprobación", "Rendición", "Registrada",
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
                ws.Cell(row, 3).Value  = s.FechaSalida.ToString("dd/MM/yyyy");
                ws.Cell(row, 4).Value  = s.HoraSalida.ToString("HH:mm");
                ws.Cell(row, 5).Value  = s.HoraRetorno.HasValue ? s.HoraRetorno.Value.ToString("HH:mm") : "—";
                ws.Cell(row, 6).Value  = s.Motivo;
                ws.Cell(row, 7).Value  = s.LugarOrigen  ?? "—";
                ws.Cell(row, 8).Value  = s.LugarDestino ?? "—";
                ws.Cell(row, 9).Value  = s.EstadoAprobacion;
                ws.Cell(row, 10).Value = s.EstadoRendicion;
                ws.Cell(row, 11).Value = s.CreatedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm");

                var rowRange = ws.Range(row, 1, row, headers.Length);
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
                rowRange.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;

                if (r % 2 == 0)
                    rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFAFA");

                var aprobacionCell = ws.Cell(row, 9);
                aprobacionCell.Style.Font.Bold = true;
                aprobacionCell.Style.Font.FontColor = s.EstadoAprobacion switch
                {
                    "Aprobado"  => XLColor.FromHtml("#009C87"),
                    "Rechazado" => XLColor.FromHtml("#D30000"),
                    _           => XLColor.FromHtml("#92400E"),
                };

                var rendicionCell = ws.Cell(row, 10);
                rendicionCell.Style.Font.Bold = true;
                rendicionCell.Style.Font.FontColor = s.EstadoRendicion == "Rendido"
                    ? XLColor.FromHtml("#0086A5")
                    : XLColor.FromHtml("#9CA3AF");
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task Aprobar(int id, int reviewerUserId)
        {
            await _repo.Aprobar(id, reviewerUserId);
            // Email de confirmación al solicitante (best-effort, no rompe el flujo si falla)
            await _solicitudSalidaService.NotifySolicitanteAprobada(id);
        }

        public Task Rechazar(int id, int reviewerUserId)
            => _repo.Rechazar(id, reviewerUserId);

        public Task SetHoraSalidaReal(int id, TimeOnly? hora, int registradaPorUserId)
            => _repo.SetHoraSalidaReal(id, hora, registradaPorUserId);

        public async Task<(byte[] Pdf, int Count)> RendirYGenerarPlanilla(IEnumerable<int> ids, int userId)
        {
            // 1. Pre-flight: ¿cuáles serían marcables? — sin tocar BD.
            var elegiblesIds = await _repo.GetEligibleIdsForRendicion(ids);
            if (elegiblesIds.Count == 0)
                throw new AbrilException("No hay solicitudes elegibles para rendir (deben estar aprobadas y no rendidas).", 400);

            // 1.b. Bloqueo: cada trayecto de cada solicitud debe estar cubierto.
            //       Regla normal: trayecto con al menos 1 captura.
            //       Regla TI (Tecnología de la Información): captura O match contra ga_trayecto.
            var sinCapturas = await _repo.GetIdsConTrayectosSinCapturas(elegiblesIds);
            if (sinCapturas.Count > 0)
                throw new AbrilException(
                    $"No se puede rendir: {sinCapturas.Count} solicitud(es) tienen trayectos sin cubrir (IDs: {string.Join(", ", sinCapturas)}). " +
                    "Cada trayecto debe tener al menos una captura con monto, o (para trabajadores de Tecnología de la Información) un trayecto registrado en el catálogo.",
                    400);

            // 2. Cargar info y generar PDF en memoria.
            var datos = await _repo.GetRendicionData(elegiblesIds);
            var pdf   = GenerarPlanillaPdf(datos);

            // 3. Subir a SharePoint ANTES de marcar como rendidas.
            //    Si el upload falla, no se modifica nada en BD (estricto).
            var filename = $"Planilla_Rendicion_{DateTime.Now:yyyyMMdd_HHmmss}_u{userId}.pdf";
            string pdfUrl;
            string? pdfItemId;
            try
            {
                using var pdfStream = new MemoryStream(pdf);
                var result = await _sharePointService.UploadToSharePointLibraryAsync(
                    site:        _site,
                    libraryName: _solicitudSalidasLibraryId,
                    folderPath:  CarpetaSolicitudesRendidas,
                    fileName:    filename,
                    fileStream:  pdfStream,
                    contentType: "application/pdf");

                if (result?.WebUrl is null)
                    throw new AbrilException("No se pudo subir la planilla a SharePoint (respuesta vacía).", 502);

                pdfUrl    = result.WebUrl;
                pdfItemId = result.ItemId;
            }
            catch (AbrilException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló upload de planilla a SharePoint (filename={Filename}). Rendición abortada.", filename);
                throw new AbrilException(
                    "No se pudo guardar la planilla en SharePoint. La rendición fue cancelada — vuelve a intentarlo.",
                    502);
            }

            // 4. Persistir GaRendicion + marcar solicitudes (transacción interna).
            var rendidasIds = await _repo.CrearRendicionYMarcarBulk(
                elegiblesIds, userId, pdfUrl, pdfItemId, filename);

            return (pdf, rendidasIds.Count);
        }

        public Task<GestionSalidaDetalleDto?> GetDetalle(int id)
            => _repo.GetDetalle(id);

        // ── Generación de la planilla de gasto por movilidad (QuestPDF) ──────

        private const int FilasPorPagina = 10;

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

        private static byte[] GenerarPlanillaPdf(List<RendicionItemDto> items)
        {
            var grupos = items.GroupBy(x => x.WorkerId).Select(g => g.ToList()).ToList();
            if (grupos.Count == 0) grupos.Add(new List<RendicionItemDto>());

            // Una "página lógica" por chunk de FilasPorPagina dentro de cada grupo.
            // En QuestPDF lo modelamos como múltiples Document.Page() — cada chunk es su propia página.
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
                        page.Margin(25);
                        page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(10));

                        page.Content().Element(c => RenderPagina(c, pag.trabajadorItems, pag.pageItems, pag.isLast, pag.pageNum, pag.totalPages, logo));
                    });
                }
            });

            return doc.GeneratePdf();
        }

        private static void RenderPagina(
            IContainer container,
            List<RendicionItemDto> trabajadorItems,
            List<RendicionItemDto> pageItems,
            bool isLastPage,
            int pageNum,
            int totalPages,
            byte[]? logo)
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
            string periodo  = trabajadorItems.Count > 0
                ? $"{trabajadorItems.Min(i => i.FechaSalida):dd/MM/yyyy}   AL   {trabajadorItems.Max(i => i.FechaSalida):dd/MM/yyyy}"
                : "";

            container.Column(col =>
            {
                col.Spacing(10);

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
                        titleRow.RelativeItem(1).Border(1); // caja vacía para el número
                    });
                });

                // ── Info section ─────────────────────────────────────────────
                col.Item().PaddingTop(5).Column(info =>
                {
                    info.Spacing(4);

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
                col.Item().PaddingTop(10).Table(table =>
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
                            .PaddingVertical(6).AlignCenter().AlignMiddle();
                        h.Cell().Element(Th).Text("FECHA").Bold();
                        h.Cell().Element(Th).Text("MOTIVO").Bold();
                        h.Cell().Element(Th).Text("ORIGEN").Bold();
                        h.Cell().Element(Th).Text("DESTINO").Bold();
                        h.Cell().Element(Th).Text("IMPORTE S/").Bold();
                    });

                    static IContainer Td(IContainer c) => c.Border(1).PaddingVertical(5).PaddingHorizontal(4).AlignMiddle();

                    foreach (var it in pageItems)
                    {
                        table.Cell().Element(Td).AlignCenter().Text(it.FechaSalida.ToString("dd/MM/yyyy"));
                        table.Cell().Element(Td).Text(it.Motivo ?? "");
                        table.Cell().Element(Td).Text(it.LugarOrigen ?? "");
                        table.Cell().Element(Td).Text(it.LugarDestino ?? "");
                        // Importe: mostrar siempre que venga del catálogo (incluso si es 0.00)
                        // o cuando la suma de capturas sea > 0. Si el trayecto no tiene
                        // ninguna fuente, dejar la celda vacía.
                        table.Cell().Element(Td).AlignRight().Text(
                            (it.EsCatalogo || it.Importe > 0)
                                ? it.Importe.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"))
                                : "");
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

                // ── Firmas (solo última página del trabajador) ───────────────
                if (isLastPage)
                {
                    col.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Column(fc =>
                        {
                            fc.Item().LineHorizontal(0.7f);
                            fc.Item().PaddingTop(2).AlignCenter()
                                .Text("Firma de Jefatura").FontSize(9).Italic();
                        });
                        row.ConstantItem(60); // spacer
                        row.RelativeItem().AlignCenter().Column(fc =>
                        {
                            fc.Item().LineHorizontal(0.7f);
                            fc.Item().PaddingTop(2).AlignCenter()
                                .Text("Firma de Gerencia").FontSize(9).Italic();
                        });
                    });
                }

                // ── Indicador de página (si el trabajador ocupa varias) ──────
                if (totalPages > 1)
                {
                    col.Item().PaddingTop(8).AlignRight()
                        .Text($"Página {pageNum} de {totalPages}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                }
            });
        }

        private static void InfoLine(IContainer container, string label, string value)
        {
            container.Row(r =>
            {
                r.ConstantItem(170).AlignMiddle().Text(label).Bold().FontSize(10);
                r.RelativeItem().BorderBottom(0.6f).BorderColor(Colors.Grey.Darken1)
                    .PaddingBottom(2).AlignMiddle()
                    .Text(value ?? "").FontSize(10);
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
