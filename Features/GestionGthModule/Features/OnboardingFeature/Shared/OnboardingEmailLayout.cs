using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Shared
{
    /// <summary>
    /// El <see cref="AbrilEmailLayout"/> con el pie de Gestión GTH · Onboarding. Todo el chrome
    /// (tarjeta, cabecera, tablas, franjas, botones, colores, logo al pie) vive en la clase base,
    /// en <c>Shared/Services/Email/Layout/</c>: acá abajo solo va el pie y el criterio editorial
    /// de esta feature. Si hay que tocar cómo se ve un correo, se toca la clase base y cambian
    /// todos los correos de la empresa a la vez.
    ///
    /// Criterio editorial: el destinatario de este correo es alguien que TODAVÍA no es usuario de
    /// la app —recién firma su carta oferta— y no tiene ninguna pantalla nuestra donde ver el
    /// proceso. Vale por eso la misma excepción que los correos al candidato de Reclutamiento: se
    /// le puede escribir en primera persona y contarle qué tiene que hacer. Lo que no cambia es
    /// que las condiciones de la propuesta NO van en el correo: van dentro del enlace, que es
    /// personal.
    /// </summary>
    public sealed class OnboardingEmailLayout : AbrilEmailLayout
    {
        /// <summary>
        /// Pie de los correos de la feature. Sin el código del onboarding ni el del requerimiento:
        /// es jerga nuestra y a quien acaba de ser contratado no le dice nada.
        /// </summary>
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
