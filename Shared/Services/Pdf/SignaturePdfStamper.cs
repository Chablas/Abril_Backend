using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace Abril_Backend.Shared.Services.Pdf
{
    /// <summary>
    /// Estampa una firma (PNG) en la esquina inferior derecha de TODAS las páginas del documento. El
    /// resultado es SIEMPRE un PDF: si el documento original es una imagen (PNG/JPG/WEBP) se
    /// convierte a un PDF de una página y se estampa.
    ///
    /// Vive en Shared porque lo usan tres módulos y en los tres la firma vale como visado de cada
    /// hoja del documento, no como una única línea de firma al pie: Contabilidad (la firma del
    /// Gerente General sobre una factura), Gestión Administrativa (la firma de jefatura sobre una
    /// planilla de rendición) y Gestión GTH (la firma del postulante sobre su carta oferta).
    ///
    /// Gestión Administrativa firma además CON PIE (<see cref="PieFirma"/>): debajo de la firma van
    /// una línea, el cargo, el nombre y la fecha y hora en que se firmó.
    /// </summary>
    public static class SignaturePdfStamper
    {
        /// <summary>
        /// Lo que se imprime debajo de la firma: quién firmó, con qué cargo y cuándo.
        /// </summary>
        /// <param name="Nombre">Nombre del firmante, ya formateado para imprimirse.</param>
        /// <param name="Cargo">
        /// Su puesto. Null si no tiene: el pie cae en <see cref="LeyendaSinCargo"/>.
        /// </param>
        /// <param name="FirmadoAt">Momento de la firma; se imprime en hora de Perú.</param>
        public sealed record PieFirma(string Nombre, string? Cargo, DateTimeOffset FirmadoAt);

        /// <summary>
        /// Leyenda bajo la línea de firma mientras el documento no está firmado, y la que queda si
        /// el firmante no tiene puesto. La planilla de rendición la imprime al generarse.
        /// </summary>
        public const string LeyendaSinCargo = "Firma de Jefatura / Gerencia";

        /// <summary>
        /// Alto del pie de la firma, debajo de la línea: cargo, nombre y fecha. Público porque la
        /// planilla de rendición reserva exactamente ese espacio bajo su línea de firma.
        /// </summary>
        public const double PieAltoPt = 28;

        /// <summary>Grosor de la línea de firma, en puntos (el mismo de la planilla).</summary>
        public const float LineaFirmaGrosorPt = 0.7f;

        /// <summary>
        /// Distancia entre el borde inferior de la hoja y el pie de la firma. Público por lo mismo
        /// que <see cref="PieAltoPt"/>: es el margen inferior de la planilla de rendición.
        /// </summary>
        public const double PieMargenInferiorPt = 12;

        /// <summary>
        /// Alto máximo de la firma sobre la línea. Una imagen muy alta (una foto) se achica para que
        /// no suba sobre el contenido de la hoja.
        /// </summary>
        public const double FirmaAltoMaxPt = 60;

        /// <summary>
        /// Lo que el pie se pasa del ancho de la firma a cada lado. El fondo blanco del pie tapa así
        /// por completo la línea y la leyenda que la planilla trae impresas.
        /// </summary>
        private const double PieRellenoPt = 1;

        private static readonly string[] MesesCortos =
            { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };

        /// <summary>Perú no tiene horario de verano: la hora impresa es la de -05:00.</summary>
        private static readonly TimeSpan PeruOffset = TimeSpan.FromHours(-5);
        /// <summary>Ancho objetivo de la firma, en puntos.</summary>
        /// <remarks>
        /// Público porque quien GENERA un documento firmable necesita saber dónde va a caer la
        /// firma para poder dibujar su línea justo ahí (lo hace la planilla de rendición).
        /// Repetir el número allá lo desalinearía en silencio el día que se toque acá.
        /// </remarks>
        public const double SignatureWidthPt = 140;

        /// <summary>Margen de la firma respecto al borde inferior/derecho de la hoja, en puntos.</summary>
        public const double SignatureMarginPt = 24;

        /// <summary>Separación entre dos firmas estampadas en la misma hoja, en puntos.</summary>
        public const double SignatureGapPt = 12;

        /// <param name="slot">
        /// Lugar de esta firma cuando el documento ya trae otras estampadas con este mismo método:
        /// 0 es la esquina inferior derecha de siempre y cada lugar siguiente se corre a la
        /// izquierda (y sube una fila cuando ya no entra). Lo usa el Consolidado del S10 compartido
        /// por planillas que aprueban jefes distintos: la firma del segundo no tapa la del primero.
        /// </param>
        public static byte[] Stamp(byte[] source, byte[] signaturePng, int slot = 0)
        {
            return IsPdf(source)
                ? StampPdf(source, signaturePng, slot, pie: null)
                // Una imagen se convierte en un PDF de una sola página y se estampa igual.
                : StampImageAsPdf(source, signaturePng, slot, pie: null);
        }

        /// <summary>
        /// Igual que <see cref="Stamp(byte[], byte[], int)"/>, pero la firma va sobre una línea y con
        /// su pie debajo: cargo, nombre y "Firmado digitalmente: 18 sep 2026, 16:15". El pie lleva
        /// fondo blanco, así que tapa la leyenda "Firma de Jefatura / Gerencia" que la planilla de
        /// rendición trae impresa en ese lugar: la firmada ya dice quién firmó y con qué cargo.
        /// </summary>
        public static byte[] Stamp(byte[] source, byte[] signaturePng, PieFirma pie, int slot = 0)
        {
            return IsPdf(source)
                ? StampPdf(source, signaturePng, slot, pie)
                : StampImageAsPdf(source, signaturePng, slot, pie);
        }

        private static bool IsPdf(byte[] b)
            => b.Length >= 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46; // "%PDF"

        private static byte[] StampPdf(byte[] pdfBytes, byte[] signaturePng, int slot, PieFirma? pie)
        {
            using var input = new MemoryStream(pdfBytes);
            var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);

            // El pie es el mismo en todas las hojas: se arma una vez y se reutiliza.
            using var pieStream = pie == null ? null : new MemoryStream(PiePdf(pie));
            using var pieForm   = pieStream == null ? null : XPdfForm.FromStream(pieStream);

            for (var i = 0; i < doc.Pages.Count; i++)
            {
                var page = doc.Pages[i];
                using var gfx = XGraphics.FromPdfPage(page);
                if (pieForm == null)
                    DrawSignatureBottomRight(gfx, signaturePng, page.Width.Point, page.Height.Point, slot);
                else
                    DrawSignatureWithPie(gfx, signaturePng, pieForm, page.Width.Point, page.Height.Point, slot);
            }

            using var output = new MemoryStream();
            doc.Save(output, false);
            return output.ToArray();
        }

        private static byte[] StampImageAsPdf(byte[] imageBytes, byte[] signaturePng, int slot, PieFirma? pie)
        {
            // Normalizar a PNG (ImageSharp soporta png/jpg/webp) y obtener dimensiones en píxeles.
            byte[] pngBytes;
            int pxW, pxH;
            // Calificado: QuestPDF también tiene un tipo Image.
            using (var img = SixLabors.ImageSharp.Image.Load(imageBytes))
            {
                pxW = img.Width;
                pxH = img.Height;
                using var ms = new MemoryStream();
                img.Save(ms, new PngEncoder());
                pngBytes = ms.ToArray();
            }

            // Página del tamaño de la imagen (96 DPI → puntos).
            double pageW = pxW * 72.0 / 96.0;
            double pageH = pxH * 72.0 / 96.0;

            var doc = new PdfDocument();
            var page = doc.AddPage();
            page.Width = XUnit.FromPoint(pageW);
            page.Height = XUnit.FromPoint(pageH);

            using var pieStream = pie == null ? null : new MemoryStream(PiePdf(pie));
            using var pieForm   = pieStream == null ? null : XPdfForm.FromStream(pieStream);

            using (var gfx = XGraphics.FromPdfPage(page))
            {
                using (var pageStream = new MemoryStream(pngBytes))
                using (var pageImg = XImage.FromStream(pageStream))
                    gfx.DrawImage(pageImg, 0, 0, pageW, pageH);

                if (pieForm == null)
                    DrawSignatureBottomRight(gfx, signaturePng, pageW, pageH, slot);
                else
                    DrawSignatureWithPie(gfx, signaturePng, pieForm, pageW, pageH, slot);
            }

            using var output = new MemoryStream();
            doc.Save(output, false);
            return output.ToArray();
        }

        /// <summary>
        /// La firma con su pie: la imagen apoyada sobre la línea y, debajo, cargo, nombre y fecha.
        /// La línea cae exactamente donde la planilla de rendición dibuja la suya
        /// (<see cref="PieMargenInferiorPt"/> + <see cref="PieAltoPt"/> desde abajo), así que en la
        /// planilla se superponen; en el Consolidado del S10, que no la trae, la pone el pie.
        /// </summary>
        private static void DrawSignatureWithPie(
            XGraphics gfx, byte[] signaturePng, XPdfForm pieForm, double pageW, double pageH, int slot)
        {
            using var sigStream = new MemoryStream(signaturePng);
            using var sig = XImage.FromStream(sigStream);

            // En hojas chicas todo el bloque se achica en la misma proporción que la firma sola.
            var escala = Math.Min(1.0, pageW * 0.4 / SignatureWidthPt);
            var ancho  = SignatureWidthPt * escala;

            var pieAncho = pieForm.PointWidth * escala;
            var pieAlto  = pieForm.PointHeight * escala;

            // La imagen ocupa el ancho de la línea salvo que quede más alta que el máximo: entonces
            // se achica en proporción y se centra sobre la línea.
            var altoMax = FirmaAltoMaxPt * escala;
            var imgAncho = ancho;
            var imgAlto  = ancho * sig.PixelHeight / sig.PixelWidth;
            if (imgAlto > altoMax)
            {
                imgAlto  = altoMax;
                imgAncho = altoMax * sig.PixelWidth / sig.PixelHeight;
            }

            var x      = pageW - SignatureMarginPt - ancho;
            var pieTop = pageH - PieMargenInferiorPt - pieAlto;

            if (slot > 0)
            {
                // Mismo reparto que sin pie: a la izquierda y, si ya no entra, una fila más arriba.
                // La fila se mide con la firma más alta posible para que dos filas no se pisen.
                var porFila = Math.Max(1,
                    (int)((pageW - 2 * SignatureMarginPt + SignatureGapPt) / (ancho + SignatureGapPt)));
                x      -= (slot % porFila) * (ancho + SignatureGapPt);
                pieTop -= (slot / porFila) * (altoMax + pieAlto + SignatureGapPt);
            }

            gfx.DrawImage(pieForm, x - PieRellenoPt * escala, pieTop, pieAncho, pieAlto);
            gfx.DrawImage(sig, x + (ancho - imgAncho) / 2, pieTop - imgAlto, imgAncho, imgAlto);
        }

        /// <summary>
        /// El pie de la firma como un PDF del tamaño exacto del bloque: la línea arriba y, debajo,
        /// cargo, nombre y fecha, sobre fondo blanco. Se arma con QuestPDF —el mismo motor que genera
        /// la planilla— y se estampa como formulario: así el texto no depende de las fuentes que
        /// tenga instaladas el servidor.
        ///
        /// Cada línea se achica sola si no entra en el ancho (un puesto largo), en vez de partirse
        /// y salirse del pie.
        /// </summary>
        private static byte[] PiePdf(PieFirma pie)
        {
            var ancho = (float)(SignatureWidthPt + 2 * PieRellenoPt);
            var alto  = LineaFirmaGrosorPt + (float)PieAltoPt;

            var cargo = string.IsNullOrWhiteSpace(pie.Cargo) ? LeyendaSinCargo : pie.Cargo.Trim();
            var fecha = "Firmado digitalmente: " + FechaFirma(pie.FirmadoAt);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(ancho, alto, Unit.Point);
                    page.Margin(0);
                    page.PageColor(Colors.Transparent);
                    page.DefaultTextStyle(t => t.FontFamily("Arial"));

                    page.Content().Background(Colors.White).PaddingHorizontal((float)PieRellenoPt).Column(col =>
                    {
                        col.Item().LineHorizontal(LineaFirmaGrosorPt);
                        col.Item().PaddingTop(2).Column(pieCol =>
                        {
                            pieCol.Item().Height(9).ScaleToFit().AlignCenter().AlignMiddle()
                                .Text(cargo).FontSize(7.5f).Italic();
                            pieCol.Item().Height(9).ScaleToFit().AlignCenter().AlignMiddle()
                                .Text(pie.Nombre).FontSize(7.5f).Bold();
                            pieCol.Item().Height(7.5f).ScaleToFit().AlignCenter().AlignMiddle()
                                .Text(fecha).FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                        });
                    });
                });
            }).GeneratePdf();
        }

        /// <summary>"18 sep 2026, 16:15", en hora de Perú.</summary>
        private static string FechaFirma(DateTimeOffset momento)
        {
            var peru = momento.ToOffset(PeruOffset);
            return string.Create(CultureInfo.InvariantCulture,
                $"{peru.Day:00} {MesesCortos[peru.Month - 1]} {peru.Year}, {peru:HH:mm}");
        }

        private static void DrawSignatureBottomRight(
            XGraphics gfx, byte[] signaturePng, double pageW, double pageH, int slot)
        {
            // PDFsharp 6 recibe el Stream directamente (en PdfSharpCore era un Func<Stream>).
            // El XImage ya tiene la imagen decodificada en memoria, así que el stream puede
            // cerrarse en el mismo scope sin afectar al dibujado ni al Save posterior.
            using var sigStream = new MemoryStream(signaturePng);
            using var sig = XImage.FromStream(sigStream);

            double w = SignatureWidthPt;
            double h = SignatureWidthPt * sig.PixelHeight / sig.PixelWidth;

            // No dejar que la firma ocupe más del 40% del ancho de páginas pequeñas.
            if (w > pageW * 0.4)
            {
                w = pageW * 0.4;
                h = w * sig.PixelHeight / sig.PixelWidth;
            }

            double x = pageW - SignatureMarginPt - w;
            double y = pageH - SignatureMarginPt - h;

            if (slot > 0)
            {
                // Cuántas firmas entran en una fila sin pasar el margen izquierdo; las que sobran
                // suben a la fila de arriba.
                var porFila = Math.Max(1,
                    (int)((pageW - 2 * SignatureMarginPt + SignatureGapPt) / (w + SignatureGapPt)));
                x -= (slot % porFila) * (w + SignatureGapPt);
                y -= (slot / porFila) * (h + SignatureGapPt);
            }

            gfx.DrawImage(sig, x, y, w, h);
        }
    }
}
