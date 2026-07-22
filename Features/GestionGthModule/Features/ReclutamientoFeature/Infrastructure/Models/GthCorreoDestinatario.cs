namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Destinatario del correo de notificación de "nueva solicitud de personal"
    /// (tabla <c>gth_correo_destinatario</c>). Se define por BD y se edita desde la UI
    /// (botón Configuración) para poder alternar fácilmente entre un correo de pruebas y
    /// el de GTH en producción sin redeploy.
    /// <c>es_copia</c> = false → destinatario principal (Para/To); true → copia (CC).
    /// </summary>
    public class GthCorreoDestinatario
    {
        public int GthCorreoDestinatarioId { get; set; }
        /// <summary>Correo destinatario (se guarda en minúsculas).</summary>
        public string Email { get; set; } = null!;
        /// <summary>false = Para (To); true = Copia (CC).</summary>
        public bool EsCopia { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
