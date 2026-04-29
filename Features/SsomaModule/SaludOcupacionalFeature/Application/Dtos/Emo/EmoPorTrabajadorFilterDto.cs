namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Emo
{
    public class EmoPorTrabajadorFilterDto
    {
        public string? Search { get; set; }
        public string? Aptitud { get; set; }
        public string? Estado { get; set; }
        public int? EmpresaId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
