namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Tipo de correo configurable del módulo de Reclutamiento (tabla <c>gth_correo_tipo</c>).
    /// Cada tipo tiene su propio juego de destinatarios en <see cref="GthCorreoDestinatario"/>.
    /// Códigos estables:
    ///   <c>SOLICITUD</c> → correo de "nueva solicitud de personal" (va a GTH).
    ///   <c>LONG_LIST</c> → correo de "long list enviada" (va al solicitante).
    /// </summary>
    public class GthCorreoTipo
    {
        public int GthCorreoTipoId { get; set; }
        /// <summary>Clave estable usada en código (SOLICITUD, LONG_LIST).</summary>
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        /// <summary>Texto que explica a quién se le manda el correo; lo muestra la pantalla de Configuración.</summary>
        public string? Descripcion { get; set; }
        public int Orden { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
