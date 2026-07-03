namespace Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Dtos
{
    /// <summary>Datos del formulario de creación de una licencia (el archivo va aparte como IFormFile).</summary>
    public class VecinoLicenciaCreateDto
    {
        public DateOnly FechaVencimiento { get; set; }
        public DateOnly FechaRecordatorio { get; set; }
        public int DiasAntes { get; set; }
        /// <summary>Correos a los que se enviará el recordatorio (pueden ser grupos; se desglosan al enviar).</summary>
        public List<string> Emails { get; set; } = new();
    }

    /// <summary>Una licencia/permiso registrada, tal como se lista en el frontend.</summary>
    public class VecinoLicenciaDto
    {
        public int VecinoLicenciaId { get; set; }
        public string ArchivoUrl { get; set; } = null!;
        public string? OriginalFileName { get; set; }
        public DateOnly FechaVencimiento { get; set; }
        public DateOnly FechaRecordatorio { get; set; }
        public int DiasAntes { get; set; }
        /// <summary>Correos destinatarios del recordatorio.</summary>
        public List<string> Emails { get; set; } = new();
        /// <summary>Momento en que se envió el recordatorio (null = pendiente).</summary>
        public DateTime? RecordatorioEnviadoDateTime { get; set; }
    }

    /// <summary>Resultado del procesamiento del cron de recordatorios.</summary>
    public class RecordatoriosResultDto
    {
        public int LicenciasProcesadas { get; set; }
        public int CorreosEnviados { get; set; }
        public List<string> Errores { get; set; } = new();
    }
}
