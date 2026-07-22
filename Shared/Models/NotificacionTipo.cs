namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// Catálogo de tipos de notificación in-app (tabla <c>notificacion_tipo</c>). Normalizado a
    /// tabla en vez de texto plano (regla del proyecto). <c>codigo</c> es la clave estable usada
    /// en código (p.ej. <c>GTH_SOLICITUD_PERSONAL</c>).
    /// </summary>
    public class NotificacionTipo
    {
        public int NotificacionTipoId { get; set; }
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }

    /// <summary>Códigos estables de tipos de notificación (espejo de notificacion_tipo.codigo).</summary>
    public static class NotificacionTipoCodigo
    {
        /// <summary>Alguien registró una solicitud de personal (Gestión GTH · Reclutamiento).</summary>
        public const string GthSolicitudPersonal = "GTH_SOLICITUD_PERSONAL";
    }
}
