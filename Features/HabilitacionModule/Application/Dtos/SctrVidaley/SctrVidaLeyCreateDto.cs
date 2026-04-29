namespace Abril_Backend.Features.Habilitacion.Application.Dtos.SctrVidaley
{
    public class SctrVidaLeyCreateDto
    {
        public int ProyectoId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public int Mes { get; set; }
        public int Anio { get; set; }
        public string? ArchivoUrl { get; set; }
        public string? ArchivoUrl2 { get; set; }
        public List<int> WorkerIds { get; set; } = [];
    }
}
