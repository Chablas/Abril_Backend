namespace Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Dtos
{
    /// <summary>Datos del formulario de creación de una licencia (el archivo va aparte como IFormFile).</summary>
    public class VecinoLicenciaCreateDto
    {
        public DateOnly FechaVencimiento { get; set; }
        public DateOnly FechaRecordatorio { get; set; }
        public int DiasAntes { get; set; }
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
    }
}
