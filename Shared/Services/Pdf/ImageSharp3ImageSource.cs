using MigraDocCore.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace Abril_Backend.Shared.Services.Pdf
{
    /// <summary>
    /// Reemplazo del proveedor de imágenes por defecto de PdfSharpCore.
    ///
    /// El default (<c>PdfSharpCore.Utils.ImageSharpImageSource</c>) fue compilado contra
    /// ImageSharp v1/v2 e invoca <c>Image.Load(Stream, out IImageFormat)</c>, overload que
    /// ImageSharp 3.x eliminó. Con ImageSharp 3.x, cualquier <c>XImage.FromStream</c> lanza
    /// <see cref="MissingMethodException"/> (rompe el estampado de la firma en facturas y
    /// cualquier otro dibujo de imágenes en PDFs). Esta implementación usa las APIs de
    /// ImageSharp 3.x y conserva la transparencia (PNG con canal alfa → PdfSharpCore genera
    /// la máscara /Mask), por lo que la firma se superpone sin recuadro opaco.
    ///
    /// Se registra una sola vez al arranque en <c>Program.cs</c>:
    /// <c>ImageSource.ImageSourceImpl = new ImageSharp3ImageSource();</c>
    /// </summary>
    public sealed class ImageSharp3ImageSource : ImageSource
    {
        protected override IImageSource FromBinaryImpl(string name, Func<byte[]> fetch, int? quality = 75)
            => Load(name, () => new MemoryStream(fetch()), quality ?? 75);

        protected override IImageSource FromFileImpl(string path, int? quality = 75)
            => Load(path, () => File.OpenRead(path), quality ?? 75);

        protected override IImageSource FromStreamImpl(string name, Func<Stream> fetch, int? quality = 75)
            => Load(name, fetch, quality ?? 75);

        private static IImageSource Load(string name, Func<Stream> fetch, int quality)
        {
            using var stream = fetch();
            var image = Image.Load<Rgba32>(stream);
            return new ImageSharpSource(name, image, quality, HasAlpha(image));
        }

        /// <summary>Detecta si la imagen tiene píxeles con canal alfa (&lt; 255) para pedir máscara.</summary>
        private static bool HasAlpha(Image<Rgba32> image)
        {
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image[x, y].A < 255) return true;
            return false;
        }

        private sealed class ImageSharpSource : IImageSource
        {
            private readonly Image<Rgba32> _image;
            private readonly int _quality;

            public int Width => _image.Width;
            public int Height => _image.Height;
            public string Name { get; }
            public bool Transparent { get; }

            public ImageSharpSource(string name, Image<Rgba32> image, int quality, bool transparent)
            {
                Name = name;
                _image = image;
                _quality = quality;
                Transparent = transparent;
            }

            /// <summary>Plano de color (PdfSharpCore lo usa para imágenes opacas).</summary>
            public void SaveAsJpeg(MemoryStream ms)
                => _image.Save(ms, new JpegEncoder { Quality = _quality });

            /// <summary>BMP 32bpp con transparencia; PdfSharpCore extrae color + alfa para la máscara.</summary>
            public void SaveAsPdfBitmap(MemoryStream ms)
                => _image.Save(ms, new BmpEncoder { BitsPerPixel = BmpBitsPerPixel.Pixel32, SupportTransparency = true });

            public void Dispose() => _image.Dispose();
        }
    }
}
