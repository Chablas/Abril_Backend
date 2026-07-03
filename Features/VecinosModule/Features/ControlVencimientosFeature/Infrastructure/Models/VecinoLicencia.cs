namespace Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Infrastructure.Models
{
    /// <summary>
    /// Licencia/permiso con una fecha de vencimiento. La fecha de recordatorio indica
    /// con cuánta antelación (en días) se debe avisar antes del vencimiento real para
    /// que la empresa pueda tomar medidas a tiempo.
    /// </summary>
    public class VecinoLicencia
    {
        public int VecinoLicenciaId { get; set; }

        /// <summary>URL del archivo de la licencia/permiso almacenado.</summary>
        public string ArchivoUrl { get; set; } = null!;
        public string? OriginalFileName { get; set; }

        /// <summary>Fecha real en la que vence la licencia/permiso.</summary>
        public DateOnly FechaVencimiento { get; set; }

        /// <summary>Fecha en la que se debe enviar el recordatorio (antes del vencimiento).</summary>
        public DateOnly FechaRecordatorio { get; set; }

        /// <summary>Días de antelación respecto al vencimiento (FechaVencimiento - FechaRecordatorio).</summary>
        public int DiasAntes { get; set; }

        /// <summary>Momento en que el cron envió el recordatorio (null = aún no enviado).</summary>
        public DateTime? RecordatorioEnviadoDateTime { get; set; }

        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
