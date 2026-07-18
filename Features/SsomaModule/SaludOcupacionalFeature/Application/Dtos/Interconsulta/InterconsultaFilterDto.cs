namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Interconsulta
{
    public class InterconsultaFilterDto
    {
        public string? Estado { get; set; }
        public int? WorkerId { get; set; }
        public int? ProyectoId { get; set; }
        public int? ContributorId { get; set; }
        /// <summary>Filtra por workers.obra_oficina (Staff, Oficina Central, Obra, Contratista).</summary>
        public string? ObraOficina { get; set; }
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 15;
    }
}
