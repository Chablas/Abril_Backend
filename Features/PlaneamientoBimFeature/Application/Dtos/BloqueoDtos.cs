namespace Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos
{
    public class BloqueoDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateTimeOffset FechaCreacion { get; set; }
        public DateTimeOffset? FechaActualizacion { get; set; }
        public DateTimeOffset? FechaCierre { get; set; }
    }

    public class BloqueoCreateDto
    {
        public string Descripcion { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }

    public class BloqueoUpdateDto
    {
        public string Descripcion { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }
}
