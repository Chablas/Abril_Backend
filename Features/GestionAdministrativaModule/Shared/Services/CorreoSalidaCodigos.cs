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

        /// <summary>El jefe rechazó el reembolso — se avisa al solicitante con la observación.</summary>
        public const string ReembolsoRechazado = "REEMBOLSO_RECHAZADO";
    }

    /// <summary>Códigos estables del catálogo ga_correo_tipo_destinatario.</summary>
    public static class CorreoTipoCodigos
    {
        public const string Trabajador = "TRABAJADOR";
        public const string Area = "AREA";
        public const string Correo = "CORREO";
    }
}
