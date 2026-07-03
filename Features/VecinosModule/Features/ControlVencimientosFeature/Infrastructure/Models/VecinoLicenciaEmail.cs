namespace Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Infrastructure.Models
{
    /// <summary>
    /// Correo destinatario del recordatorio de una licencia. Puede ser un correo individual
    /// o un grupo (mail-enabled): al enviar, el cron lo desglosa con IEmailGroupResolver.
    /// </summary>
    public class VecinoLicenciaEmail
    {
        public int VecinoLicenciaEmailId { get; set; }

        public int VecinoLicenciaId { get; set; }
        public VecinoLicencia? VecinoLicencia { get; set; }

        public string Email { get; set; } = null!;

        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
