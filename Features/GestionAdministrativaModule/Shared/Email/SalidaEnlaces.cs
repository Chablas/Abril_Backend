namespace Abril_Backend.Features.GestionAdministrativa.Shared.Email
{
    /// <summary>
    /// Las URLs de la intranet a las que llevan los botones de los correos de salidas. Están acá y
    /// no escritas en cada plantilla porque el botón tiene que caer en la solicitud EXACTA: si la
    /// query cambia de nombre, cambia en un solo lugar y no en tres correos.
    ///
    /// Ambas pantallas leen <c>?solicitud=</c> al entrar y abren ese detalle solo.
    /// </summary>
    public static class SalidaEnlaces
    {
        /// <summary>Base del frontend (<c>App:FrontendUrl</c>), sin barra final.</summary>
        public static string Base(IConfiguration configuration) =>
            (configuration["App:FrontendUrl"] ?? "https://intranet.abril.pe").TrimEnd('/');

        /// <summary>
        /// Gestión de Salidas abierta en esa solicitud — es la pantalla donde el jefe aprueba o
        /// rechaza el reembolso.
        /// </summary>
        public static string Gestion(IConfiguration configuration, int solicitudId) =>
            $"{Base(configuration)}/gestion-administrativa/gestion-salidas?solicitud={solicitudId}";

        /// <summary>
        /// Solicitud de Salidas (el autoservicio del trabajador) abierta en esa solicitud — es
        /// donde ve el detalle de la salida en sí.
        /// </summary>
        public static string Autoservicio(IConfiguration configuration, int solicitudId) =>
            $"{Base(configuration)}/gestion-administrativa/solicitud-salidas?solicitud={solicitudId}";

        /// <summary>
        /// Mis Rendiciones abierta en esa planilla — es donde el trabajador adjunta (o vuelve a
        /// adjuntar, para subsanar) el Consolidado del S10 y avisa a su revisor. Todo lo que va
        /// después de rendir vive ahí, así que es el destino de los correos del reembolso.
        /// </summary>
        public static string Rendiciones(IConfiguration configuration, int rendicionId) =>
            $"{Base(configuration)}/gestion-administrativa/rendiciones?rendicion={rendicionId}";
    }
}
