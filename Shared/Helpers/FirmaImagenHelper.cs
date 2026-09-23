using Abril_Backend.Application.Exceptions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace Abril_Backend.Shared.Helpers
{
    /// <summary>
    /// Validación de una firma que llega del frontend, ya sea el <c>toDataURL('image/png')</c> de un
    /// canvas o el archivo de imagen que el usuario subió.
    ///
    /// Vive en Shared porque la firma de una persona se registra desde tres módulos y la regla es la
    /// misma en los tres: Contabilidad (la firma del Gerente General, que se estampa en las
    /// facturas), Gestión Administrativa (la firma de jefatura sobre la planilla de rendición y el
    /// Consolidado del S10) y Gestión GTH (la firma del postulante sobre su carta oferta). Las tres
    /// terminan en <c>person_firma</c> y las estampa el mismo helper de PDF, así que si acá se
    /// aceptara algo que allá no, la misma ficha podría quedar con una firma que un módulo no sabe
    /// estampar.
    /// </summary>
    public static class FirmaImagenHelper
    {
        /// <summary>Tope de tamaño de la firma ya decodificada y normalizada.</summary>
        public const int MaxBytes = 2 * 1024 * 1024; // 2 MB

        /// <summary>
        /// Tope de lo que se acepta recibir, antes de normalizar. Es más alto que
        /// <see cref="MaxBytes"/> porque una foto de una firma sale de cualquier celular pesando
        /// varios MB y el reescalado la deja muy por debajo del tope final.
        /// </summary>
        public const int MaxBytesSubida = 12 * 1024 * 1024; // 12 MB

        /// <summary>
        /// Ancho máximo al que se reescala una imagen subida. 1200 px sobra para una firma que en el
        /// PDF se dibuja a 140 puntos de ancho, y es lo que mantiene el PNG por debajo del tope.
        /// </summary>
        private const int AnchoMaximo = 1200;

        /// <summary>Cabecera de un PNG válido.</summary>
        private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>MIME con el que se guarda la firma en la ficha.</summary>
        public const string Mime = "image/png";

        /// <summary>
        /// Decodifica y valida una firma DIBUJADA en el canvas, que siempre llega como PNG. Acepta
        /// tanto el data URL completo (<c>data:image/png;base64,XXXX</c>) como solo el base64.
        /// </summary>
        public static byte[] DecodePng(string? imageBase64)
        {
            var bytes = DecodeBase64(imageBase64, "Debe dibujar una firma antes de guardar.");

            if (bytes.Length > MaxBytes)
                throw new AbrilException("La firma es demasiado grande (máximo 2 MB).");
            if (!EsPng(bytes))
                throw new AbrilException("La firma debe ser una imagen PNG.");

            return bytes;
        }

        /// <summary>
        /// Decodifica y valida una firma SUBIDA como archivo. Acepta PNG, JPG y WEBP y siempre
        /// devuelve PNG: es el formato con el que el estampador del PDF respeta la transparencia, y
        /// guardar un único formato evita que el estampado dependa de con qué se subió la firma.
        ///
        /// Un PNG que ya entra chico se devuelve tal cual, para no reescribir (y engordar) lo que el
        /// canvas ya generó con el tamaño justo.
        /// </summary>
        public static byte[] DecodeImagenSubida(string? imageBase64)
        {
            var bytes = DecodeBase64(imageBase64, "Debe subir una imagen antes de guardar.");

            if (bytes.Length > MaxBytesSubida)
                throw new AbrilException("La imagen es demasiado grande (máximo 12 MB).");

            if (EsPng(bytes) && bytes.Length <= MaxBytes)
                return bytes;

            byte[] png;
            try
            {
                using var imagen = Image.Load(bytes);

                // Solo se achica: agrandar una firma pequeña no le agrega detalle, solo peso.
                if (imagen.Width > AnchoMaximo)
                    imagen.Mutate(x => x.Resize(AnchoMaximo, 0));

                using var salida = new MemoryStream();
                imagen.Save(salida, new PngEncoder());
                png = salida.ToArray();
            }
            catch (AbrilException) { throw; }
            catch (Exception)
            {
                // ImageSharp tira excepciones distintas según el formato; para el usuario es lo mismo.
                throw new AbrilException(
                    "No pudimos leer la imagen. Sube un archivo PNG, JPG o WEBP.");
            }

            if (png.Length > MaxBytes)
                throw new AbrilException(
                    "La imagen es demasiado pesada incluso reducida. Sube una versión más liviana.");

            return png;
        }

        private static bool EsPng(byte[] bytes) =>
            bytes.Length >= PngMagic.Length && bytes.Take(PngMagic.Length).SequenceEqual(PngMagic);

        /// <summary>Quita el prefijo <c>data:…;base64,</c> si viene y decodifica.</summary>
        private static byte[] DecodeBase64(string? imageBase64, string mensajeVacio)
        {
            if (string.IsNullOrWhiteSpace(imageBase64))
                throw new AbrilException(mensajeVacio);

            var raw = imageBase64.Trim();

            var commaIdx = raw.IndexOf(',');
            if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIdx >= 0)
                raw = raw[(commaIdx + 1)..];

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(raw);
            }
            catch (FormatException)
            {
                throw new AbrilException("La firma no tiene un formato de imagen válido.");
            }

            if (bytes.Length == 0)
                throw new AbrilException("La firma está vacía.");

            return bytes;
        }
    }
}
