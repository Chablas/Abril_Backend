namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Destinatario de un correo configurable del módulo de Reclutamiento
    /// (tabla <c>gth_correo_destinatario</c>). Se define por BD y se edita desde la UI
    /// (botón Configuración) para poder alternar fácilmente entre un correo de pruebas y
    /// el de producción sin redeploy. Cada fila pertenece a un <see cref="GthCorreoTipo"/>
    /// (SOLICITUD / LONG_LIST): así conviven los destinatarios de "nueva solicitud" y los
    /// de "long list enviada" en la misma tabla.
    /// <c>es_copia</c> = false → destinatario principal (Para/To); true → copia (CC).
    /// </summary>
    public class GthCorreoDestinatario
    {
        public int GthCorreoDestinatarioId { get; set; }
        /// <summary>FK a <see cref="GthCorreoTipo"/>: a qué correo pertenece este destinatario.</summary>
        public int GthCorreoTipoId { get; set; }
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
