namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application
{
    /// <summary>
    /// Códigos estables de los tipos de correo configurables del módulo de Reclutamiento
    /// (espejo de <c>gth_correo_tipo.codigo</c>). Se usan para scopear los destinatarios.
    /// </summary>
    public static class CorreoTipoReclutamiento
    {
        /// <summary>
        /// Correo de "solicitud de personal por aprobar" (va al Gerente General). Es el primer
        /// correo del flujo: hasta que el GG no aprueba, GTH no recibe nada.
        /// </summary>
        public const string AprobacionGg = "APROBACION_GG";

        /// <summary>
        /// Correo de "nueva solicitud de personal" (va a GTH). Sale recién cuando Gerencia General
        /// aprueba, y solo con las vacantes aprobadas.
        /// </summary>
        public const string Solicitud = "SOLICITUD";

        /// <summary>Correo de "long list enviada" (va al solicitante).</summary>
        public const string LongList = "LONG_LIST";

        /// <summary>Correo de "decisión de long list" (lo envía el solicitante y va a GTH).</summary>
        public const string LongListDecision = "LONG_LIST_DECISION";

        /// <summary>
        /// Correo de "decisión de finalista" (lo envía el solicitante al aprobar o rechazar a un
        /// finalista y va a GTH).
        /// </summary>
        public const string FinalistaDecision = "FINALISTA_DECISION";

        /// <summary>
        /// Correo de "formulario del postulante completado" (va a GTH). Sale solo, sin que nadie lo
        /// dispare, en cuanto el postulante envía su formulario desde la página pública, para que
        /// GTH sepa que ya lo puede revisar.
        /// </summary>
        public const string FormularioCompletado = "FORMULARIO_COMPLETADO";

        /// <summary>
        /// Correo de "invitación a la entrevista" (va al postulante citado). El destinatario
        /// principal es SIEMPRE el postulante; la configuración solo aporta principales adicionales
        /// y copias, por si GTH quiere quedarse con el registro de cada citación.
        /// </summary>
        public const string Entrevista = "ENTREVISTA";

        /// <summary>
        /// Traduce el slug de la URL (<c>aprobacion-gg</c> / <c>solicitud</c> / <c>long-list</c> /
        /// <c>decision-long-list</c> / <c>decision-finalista</c> / <c>formulario-completado</c> /
        /// <c>entrevista</c>) al código estable. Devuelve null si el slug no corresponde a un tipo
        /// conocido.
        /// </summary>
        public static string? FromSlug(string? slug) => slug?.Trim().ToLowerInvariant() switch
        {
            "aprobacion-gg"          => AprobacionGg,
            "solicitud"              => Solicitud,
            "long-list"              => LongList,
            "decision-long-list"     => LongListDecision,
            "decision-finalista"     => FinalistaDecision,
            "formulario-completado"  => FormularioCompletado,
            "entrevista"             => Entrevista,
            _ => null,
        };
    }
}
