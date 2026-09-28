using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Application.Services;

/// <summary>
/// Plantilla descargable de la autorización de uso de firma digital e imagen para el ATS Digital
/// — el trabajador la imprime, la firma en físico, y el Coordinador SSOMA sube el escaneado (ver
/// SsAtsAutorizacionPermiso). Mismo patrón que AutorizacionFirmaPdfService (SSO-FO-149, médico
/// ocupacional) y TareoAutorizacionPdfService (SSO-FO-150, Tareo AC) — header/paleta replicados a
/// propósito para que los formatos oficiales se vean consistentes entre módulos.
/// </summary>
public static class AtsAutorizacionPdfService
{
    private const string ColorSecundario = "#2D5AA0";
    private const string ColorGrupo = "#E8EEF7";
    private static readonly string Border = "#D8DBE2";
    private static readonly string TextMain = "#1A1A2E";
    private static readonly string TextMuted = "#5A6275";

    private const string Codigo = "SSO-FO-151";
    private const string Titulo = "AUTORIZACIÓN DE USO DE FIRMA DIGITAL E IMAGEN — ATS DIGITAL";

    public static byte[] GenerarPdf(string nombre, string? dni, byte[]? logoBytes, byte[]? firmaDigitalBytes)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(18);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9).FontColor(TextMain));

                page.Header().Element(c => ComposeHeader(c, logoBytes));
                page.Content().PaddingTop(12).Element(c => ComposeBody(c, nombre, dni, firmaDigitalBytes));

                page.Footer().AlignCenter().PaddingTop(6).Text(t =>
                {
                    t.Span("Documento generado por el sistema Abril — ").FontSize(7.5f).FontColor(Colors.Grey.Medium);
                    t.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7.5f).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, byte[]? logoBytes)
    {
        container.Border(0.5f).BorderColor(Border).Row(row =>
        {
            row.ConstantItem(90).AlignMiddle().AlignCenter().Padding(4).Element(logoEl =>
            {
                if (logoBytes != null)
                    logoEl.AlignMiddle().AlignCenter().Image(logoBytes).FitArea();
                else
                    logoEl.AlignMiddle().AlignCenter().Text("ABRIL").Bold().FontSize(8).AlignCenter();
            });

            row.ConstantItem(0.5f).Background(Colors.Grey.Lighten1);
            row.RelativeItem().AlignMiddle().AlignCenter().Text(Titulo).Bold().FontSize(11).AlignCenter();
            row.ConstantItem(0.5f).Background(Colors.Grey.Lighten1);

            row.ConstantItem(120).Column(metaCol =>
            {
                void MetaRow(string label, string valor, bool last = false)
                {
                    metaCol.Item().BorderBottom(last ? 0f : 0.5f).Padding(2).Row(r =>
                    {
                        r.AutoItem().Text(label).Bold().FontSize(7);
                        r.ConstantItem(2);
                        r.RelativeItem().Text(valor).FontSize(7);
                    });
                }
                MetaRow("Código:", Codigo);
                MetaRow("Versión:", "01");
                MetaRow("Fecha:", DateTime.Now.ToString("dd/MM/yyyy"));

                metaCol.Item().BorderTop(0.5f).Row(subRow =>
                {
                    foreach (var (lbl, val, last) in new[] { ("Elab.:", "SSOMA", false), ("Rev.:", "JSSOMA", false), ("Apro.:", "GP", true) })
                    {
                        var cell = subRow.RelativeItem().Padding(2);
                        if (!last) cell = cell.BorderRight(0.5f);
                        cell.Text(t => { t.Span(lbl + " ").Bold().FontSize(5.5f); t.Span(val).FontSize(5.5f); });
                    }
                });
            });
        });
    }

    private static void ComposeBody(IContainer container, string nombre, string? dni, byte[]? firmaDigitalBytes)
    {
        container.Column(col =>
        {
            col.Spacing(14);

            col.Item().Column(inner =>
            {
                SectionHeader(inner, "DATOS DEL TRABAJADOR");
                inner.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2.6f); });
                    void L(string v) => t.Cell().Background(ColorGrupo).Padding(5).Text(v).Bold().FontSize(8.5f);
                    void V(string v) => t.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(v).FontSize(9.5f);

                    t.Cell().ColumnSpan(2).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(nombre).Bold().FontSize(12);
                    L("DNI:"); V(dni ?? "—");
                });
            });

            col.Item().Column(inner =>
            {
                SectionHeader(inner, "DECLARACIÓN Y AUTORIZACIÓN");
                inner.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(dc =>
                {
                    dc.Spacing(8);
                    void P(string texto) => dc.Item().Text(texto).FontSize(9).LineHeight(1.45f).FontColor(TextMain);

                    P($"Yo, {(string.IsNullOrWhiteSpace(nombre) ? "____________________" : nombre)}, " +
                      $"identificado con DNI N° {(string.IsNullOrWhiteSpace(dni) ? "____________" : dni)}, " +
                      "trabajador de Abril Grupo Inmobiliario, DECLARO Y AUTORIZO lo siguiente:");

                    P("1. Que autorizo a Abril Grupo Inmobiliario a capturar y almacenar, cada vez que firme un " +
                      "Análisis de Trabajo Seguro (ATS) desde el sistema digital: una fotografía de mi rostro " +
                      "(\"selfie\") tomada en el momento de la firma, mi geolocalización (latitud/longitud) al " +
                      "momento de firmar, y mi firma manuscrita capturada en pantalla — todo ello con el único " +
                      "fin de dar trazabilidad y sustento legal a cada ATS que firme.");

                    P("2. Que entiendo que este tratamiento de mis datos personales e imagen se rige por la Ley " +
                      "N° 29733 (Ley de Protección de Datos Personales) y su reglamento, y que estos datos NO " +
                      "serán usados para ningún otro fin distinto al de trazabilidad del ATS digital.");

                    P("3. Que la firma que registro en pantalla al aceptar cada ATS constituye mi firma " +
                      "electrónica para dicho documento, conforme al artículo 141° del Código Civil y la Ley " +
                      "N° 27269 (Ley de Firmas y Certificados Digitales) y su reglamento, y que tiene el mismo " +
                      "valor probatorio que mi firma manuscrita para efectos internos y ante las autoridades " +
                      "competentes (SUNAFIL y otras que correspondan).");

                    P("4. Que esta autorización es voluntaria y puedo revocarla en cualquier momento mediante " +
                      "comunicación escrita a mi Coordinador SSOMA, lo que impediría que siga firmando ATS " +
                      "digitales hasta que se formalice una nueva autorización.");

                    P("5. Que la presente autorización se formaliza con mi firma manuscrita al pie del presente " +
                      "documento, el cual — una vez firmado en físico — será escaneado y cargado al sistema por " +
                      "mi Coordinador SSOMA como evidencia de mi conformidad. No podré crear ni firmar ningún " +
                      "ATS digital hasta que esta evidencia esté registrada.");
                });
            });

            col.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Height(60).AlignMiddle().AlignCenter().Element(box =>
                    {
                        if (firmaDigitalBytes != null)
                            box.Image(firmaDigitalBytes).FitArea();
                        else
                            box.Text("(sin firma digital)").FontSize(8).FontColor(TextMuted).AlignCenter();
                    });
                    c.Item().BorderTop(1).BorderColor(Border);
                    c.Item().PaddingTop(4).Text("Firma digital (registrada en el sistema)").FontSize(8).AlignCenter();
                });

                row.ConstantItem(20);

                row.RelativeItem().Column(c =>
                {
                    c.Item().Height(60);
                    c.Item().BorderTop(1).BorderColor(Border);
                    c.Item().PaddingTop(4).Text("Firma física del trabajador (rúbrica)").FontSize(8).AlignCenter();
                });
            });

            col.Item().PaddingTop(6).AlignCenter().Column(c =>
            {
                c.Item().Text(nombre).Bold().FontSize(9.5f).AlignCenter();
                if (!string.IsNullOrWhiteSpace(dni))
                    c.Item().Text($"DNI {dni}").FontSize(8).FontColor(TextMuted).AlignCenter();
            });

            col.Item().PaddingTop(10).Text(
                "La firma digital de arriba fue capturada en el sistema Abril antes de imprimir este documento. " +
                "La rúbrica física al lado es el respaldo en papel de esa misma firma — el escaneado de este " +
                "documento firmado en físico queda como evidencia de trazabilidad de ambas.")
                .FontSize(7.5f).FontColor(TextMuted).Italic();
        });
    }

    private static void SectionHeader(ColumnDescriptor col, string title)
    {
        col.Item().Background(ColorSecundario).Padding(5).Text(title).Bold().FontSize(9.5f).FontColor(Colors.White);
    }
}
