using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Shared
{
    /// <summary>
    /// El <see cref="AbrilEmailLayout"/> con el pie de Gestión GTH · Onboarding. Todo el chrome
    /// (tarjeta, cabecera, tablas, franjas, botones, colores) vive en la clase base: acá solo va lo
    /// propio del módulo, que es el pie.
    ///
    /// Mismo criterio editorial que Reclutamiento: el correo lleva datos y un acceso, no
    /// explicaciones. La bajada es UNA línea y el resto se ve en la pantalla a la que lleva el
    /// enlace.
    /// </summary>
    public sealed class OnboardingEmailLayout : AbrilEmailLayout
    {
        private const string PieOnboarding =
            "Correo automático de Abril One · Gestión GTH · Onboarding.";

        public OnboardingEmailLayout(string assetsUrl) : base(assetsUrl, PieOnboarding) { }

        /// <summary>
        /// Layout con el origen de las imágenes que corresponde. Ver <see cref="AssetsUrl"/> en la
        /// clase base para por qué esa clave es distinta de <c>App:FrontendUrl</c>.
        /// </summary>
        public static OnboardingEmailLayout Desde(IConfiguration configuration) =>
            new(AssetsUrl(configuration));
    }
}
