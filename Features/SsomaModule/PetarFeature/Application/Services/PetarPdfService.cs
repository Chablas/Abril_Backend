using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Abril_Backend.Features.SsomaModule.PetarFeature.Application.Services;

/// <summary>Genera el PDF del PETAR — checklist de verificación + las 3 firmas (Ejecutante,
/// Supervisor, SSOMA) + QR de verificación, mismo patrón que AtsPdfService.</summary>
public static class PetarPdfService
{
    public static byte[] Generar(
        PetarResponseDto p, byte[]? selfieBytes, byte[]? firmaBytes, string verificacionUrl,
        byte[]? firmaSupervisorBytes = null, byte[]? firmaSsomaBytes = null)
    {
        byte[]? qrBytes = null;
        using (var generator = new QRCodeGenerator())
        using (var data = generator.CreateQrCode(verificacionUrl, QRCodeGenerator.ECCLevel.M))
        {
            var png = new PngByteQRCode(data);
            qrBytes = png.GetGraphic(8);
        }

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("PERMISO ESCRITO DE TRABAJO DE ALTO RIESGO — PETAR").Bold().FontSize(14);
                    var tipoLinea = string.IsNullOrEmpty(p.TipoCodigo) ? $"Tipo: {p.TipoNombre}" : $"Tipo: {p.TipoNombre} ({p.TipoCodigo})";
                    col.Item().Text(tipoLinea).FontSize(8).FontColor(Colors.Grey.Darken1);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    col.Item().PaddingTop(10).Column(c =>
                    {
                        c.Item().Text($"Ejecutante: {p.WorkerNombre}").Bold();
                        c.Item().Text($"Proyecto: {p.ProyectoNombre}");
                        c.Item().Text($"Trabajo a realizar: {p.DescripcionTrabajo}");
                        if (!string.IsNullOrWhiteSpace(p.Lugar))
                            c.Item().Text($"Lugar: {p.Lugar}");
                        c.Item().Text($"Fecha: {p.Fecha:dd/MM/yyyy}" + (p.HoraInicio != null ? $"   Horario: {p.HoraInicio} - {p.HoraFin}" : ""));
                        c.Item().Text($"ATS de origen: #{p.AtsId}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });

                    if (p.IzajeGrua is { } g)
                    {
                        col.Item().PaddingTop(6).Text("Datos técnicos del izaje").Bold();
                        col.Item().Column(c =>
                        {
                            var tipoGrua = g.TipoGrua == "TorreGrua" ? "Torre grúa" : g.TipoGrua == "GruaMovil" ? "Grúa móvil" : g.TipoGrua;
                            c.Item().Text($"Tipo de grúa: {tipoGrua ?? "—"}   Fabricante/marca: {g.FabricanteOMarca ?? "—"}").FontSize(8);
                            c.Item().Text($"Modelo/placa: {g.ModeloOPlaca ?? "—"}   Serie/tarjeta de circulación: {g.SerieOTarjetaCirculacion ?? "—"}").FontSize(8);
                            c.Item().Text($"Longitud de pluma/brazo: {g.LongitudPlumaBrazoM?.ToString() ?? "—"} m   Ángulo de pluma: {g.AnguloPluma?.ToString() ?? "—"}°").FontSize(8);
                            c.Item().Text($"Radio máximo de giro: {g.RadioMaximoGiroM?.ToString() ?? "—"} m   Elevación: {g.ElevacionM?.ToString() ?? "—"} m   Dirección/grado de giro: {g.DireccionGradoGiro ?? "—"}").FontSize(8);
                            c.Item().Text($"Capacidad certificada de fábrica: {g.CapacidadCertificadaTon?.ToString() ?? "—"} ton   Peso de la carga: {g.PesoCargaTotalTon?.ToString() ?? "—"} ton   % de capacidad usado: {g.PorcentajeCapacidad?.ToString() ?? "—"}%").FontSize(8);
                            if (!string.IsNullOrWhiteSpace(g.TamanoEstrobo))
                                c.Item().Text($"Tamaño del estrobo: {g.TamanoEstrobo}").FontSize(8);
                            if (!string.IsNullOrWhiteSpace(g.Observaciones))
                                c.Item().Text($"Observaciones: {g.Observaciones}").FontSize(8);
                        });
                    }

                    col.Item().PaddingTop(6).Text("Checklist de verificación previa").Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(5); c.ConstantColumn(50); });
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Ítem").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Resp.").Bold();
                        });
                        foreach (var r in p.Respuestas)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(r.Texto).FontSize(8);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignCenter()
                                .Text(r.Respuesta).FontSize(8).Bold()
                                .FontColor(r.Respuesta == "NO" ? Colors.Red.Darken2 : Colors.Green.Darken2);
                        }
                    });

                    if (p.Estado == "Cerrado")
                    {
                        col.Item().PaddingTop(6).Text("Cierre del trabajo").Bold();
                        col.Item().Text($"Cerrado: {p.CierreHoraServidor:dd/MM/yyyy HH:mm}").FontSize(8);
                        if (!string.IsNullOrWhiteSpace(p.CierreObservaciones))
                            col.Item().Text($"Observaciones: {p.CierreObservaciones}").FontSize(8);
                    }

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Estado: {p.Estado}").FontSize(8).Bold();
                            if (!string.IsNullOrWhiteSpace(p.PdfHash))
                                c.Item().Text($"Hash del documento: {p.PdfHash[..Math.Min(16, p.PdfHash.Length)]}…").FontSize(7).FontColor(Colors.Grey.Darken1);
                        });
                        if (selfieBytes is { Length: > 0 })
                            row.ConstantItem(80).Height(80).Image(selfieBytes).FitArea();
                        if (qrBytes is { Length: > 0 })
                            row.ConstantItem(70).Height(70).Image(qrBytes).FitArea();
                    });

                    col.Item().PaddingTop(14).Text("Firmas").Bold();
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Element(c => FirmaBloque(c, "Ejecutante", p.WorkerNombre, null, p.HoraServidorFirma, firmaBytes));
                        row.RelativeItem().Element(c => FirmaBloque(c, "Supervisor / Responsable", p.SupervisorNombre, p.SupervisorCargo, p.SupervisorHoraServidor, firmaSupervisorBytes));
                        row.RelativeItem().Element(c => FirmaBloque(c, "Visto Bueno SSOMA", p.SsomaNombre, p.SsomaCargo, p.SsomaHoraServidor, firmaSsomaBytes));
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Verificar autenticidad: ").FontSize(7);
                    t.Span(verificacionUrl).FontSize(7).FontColor(Colors.Blue.Darken2);
                });
            });
        }).GeneratePdf();
    }

    private static void FirmaBloque(IContainer container, string rol, string? nombre, string? cargo, DateTime? hora, byte[]? firma)
    {
        container.Column(c =>
        {
            c.Item().Height(40).AlignCenter().Element(inner =>
            {
                if (firma is { Length: > 0 })
                    inner.Image(firma).FitArea();
                else
                    inner.AlignMiddle().AlignCenter().Text("PENDIENTE").FontSize(9).Bold().FontColor(Colors.Red.Darken2);
            });
            c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Darken1);
            c.Item().PaddingTop(2).Text(rol).FontSize(7).Bold();
            c.Item().Text(nombre ?? "—").FontSize(7);
            if (!string.IsNullOrWhiteSpace(cargo))
                c.Item().Text(cargo).FontSize(6.5f).FontColor(Colors.Grey.Darken1).Italic();
            if (hora.HasValue)
                c.Item().Text(FechaHoraPeru(hora)).FontSize(6.5f).FontColor(Colors.Grey.Darken1);
        });
    }

    private static string FechaHoraPeru(DateTime? horaServidorUtc)
    {
        if (horaServidorUtc is null) return "—";
        var utc = DateTime.SpecifyKind(horaServidorUtc.Value, DateTimeKind.Utc);
        var peru = new DateTimeOffset(utc).ToOffset(TimeSpan.FromHours(-5));
        return peru.ToString("dd/MM/yyyy HH:mm");
    }
}
