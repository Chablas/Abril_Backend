namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>Códigos estables del catálogo ga_correo_evento (los correos del flujo de salidas).</summary>
    public static class CorreoEventoCodigos
    {
        public const string Revisor = "REVISOR";
        public const string Confirmacion = "CONFIRMACION";
        public const string Aprobada = "APROBADA";
        public const string Rechazada = "RECHAZADA";

        // ── Primera revisión de la rendición ─────────────────────────────────
        // Los cuatro correos del paso que va ANTES del Consolidado del S10: el trabajador envía la
        // planilla, el jefe la aprueba u observa, y solo con la aprobación se habilita cargar el
        // consolidado. Ver EstadosSalida.PrimeraRevision.

        /// <summary>
        /// Al jefe/revisor: hay una rendición esperando su primera revisión. Es el único de los
        /// cuatro con dos botones (aprobar / observar), que lo llevan a la pantalla con la acción
        /// ya abierta — observar exige un comentario, así que no se puede resolver desde el correo.
        /// </summary>
        public const string RendicionPrimeraRevision = "REN_PRIMERA_REVISION";

        /// <summary>
        /// Al solicitante: su rendición quedó registrada y se envió a primera revisión. Informativo
        /// (no lleva botón de acción, solo el acceso a la pantalla).
        /// </summary>
        public const string RendicionEnviada = "REN_ENVIADA";

        /// <summary>Al solicitante: el jefe aprobó la primera revisión y ya puede cargar el S10.</summary>
        public const string RendicionPrimeraAprobada = "REN_PRIMERA_APROBADA";

        /// <summary>
        /// Al solicitante: el jefe observó la primera revisión, con el comentario de qué corregir
        /// antes de volver a generar la rendición.
        /// </summary>
        public const string RendicionPrimeraObservada = "REN_PRIMERA_OBSERVADA";

        /// <summary>
        /// Aviso al jefe/revisor de que el trabajador ya adjuntó el Consolidado del S10 y su
        /// reembolso está esperando revisión. Lo dispara el trabajador desde el autoservicio.
        /// </summary>
        public const string S10Revisor = "S10_REVISOR";

        /// <summary>El jefe aprobó el reembolso de una salida rendida — se avisa al solicitante.</summary>
        public const string ReembolsoAprobado = "REEMBOLSO_APROBADO";

        /// <summary>
        /// La jefatura observó el reembolso — se avisa al solicitante con la observación y con
        /// los dos caminos para subsanar (arreglar el S10 él mismo o pedírselo al Coordinador ERP).
        ///
        /// El código de la fila NO cambió cuando el estado pasó de llamarse "Rechazado" a
        /// "Observado": es la clave del catálogo y renombrarla habría desconectado la
        /// configuración de destinatarios que ya está cargada.
        /// </summary>
        public const string ReembolsoObservado = "REEMBOLSO_RECHAZADO";

        // ── Subsanación con el Coordinador ERP ───────────────────────────────
        // Los dos correos del paso del medio de la subsanación (§10.5): el trabajador le pide la
        // corrección al ERP y el ERP le confirma que ya la hizo. Ver EstadosSalida.CorreccionS10.

        /// <summary>
        /// Al Coordinador ERP: hay una corrección del Consolidado del S10 esperándolo, con la
        /// guía, la observación de la jefatura y el «MOTIVO *» del trabajador (RF-OBS-06). Lo
        /// dispara el trabajador desde Mis Rendiciones, así que se administra ahí.
        /// </summary>
        public const string CorreccionS10Solicitada = "CORRECCION_S10_SOLICITADA";

        /// <summary>
        /// Al solicitante: el ERP ya corrigió en el S10 y puede recargar el Consolidado
        /// (RF-OBS-08). Se origina en la bandeja del ERP, que es su propia pantalla.
        /// </summary>
        public const string CorreccionS10Atendida = "CORRECCION_S10_ATENDIDA";

        // ── Tesorería ────────────────────────────────────────────────────────

        /// <summary>
        /// A Tesorería: la jefatura ya firmó una planilla y su reembolso entró a la bandeja de
        /// pago (RF-TES-01). Se origina en Gestión de Rendiciones, que es donde se firma; el
        /// destinatario principal se resuelve por puesto (categoría Tesorero), no por área.
        /// </summary>
        public const string TesoreriaReembolso = "TESORERIA_REEMBOLSO";

        /// <summary>
        /// Al solicitante: Tesorería ya pagó su reembolso (RG-28 / RF-TES-11). Es el único correo
        /// que se origina en Reembolsos y cierra el ciclo.
        /// </summary>
        public const string ReembolsoPagado = "REEMBOLSO_PAGADO";
    }

    /// <summary>
    /// Códigos estables del catálogo ga_correo_pantalla: la pantalla donde se ORIGINA cada correo
    /// y, por lo tanto, la que lo administra en su botón «Configuración».
    /// </summary>
    public static class CorreoPantallaCodigos
    {
        /// <summary>Los que salen al crear la solicitud (al revisor y al solicitante).</summary>
        public const string SolicitudSalidas = "SOLICITUD_SALIDAS";

        /// <summary>
        /// Los que dispara el trabajador desde Mis Rendiciones: enviar la planilla a primera
        /// revisión (con su acuse) y avisar que adjuntó el Consolidado del S10.
        /// </summary>
        public const string Rendiciones = "RENDICIONES";

        /// <summary>La decisión del revisor sobre la solicitud: aprobada o rechazada.</summary>
        public const string GestionSalidas = "GESTION_SALIDAS";

        /// <summary>Las dos decisiones del revisor sobre la planilla: primera revisión y reembolso.</summary>
        public const string GestionRendiciones = "GESTION_RENDICIONES";

        /// <summary>Tesorería: el aviso de pago al solicitante, que es lo que cierra el ciclo.</summary>
        public const string Reembolsos = "REEMBOLSOS";

        /// <summary>
        /// La bandeja del Coordinador ERP: el único correo que se origina acá es el aviso al
        /// trabajador de que la corrección ya se hizo en el S10.
        /// </summary>
        public const string CorreccionesS10 = "CORRECCIONES_S10";

        /// <summary>
        /// Traduce el segmento de URL de la pantalla (el que usa el frontend y la ruta del
        /// controller) al código del catálogo. Devuelve null si el segmento no es una pantalla
        /// conocida, para que el controller responda 404 en vez de listar todo.
        /// </summary>
        public static string? DesdeSegmento(string? segmento) =>
            (segmento ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "solicitud-salidas"   => SolicitudSalidas,
                "rendiciones"         => Rendiciones,
                "gestion-salidas"     => GestionSalidas,
                "gestion-rendiciones" => GestionRendiciones,
                "reembolsos"          => Reembolsos,
                "correcciones-s10"    => CorreccionesS10,
                _                     => null,
            };
    }

    /// <summary>Códigos estables del catálogo ga_correo_tipo_destinatario.</summary>
    public static class CorreoTipoCodigos
    {
        public const string Trabajador = "TRABAJADOR";
        public const string Area = "AREA";
        public const string Correo = "CORREO";
    }
}
