using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Shared
{
    /// <summary>
    /// El <see cref="AbrilEmailLayout"/> con el pie de SSOMA · Salud Ocupacional. Todo el chrome
    /// (tarjeta, cabecera, tablas, franjas, colores) vive en la clase base, en
    /// <c>Shared/Services/Email/Layout/</c>: si hay que tocar cómo se ve un correo se toca allá y
    /// cambian todos los correos brandeados a la vez.
    ///
    /// Lo usan los dos correos de EMO: <see cref="EmoConfirmacionEmailTemplate"/> y
    /// <see cref="EmoResultadoEmailTemplate"/>. El de confirmación armó su propio HTML hasta que
    /// se le pidió el título centrado como el resto: se veía parecido porque había copiado las
    /// medidas y los colores, pero al copiarlos también se le fue una constante de fuente rota y
    /// nadie lo notó hasta ahí. Los correos de programación creada y de rechazo de la clínica
    /// siguen con su HTML propio, sin brandear, en el repositorio.
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
