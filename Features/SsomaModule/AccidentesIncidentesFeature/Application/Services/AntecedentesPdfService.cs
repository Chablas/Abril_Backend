using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Services;

// Reporte interno de consulta — no lleva código de formato SSO-FO, a diferencia
// de FlashReportPdfService (que sí documenta el formato SSO-FO-035).
public static class AntecedentesPdfService
{
    private static readonly string Navy     = "#0D1F3C";
    private static readonly string NavyMid  = "#1E3A5F";
    private static readonly string BgRow    = "#F5F6F8";
    private static readonly string Border   = "#D8DBE2";
    private static readonly string TextMain = "#1A1A2E";
    private static readonly string TextMute = "#5A6275";

    private static readonly string LogoPath = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "wwwroot", "images", "abril-logo.png"),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "abril-logo.png"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wwwroot", "images", "abril-logo.png"),
    }.FirstOrDefault(File.Exists) ?? "";

    public static byte[] Generar(string titulo, string palabraClave, List<AntecedenteItemDto> items)
    {
        var logo = File.Exists(LogoPath) ? File.ReadAllBytes(LogoPath) : null;
        var generadoEl = DateTime.Now;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(1.2f, Unit.Centimetre);
                page.MarginVertical(0.8f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(7.5f).FontFamily("Arial").FontColor(TextMain));

                page.Header().Element(c => ComposeHeader(c, titulo, palabraClave, items.Count, logo));
                page.Content().PaddingTop(6).Element(c => ComposeContent(c, items));
                page.Footer().BorderTop(0.5f).BorderColor(Border).PaddingTop(3)
                    .Row(row =>
                    {
                        row.RelativeItem().Text($"Antecedentes de Eventos SSOMA | Generado {generadoEl:dd/MM/yyyy HH:mm}").FontSize(6.5f).FontColor(TextMute);
                        row.ConstantItem(80).AlignRight().Text(x =>
                        {
                            x.Span("Página ").FontSize(6.5f).FontColor(TextMute);
                            x.CurrentPageNumber().FontSize(6.5f).FontColor(TextMute);
                            x.Span(" de ").FontSize(6.5f).FontColor(TextMute);
                            x.TotalPages().FontSize(6.5f).FontColor(TextMute);
                        });
                    });
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer c, string titulo, string palabraClave, int total, byte[]? logo)
    {
        c.Column(col =>
        {
            col.Item().Height(1).Background(Border);

            col.Item().Border(0.5f).BorderColor(Border).Row(row =>
            {
                row.ConstantItem(90).AlignMiddle().AlignCenter().Padding(4).Element(logoEl =>
                {
                    if (logo != null)
                        logoEl.AlignMiddle().AlignCenter().Image(logo).FitArea();
                    else
                        logoEl.AlignMiddle().AlignCenter()
                            .Text("ABRIL GRUPO INMOBILIARIO")
                            .Bold().FontSize(8).FontColor(Navy);
                });

                row.ConstantItem(0.5f).Background(Colors.Grey.Lighten1);

                row.RelativeItem().AlignMiddle().AlignCenter().Column(tc =>
                {
                    tc.Item().AlignCenter().Text("ANTECEDENTES DE EVENTOS SSOMA").Bold().FontSize(14).FontColor(Navy);
                    tc.Item().AlignCenter().Text(titulo).Bold().FontSize(10).FontColor(NavyMid);
                });

                row.ConstantItem(0.5f).Background(Colors.Grey.Lighten1);

                row.ConstantItem(140).Column(metaCol =>
                {
                    void MetaRow(string label, string valor, bool last = false)
                    {
                        metaCol.Item()
                            .BorderBottom(last ? 0f : 0.5f).BorderColor(Border)
                            .Padding(2).Row(r =>
                            {
                                r.AutoItem().Text(label).Bold().FontSize(7).FontColor(TextMute);
                                r.ConstantItem(2);
                                r.RelativeItem().Text(valor).FontSize(7);
                            });
                    }
                    MetaRow("Palabra clave:", palabraClave);
                    MetaRow("Casos:", total.ToString());
                    MetaRow("Fecha:", DateTime.Now.ToString("dd/MM/yyyy"), last: true);
                });
            });

            col.Item().Height(1).Background(Border);
        });
    }

    private static void ComposeContent(IContainer c, List<AntecedenteItemDto> items)
    {
        c.Column(col =>
        {
            col.Spacing(8);

            if (items.Count == 0)
            {
                col.Item().Padding(6).Text("No se seleccionaron casos.").Italic().FontColor(TextMute);
                return;
            }

            foreach (var item in items)
            {
                col.Item().Border(0.5f).BorderColor(Border).Column(caseCol =>
                {
                    caseCol.Item().Background(Navy).Padding(4).Row(hr =>
                    {
                        hr.RelativeItem().Text($"{item.Codigo}  —  {item.TipoNombre}").Bold().FontColor(Colors.White).FontSize(8.5f);
                        hr.ConstantItem(120).AlignRight().Text($"{item.Fecha:dd/MM/yyyy}").FontColor(Colors.White).FontSize(8);
                    });

                    caseCol.Item().Padding(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(5); });
                        DataRow(t, "Proyecto", item.ProyectoNombre);
                        DataRow(t, "Lugar", item.LugarExacto);
                        DataRow(t, "Descripción", item.Descripcion);
                        if (!string.IsNullOrWhiteSpace(item.DanoProceso))
                            DataRow(t, "Daño / consecuencia", item.DanoProceso!);
                        if (!string.IsNullOrWhiteSpace(item.AccionesInmediatas))
                            DataRow(t, "Acciones inmediatas", item.AccionesInmediatas!);
                        if (!string.IsNullOrWhiteSpace(item.Mecanismo))
                            DataRow(t, "Mecanismo", item.Mecanismo!);
                        if (!string.IsNullOrWhiteSpace(item.AgenteCausante))
                            DataRow(t, "Agente causante", item.AgenteCausante!);
                    });
                });
            }
        });
    }

    private static void DataRow(TableDescriptor t, string label, string value)
    {
        t.Cell().Background(BgRow).Border(0.5f).BorderColor(Border).Padding(3)
            .Text(label).Bold().FontSize(7).FontColor(TextMute);
        t.Cell().Border(0.5f).BorderColor(Border).Padding(3)
            .Text(value).FontSize(7.5f);
    }
}
