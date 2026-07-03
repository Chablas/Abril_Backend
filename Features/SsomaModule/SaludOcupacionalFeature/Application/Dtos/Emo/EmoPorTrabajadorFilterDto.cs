namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Emo
{
    public class EmoPorTrabajadorFilterDto
    {
        public string? Search { get; set; }
        public string? Aptitud { get; set; }
        public string? Estado { get; set; }
        public int? EmpresaId { get; set; }
        public int? ProyectoId { get; set; }
        public DateOnly? FechaEmoDesde { get; set; }
        public DateOnly? FechaEmoHasta { get; set; }
        public bool SinLectura { get; set; }
        public bool SinCertificado { get; set; }
        public bool SinEmoCompleto { get; set; }

        /// <summary>"fechaEmo" | "fechaVencimiento". Cualquier otro valor (o null) ordena por nombre.</summary>
        public string? SortBy { get; set; }
        public bool SortDesc { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
