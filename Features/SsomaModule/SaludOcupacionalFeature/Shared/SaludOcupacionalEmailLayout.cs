using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Shared
{
    /// <summary>
    /// El <see cref="AbrilEmailLayout"/> con el pie de SSOMA · Salud Ocupacional. Todo el chrome
    /// (tarjeta, cabecera, tablas, franjas, colores) vive en la clase base, en
    /// <c>Shared/Services/Email/Layout/</c>: si hay que tocar cómo se ve un correo se toca allá y
    /// cambian todos los correos brandeados a la vez.
    ///
    /// No confundir con <see cref="EmoConfirmacionEmailTemplate"/>, que es anterior a que el
    /// layout subiera a Shared y arma su propio HTML: ese correo se ve igual porque copió las
    /// mismas medidas y colores, no porque comparta el código.
    /// </summary>
    public sealed class SaludOcupacionalEmailLayout : AbrilEmailLayout
    {
        private const string PieSaludOcupacional =
            "Correo automático de Abril One · SSOMA · Salud Ocupacional.";

        public SaludOcupacionalEmailLayout(string assetsUrl) : base(assetsUrl, PieSaludOcupacional) { }

        /// <summary>
        /// Layout con el origen de las imágenes que corresponde. Ver <c>AssetsUrl</c> en la clase
        /// base para por qué esa clave es distinta de <c>App:FrontendUrl</c>.
        /// </summary>
        public static SaludOcupacionalEmailLayout Desde(IConfiguration configuration) =>
            new(AssetsUrl(configuration));
    }
}
