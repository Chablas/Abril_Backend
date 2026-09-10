namespace Abril_Backend.Features.GestionAdministrativa.Shared.Email
{
    /// <summary>
    /// Las URLs de la intranet a las que llevan los botones de los correos de salidas. Están acá y
    /// no escritas en cada plantilla porque el botón tiene que caer en la solicitud EXACTA: si la
    /// query cambia de nombre, cambia en un solo lugar y no en tres correos.
    ///
    /// Las pantallas de salidas leen <c>?solicitud=</c> al entrar y las de planillas
    /// <c>?rendicion=</c>; en ambos casos abren ese detalle solo.
    /// </summary>
    public static class SalidaEnlaces
    {
        /// <summary>Base del frontend (<c>App:FrontendUrl</c>), sin barra final.</summary>
        public static string Base(IConfiguration configuration) =>
            (configuration["App:FrontendUrl"] ?? "https://intranet.abril.pe").TrimEnd('/');

        /// <summary>
        /// Gestión de Salidas abierta en esa solicitud — es la pantalla donde el jefe aprueba o
        /// rechaza la SALIDA. El reembolso ya no se decide ahí: eso es
        /// <see cref="GestionRendiciones"/>.
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
        /// Solicitud de Salidas sin abrir ninguna solicitud — es también la pantalla donde el
        /// trabajador RINDE (elige el mes en «Mes a rendir» y presiona Rendir). La usan los
        /// recordatorios del plazo: hablan de varias salidas a la vez, así que no hay una sola que
        /// abrir.
        /// </summary>
        public static string Autoservicio(IConfiguration configuration) =>
            $"{Base(configuration)}/gestion-administrativa/solicitud-salidas";

        /// <summary>
        /// Gestión de Rendiciones abierta en esa planilla — es la pantalla donde el revisor mira el
        /// Consolidado del S10, aprueba o rechaza el reembolso y firma. La unidad es la PLANILLA y
        /// no la salida: el documento que revisa cubre a todas las salidas que agrupa.
        /// </summary>
        public static string GestionRendiciones(IConfiguration configuration, int rendicionId) =>
            $"{Base(configuration)}/gestion-administrativa/gestion-rendiciones?rendicion={rendicionId}";

        /// <summary>
        /// Gestión de Rendiciones abierta en esa planilla y con la acción de la primera revisión ya
        /// planteada (<c>&amp;accion=aprobar|observar</c>). Los dos botones del correo al revisor
        /// entran por acá: la decisión no se ejecuta desde el correo porque observar exige escribir
        /// un comentario, así que el enlace lleva a la pantalla con el diálogo abierto y el revisor
        /// confirma ahí, viendo los tramos y las capturas.
        /// </summary>
        public static string GestionRendicionesAccion(
            IConfiguration configuration, int rendicionId, string accion) =>
            $"{GestionRendiciones(configuration, rendicionId)}&accion={accion}";

        /// <summary>
        /// Mis Rendiciones abierta en esa planilla — es donde el trabajador adjunta (o vuelve a
        /// adjuntar, para subsanar) el Consolidado del S10 y avisa a su revisor. Todo lo que va
        /// después de rendir vive ahí, así que es el destino de los correos del reembolso.
        /// </summary>
        public static string Rendiciones(IConfiguration configuration, int rendicionId) =>
            $"{Base(configuration)}/gestion-administrativa/rendiciones?rendicion={rendicionId}";

        /// <summary>
        /// Correcciones S10 abierta en esa solicitud de corrección — la bandeja del Coordinador
        /// ERP. La unidad es la CORRECCION y no la planilla: una planilla puede haber pasado por
        /// varias correcciones y el correo tiene que abrir la que lo disparo.
        /// </summary>
        public static string CorreccionesS10(IConfiguration configuration, int correccionId) =>
            $"{Base(configuration)}/gestion-administrativa/correcciones-s10?correccion={correccionId}";

        /// <summary>
        /// Reembolsos abierta en esa planilla — la bandeja de Tesorería, donde se confirma la
        /// revisión documental y se paga. Es el destino del aviso que le llega a Tesorería cuando
        /// la jefatura firma.
        /// </summary>
        public static string Reembolsos(IConfiguration configuration, int rendicionId) =>
            $"{Base(configuration)}/gestion-administrativa/reembolsos?rendicion={rendicionId}";
    }
}
