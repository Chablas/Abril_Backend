using Abril_Backend.Features.Ssoma.Penalidad.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Abril_Backend.Features.Ssoma.Penalidad.Services;

/// <summary>Genera los dos documentos del proceso, calcado del patrón de RacPdfService.</summary>
public static class PenalidadPdfService
{
    public static Task<byte[]> GenerarNotificacionAsync(PenalidadDetalleDto p, string textoNotificacion)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(10));

                page.Header().PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Medium).Column(col =>
                {
                    col.Item().Text("NOTIFICACIÓN DE PENALIDAD Y DERECHO A DESCARGO").Bold().FontSize(13);
                    col.Item().Text($"Código: {p.Codigo}").FontSize(10).FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(2); });
                        void Fila(string l1, string v1, string l2, string v2)
                        {
                            table.Cell().Padding(4).Background(Colors.Grey.Lighten3).Text(l1).Bold().FontSize(9);
                            table.Cell().Padding(4).Text(v1).FontSize(9);
                            table.Cell().Padding(4).Background(Colors.Grey.Lighten3).Text(l2).Bold().FontSize(9);
                            table.Cell().Padding(4).Text(v2).FontSize(9);
                        }
                        Fila("Empresa", p.EmpresaNombre ?? "-", "Proyecto", p.ProyectoNombre ?? "-");
                        Fila("Infracción", p.InfraccionNombre ?? "-", "Severidad", p.Severidad);
                        Fila("Monto estimado", $"S/ {p.MontoCalculado:N2}", "Plazo de descargo",
                             p.PlazoDescargoVenceEn?.ToString("dd/MM/yyyy HH:mm") ?? "-");
                    });

                    col.Item().PaddingTop(14).Text(textoNotificacion).FontSize(10).LineHeight(1.4f);

                    if (!string.IsNullOrWhiteSpace(p.DescripcionOcurrido))
                    {
                        col.Item().PaddingTop(10).Background(Colors.Grey.Lighten3).Padding(4).Text("Descripción del ocurrido").Bold().FontSize(9);
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(p.DescripcionOcurrido).FontSize(9);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Abril Ingeniería — Gestión SSOMA").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return Task.FromResult(doc.GeneratePdf());
    }

    public static Task<byte[]> GenerarResolucionAsync(PenalidadDetalleDto p)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(10));

                page.Header().PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Medium).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("RESOLUCIÓN DE PENALIDAD").Bold().FontSize(13);
                        col.Item().Text($"Código: {p.Codigo}").FontSize(10).FontColor(Colors.Grey.Darken2);
                    });
                    row.ConstantItem(100).AlignRight().AlignMiddle()
                        .Text(p.ResolucionTipo ?? p.Estado).Bold().FontSize(11)
                        .FontColor(p.ResolucionTipo == "Aplicada" ? Colors.Red.Darken2 : Colors.Green.Darken2);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(2); });
                        void Fila(string l1, string v1, string l2, string v2)
                        {
                            table.Cell().Padding(4).Background(Colors.Grey.Lighten3).Text(l1).Bold().FontSize(9);
                            table.Cell().Padding(4).Text(v1).FontSize(9);
                            table.Cell().Padding(4).Background(Colors.Grey.Lighten3).Text(l2).Bold().FontSize(9);
                            table.Cell().Padding(4).Text(v2).FontSize(9);
                        }
                        Fila("Empresa", p.EmpresaNombre ?? "-", "Proyecto", p.ProyectoNombre ?? "-");
                        Fila("Infracción", p.InfraccionNombre ?? "-", "Severidad", p.Severidad);
                        Fila("Monto final", $"S/ {(p.MontoFinal ?? p.MontoCalculado):N2}", "Fecha resolución",
                             p.ResueltaEn?.ToString("dd/MM/yyyy") ?? "-");
                    });

                    col.Item().PaddingTop(10).Background(Colors.Grey.Lighten3).Padding(4).Text("Fundamento").Bold().FontSize(9);
                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(p.ResolucionTexto ?? "-").FontSize(9);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Abril Ingeniería — Gestión SSOMA").FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return Task.FromResult(doc.GeneratePdf());
    }
}
