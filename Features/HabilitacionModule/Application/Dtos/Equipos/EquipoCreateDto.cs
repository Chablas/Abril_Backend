namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Equipos
{
    public class EquipoCreateDto
    {
        public string Tipo { get; set; } = string.Empty;
        public string? Marca { get; set; }
        public string? Modelo { get; set; }
        public string? NSerie { get; set; }
        public string? NVin { get; set; }
        public string? Capacidad { get; set; }
        public int? PropietarioEmpresaId { get; set; }
        public int ProyectoId { get; set; }
        public string? EmailAdmin { get; set; }
        public string? EmailSsoma { get; set; }
    }
}
