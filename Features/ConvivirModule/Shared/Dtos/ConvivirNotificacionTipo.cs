namespace Abril_Backend.Features.ConvivirModule.Shared.Dtos
{
    /// <summary>
    /// Códigos de <c>propietario_notificacion_tipo</c>: los avisos de la campana de la app (RF-05).
    /// Los demás tipos del documento funcional (comunicado, evento, encuesta, postventa) se suman
    /// con sus módulos.
    /// </summary>
    public static class ConvivirNotificacionTipo
    {
        /// <summary>Se culminó un hito para propietarios del proyecto: abre Mi Proyecto.</summary>
        public const string Hito = "HITO";
        /// <summary>La intranet subió un documento a una de sus propiedades: abre Mis documentos.</summary>
        public const string Documento = "DOCUMENTO";
    }
}
