namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Bandeja
{
    public class BandejaItemDto
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string NombreEntregable { get; set; } = string.Empty;
        public string EntidadNombre { get; set; } = string.Empty;
        public string? EmpresaNombre { get; set; }
        public string? ProyectoNombre { get; set; }
        public int? ProyectoId { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime? Vigencia { get; set; }
        public string? ArchivoUrl { get; set; }
        public string? ObsContratista { get; set; }
        public string Responsable { get; set; } = string.Empty;
        public DateTime? FechaEnvio { get; set; }
        public int? ItemId { get; set; }
        public bool EsMensual { get; set; }
        public int? Mes { get; set; }
        public int? Anio { get; set; }
        public int MesesPendientes { get; set; }
    }
}
