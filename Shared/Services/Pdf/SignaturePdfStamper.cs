using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
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
    /// </summary>
    public static class SignaturePdfStamper
    {
        private const double SignatureWidthPt = 140; // ancho objetivo de la firma
        private const double MarginPt = 24;          // margen respecto al borde inferior/derecho

        public static byte[] Stamp(byte[] source, byte[] signaturePng)
        {
            return IsPdf(source)
                ? StampPdf(source, signaturePng)
                // Una imagen se convierte en un PDF de una sola página y se estampa igual.
                : StampImageAsPdf(source, signaturePng);
        }

        private static bool IsPdf(byte[] b)
            => b.Length >= 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46; // "%PDF"

        private static byte[] StampPdf(byte[] pdfBytes, byte[] signaturePng)
        {
            using var input = new MemoryStream(pdfBytes);
            var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);

            for (var i = 0; i < doc.Pages.Count; i++)
            {
                var page = doc.Pages[i];
                using var gfx = XGraphics.FromPdfPage(page);
                DrawSignatureBottomRight(gfx, signaturePng, page.Width.Point, page.Height.Point);
            }

            using var output = new MemoryStream();
            doc.Save(output, false);
            return output.ToArray();
        }

        private static byte[] StampImageAsPdf(byte[] imageBytes, byte[] signaturePng)
        {
            // Normalizar a PNG (ImageSharp soporta png/jpg/webp) y obtener dimensiones en píxeles.
            byte[] pngBytes;
            int pxW, pxH;
            using (var img = Image.Load(imageBytes))
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

            using (var gfx = XGraphics.FromPdfPage(page))
            {
                using (var pageStream = new MemoryStream(pngBytes))
                using (var pageImg = XImage.FromStream(pageStream))
                    gfx.DrawImage(pageImg, 0, 0, pageW, pageH);

                DrawSignatureBottomRight(gfx, signaturePng, pageW, pageH);
            }

            using var output = new MemoryStream();
            doc.Save(output, false);
            return output.ToArray();
        }

        private static void DrawSignatureBottomRight(XGraphics gfx, byte[] signaturePng, double pageW, double pageH)
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

            double x = pageW - MarginPt - w;
            double y = pageH - MarginPt - h;
            gfx.DrawImage(sig, x, y, w, h);
        }
    }
}
