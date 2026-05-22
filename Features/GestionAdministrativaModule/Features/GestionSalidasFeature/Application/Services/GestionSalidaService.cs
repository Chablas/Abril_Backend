using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Interfaces;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Services
{
    public class GestionSalidaService : IGestionSalidaService
    {
        private readonly IGestionSalidaRepository _repo;

        public GestionSalidaService(IGestionSalidaRepository repo)
        {
            _repo = repo;
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

        public Task Aprobar(int id, int reviewerUserId)
            => _repo.Aprobar(id, reviewerUserId);

        public Task Rechazar(int id, int reviewerUserId)
            => _repo.Rechazar(id, reviewerUserId);

        public async Task<(byte[] Pdf, int Count)> RendirYGenerarPlanilla(IEnumerable<int> ids, int userId)
        {
            var rendidasIds = await _repo.MarcarRendidasBulk(ids, userId);
            var datos       = await _repo.GetRendicionData(rendidasIds);
            var pdf         = GenerarPlanillaPdf(datos);
            return (pdf, rendidasIds.Count);
        }

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

                    info.Item().Element(c => InfoLine(c, "APELLIDOS Y NOMBRE:", trabajador));

                    info.Item().Row(r =>
                    {
                        r.RelativeItem().Element(c => InfoLine(c, "DNI:", dni));
                        r.RelativeItem().Element(c => InfoLine(c, "PERIODO DEL:", periodo));
                    });
                });

                // ── Tabla ────────────────────────────────────────────────────
                col.Item().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(80);   // FECHA
                        c.ConstantColumn(250);  // MOTIVO
                        c.ConstantColumn(130);  // PARTIDA
                        c.ConstantColumn(200);  // DESTINO
                        c.ConstantColumn(80);   // IMPORTE S/
                    });

                    table.Header(h =>
                    {
                        static IContainer Th(IContainer c) => c.Border(1).Background(Colors.Grey.Lighten4)
                            .PaddingVertical(6).AlignCenter().AlignMiddle();
                        h.Cell().Element(Th).Text("FECHA").Bold();
                        h.Cell().Element(Th).Text("MOTIVO").Bold();
                        h.Cell().Element(Th).Text("PARTIDA").Bold();
                        h.Cell().Element(Th).Text("DESTINO").Bold();
                        h.Cell().Element(Th).Text("IMPORTE S/").Bold();
                    });

                    static IContainer Td(IContainer c) => c.Border(1).PaddingVertical(5).PaddingHorizontal(4).AlignMiddle();

                    foreach (var it in pageItems)
                    {
                        table.Cell().Element(Td).AlignCenter().Text(it.FechaSalida.ToString("dd/MM/yyyy"));
                        table.Cell().Element(Td).Text(it.Motivo ?? "");
                        table.Cell().Element(Td).Text("");
                        table.Cell().Element(Td).Text(it.LugarDestino ?? "");
                        table.Cell().Element(Td).AlignRight().Text("");
                    }

                    if (isLastPage)
                    {
                        table.Cell().ColumnSpan(4).Border(1).PaddingVertical(7).PaddingHorizontal(8).AlignMiddle()
                            .Text("TOTAL EN LETRAS:").Bold();
                        table.Cell().Border(1);
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
                                .Text("Firma del Residente").FontSize(9).Italic();
                        });
                        row.ConstantItem(60); // spacer
                        row.RelativeItem().AlignCenter().Column(fc =>
                        {
                            fc.Item().LineHorizontal(0.7f);
                            fc.Item().PaddingTop(2).AlignCenter()
                                .Text("Firma del Responsable").FontSize(9).Italic();
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
    }
}
