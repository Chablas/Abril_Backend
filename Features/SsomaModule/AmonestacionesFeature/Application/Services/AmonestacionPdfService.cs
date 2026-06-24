using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Services;

public static class AmonestacionPdfService
{
    private static readonly string AbrilLogoPath =
        Path.Combine(AppContext.BaseDirectory, "Templates", "logo-abril.jpg");

    public static byte[] GenerarPdf(AmonestacionDetalleDto a, List<byte[]> fotoBytes, byte[]? logoBytes)
    {
        var doc = Document.Create(container =>
        {
            // Imprimimos 2 copias idénticas en una sola hoja A4 con línea de corte en el centro
            for (int copia = 0; copia < 2; copia++)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9));

                    page.Content().Column(col =>
                    {
                        // ── Contenido de la papeleta ─────────────────────
                        col.Item().Element(c => RenderPapeleta(c, a, fotoBytes, logoBytes));

                        // Separador con línea de corte
                        if (copia == 0)
                        {
                            col.Item().PaddingVertical(6).Row(row =>
                            {
                                row.RelativeItem().Height(0.5f).Background(Colors.Grey.Lighten1);
                                row.ConstantItem(60).AlignCenter().PaddingHorizontal(6)
                                    .Text("✂ CORTAR").FontSize(7).FontColor(Colors.Grey.Darken1);
                                row.RelativeItem().Height(0.5f).Background(Colors.Grey.Lighten1);
                            });
                            col.Item().Element(c => RenderPapeleta(c, a, fotoBytes, logoBytes));
                        }
                    });
                });
            }
        });

        return doc.GeneratePdf();
    }

    private static void RenderPapeleta(IContainer container, AmonestacionDetalleDto a,
        List<byte[]> fotos, byte[]? logoBytes)
    {
        container.Border(1).BorderColor(Colors.Grey.Medium).Padding(10).Column(col =>
        {
            // ── HEADER ────────────────────────────────────────────
            col.Item().Row(row =>
            {
                // Logo
                row.ConstantItem(60).AlignMiddle().Column(logoCol =>
                {
                    if (a.EsEmpresaAbril && logoBytes != null)
                        logoCol.Item().Image(logoBytes).FitWidth();
                    else if (!a.EsEmpresaAbril && logoBytes != null)
                        logoCol.Item().Image(logoBytes).FitWidth();
                    else
                        logoCol.Item().Height(40).Background(Colors.Grey.Lighten3);
                });

                // Título
                row.RelativeItem().PaddingLeft(8).AlignMiddle().Column(titleCol =>
                {
                    titleCol.Item().Text("PAPELETA DE AMONESTACIÓN Y\nNOTIFICACIÓN DE RIESGO")
                        .Bold().FontSize(11).AlignCenter();
                });

                // Info documento
                row.ConstantItem(80).AlignMiddle().Column(infoCol =>
                {
                    infoCol.Item().Text("SSO-F-107").FontSize(7).Bold();
                    infoCol.Item().Text("Versión: 01").FontSize(7);
                    infoCol.Item().Text($"Fecha: {DateTime.UtcNow:dd/MM/yyyy}").FontSize(7);
                    infoCol.Item().Text($"Aprobó: GP").FontSize(7);
                });
            });

            col.Item().PaddingVertical(4).Height(0.5f).Background(Colors.Grey.Medium);

            // ── FILA 1: Puntaje, Código, Fecha, Proyecto, Penalización ──
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.2f); c.RelativeColumn(1.5f); c.RelativeColumn(1);
                    c.RelativeColumn(2.5f); c.RelativeColumn(1); c.RelativeColumn(1);
                });

                void CeldaLabel(string t) => table.Cell().Padding(3).Background(Colors.Grey.Lighten3).Text(t).Bold().FontSize(8);
                void CeldaValor(string t) => table.Cell().Padding(3).Text(t).FontSize(8);

                CeldaLabel("Puntaje acumulado");
                table.Cell().Padding(3).Background(
                    a.Inhabilitado ? Colors.Red.Lighten2 : Colors.Yellow.Lighten2)
                    .Text($"{a.PuntosAcumulados}/10")
                    .Bold().FontSize(10).FontColor(a.Inhabilitado ? Colors.Red.Darken2 : Colors.Grey.Darken2);

                CeldaLabel("Código");
                CeldaValor(a.Codigo);
                CeldaLabel("Fecha");
                CeldaValor(a.Fecha.ToString("dd/MM/yyyy"));
            });

            // ── FILA 2: Proyecto, Penalización ──
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1); c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(1);
                });

                table.Cell().Padding(3).Background(Colors.Grey.Lighten3).Text("Proyecto").Bold().FontSize(8);
                table.Cell().Padding(3).Text(a.ProyectoNombre).FontSize(8);
                table.Cell().Padding(3).Background(Colors.Grey.Lighten3).Text("Penalización").Bold().FontSize(8);
                table.Cell().Padding(3).Text(a.AplicaPenalizacion ? "Sí" : "No").FontSize(8);
            });

            // ── FILA 3: Sanción, Monto ──
            if (a.AplicaPenalizacion)
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1); c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(1);
                    });

                    table.Cell().Padding(3).Background(Colors.Grey.Lighten3).Text("Sanción").Bold().FontSize(8);
                    table.Cell().Padding(3).Text(a.SancionInfraccionNombre ?? "-").FontSize(8);
                    table.Cell().Padding(3).Background(Colors.Grey.Lighten3).Text("Monto (S/)").Bold().FontSize(8);
                    table.Cell().Padding(3).Text(a.MontoCalculado > 0 ? $"S/ {a.MontoCalculado:N2}" : "-").FontSize(8).Bold();
                });
            }

            // ── SECCIÓN: Datos del trabajador ──
            col.Item().PaddingTop(6).Background(Colors.Grey.Darken2).Padding(4)
                .Text("Datos del trabajador notificado").Bold().FontSize(9).FontColor(Colors.White);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1);
                });

                table.Cell().ColumnSpan(4).Padding(3).Text(a.WorkerNombre).FontSize(9).Bold();

                void Label(string t) => table.Cell().Padding(3).Background(Colors.Grey.Lighten3).Text(t).Bold().FontSize(8);
                void Valor(string t) => table.Cell().Padding(3).Text(t).FontSize(8);

                Label("Edad"); Valor(a.WorkerEdad?.ToString() ?? "-");
                Label("DNI"); Valor(a.WorkerDni);
                Label("Categoría"); Valor(a.WorkerCargo ?? "-");

                Label("Partida"); Valor(a.PartidaNombre ?? "-");
                Label("Empresa"); Valor(a.EmpresaNombre);
                table.Cell().ColumnSpan(2).Padding(3).Text("").FontSize(8);
            });

            // Tipo de sanción
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(3); });
                table.Cell().Padding(3).Background(Colors.Grey.Lighten3).Text("Tipo de sanción").Bold().FontSize(8);
                table.Cell().Padding(3).Text(a.TipoSancionNombre).FontSize(8);
                table.Cell().Padding(3).Background(Colors.Grey.Lighten3).Text("Infracción aplicada al trabajador amonestado").Bold().FontSize(8);
                table.Cell().Padding(3).Text(a.InfraccionTipoNombre).FontSize(8);
            });

            // ── SECCIÓN: Descripción ──
            col.Item().PaddingTop(4).Background(Colors.Grey.Lighten3).Padding(3)
                .Text("Descripción de lo ocurrido").Bold().FontSize(8);
            col.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).MinHeight(40)
                .Text(a.Descripcion).FontSize(8);

            // ── FILA: Puntos, Días suspensión, Fechas ──
            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1);
                    c.RelativeColumn(1); c.RelativeColumn(1);
                });

                void Label(string t) => table.Cell().Padding(3).Background(Colors.Grey.Lighten3).Text(t).Bold().FontSize(8);
                void Valor(string t) => table.Cell().Padding(3).Text(t).FontSize(8);

                Label("Puntos por Infracción"); Valor(a.PuntosInfraccion.ToString());
                Label("Total días de suspensión"); Valor(a.DiasSuspension?.ToString() ?? "-");
                table.Cell().ColumnSpan(2).Padding(3).Text("").FontSize(8);

                Label("Fecha de Inicio"); Valor(a.FechaInicioSuspension?.ToString("dd/MM/yyyy") ?? "-");
                Label("Fecha de Término"); Valor(a.FechaFinSuspension?.ToString("dd/MM/yyyy") ?? "-");
                Label("Persona que reporta"); Valor(a.PersonaReportaNombre ?? "-");
                table.Cell().Padding(3).Text("").FontSize(8);
            });

            // ── Fotos ──
            if (fotos.Count > 0)
            {
                col.Item().PaddingTop(6).Grid(grid =>
                {
                    grid.Columns(2);
                    grid.Spacing(4);
                    foreach (var fb in fotos)
                        grid.Item().MaxHeight(80).Image(fb).FitWidth();
                });
            }
            else
            {
                col.Item().PaddingTop(4).Row(row =>
                {
                    for (int i = 0; i < 2; i++)
                        row.RelativeItem().Height(60).Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Background(Colors.Grey.Lighten4).AlignCenter().AlignMiddle()
                            .Text("Sin imagen").FontSize(7).FontColor(Colors.Grey.Medium);
                });
            }

            // Inhabilitado
            if (a.Inhabilitado)
            {
                col.Item().PaddingTop(4).Background(Colors.Red.Lighten2).Padding(4)
                    .Text("⚠ TRABAJADOR INHABILITADO — Ha acumulado 10 o más puntos de infracción.")
                    .Bold().FontSize(8).FontColor(Colors.Red.Darken3).AlignCenter();
            }
        });
    }
}
