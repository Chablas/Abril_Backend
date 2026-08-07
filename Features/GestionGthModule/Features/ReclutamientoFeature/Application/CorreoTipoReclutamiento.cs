namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application
{
    /// <summary>
    /// Códigos estables de los tipos de correo configurables del módulo de Reclutamiento
    /// (espejo de <c>gth_correo_tipo.codigo</c>). Se usan para scopear los destinatarios.
    /// </summary>
    public static class CorreoTipoReclutamiento
    {
        /// <summary>Correo de "nueva solicitud de personal" (va a GTH).</summary>
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
        /// Traduce el slug de la URL (<c>solicitud</c> / <c>long-list</c> /
        /// <c>decision-long-list</c> / <c>decision-finalista</c>) al código estable. Devuelve null
        /// si el slug no corresponde a un tipo conocido.
        /// </summary>
        public static string? FromSlug(string? slug) => slug?.Trim().ToLowerInvariant() switch
        {
            "solicitud"           => Solicitud,
            "long-list"           => LongList,
            "decision-long-list"  => LongListDecision,
            "decision-finalista"  => FinalistaDecision,
            _ => null,
        };
    }
}
