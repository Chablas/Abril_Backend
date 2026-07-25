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

        /// <summary>
        /// Traduce el slug de la URL (<c>solicitud</c> / <c>long-list</c>) al código estable.
        /// Devuelve null si el slug no corresponde a un tipo conocido.
        /// </summary>
        public static string? FromSlug(string? slug) => slug?.Trim().ToLowerInvariant() switch
        {
            "solicitud" => Solicitud,
            "long-list" => LongList,
            _ => null,
        };
    }
}
