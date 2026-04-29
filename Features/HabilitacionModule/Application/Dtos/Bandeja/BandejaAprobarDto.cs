namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Bandeja
{
    public class BandejaAprobarDto
    {
        public string Estado { get; set; } = string.Empty;
        public string? ObsAbril { get; set; }
        public DateTime? Vigencia { get; set; }
    }
}
