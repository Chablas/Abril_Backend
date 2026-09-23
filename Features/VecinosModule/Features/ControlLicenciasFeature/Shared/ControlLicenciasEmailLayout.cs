using Abril_Backend.Shared.Services.Email.Layout;

namespace Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Shared
{
    /// <summary>
    /// El <see cref="AbrilEmailLayout"/> con el pie de Vecinos · Control de Licencias. Todo el
    /// chrome (tarjeta blanca sobre lienzo verdoso, cabecera centrada, tarjetas de datos, franjas de
    /// estado, botón verde y logo al pie) vive en la clase base: acá solo va lo propio del módulo.
    ///
    /// Reemplaza al HTML suelto que armaba <c>ControlLicenciasService</c> a mano: esos correos no
    /// llevaban ni el logo ni los datos del proyecto/razón social, y se veían distintos al resto de
    /// la intranet.
    /// </summary>
    public sealed class ControlLicenciasEmailLayout : AbrilEmailLayout
    {
        private const string PieControlLicencias =
            "Correo automático de Abril One · Vecinos · Control de Licencias.";

        public ControlLicenciasEmailLayout(string assetsUrl) : base(assetsUrl, PieControlLicencias) { }

        /// <summary>
        /// Layout con el origen de las imágenes que corresponde (<c>App:EmailAssetsUrl</c>, que en
        /// dev apunta a producción a propósito: Outlook descarga las imágenes por el proxy de
        /// Microsoft y ese proxy nunca puede alcanzar un localhost).
        /// </summary>
        public static ControlLicenciasEmailLayout Desde(IConfiguration configuration) =>
            new(AssetsUrl(configuration));
    }
}
