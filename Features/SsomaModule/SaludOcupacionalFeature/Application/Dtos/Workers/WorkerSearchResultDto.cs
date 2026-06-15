namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers
{
    public class WorkerSearchResultDto
    {
        public int Id { get; set; }
        public string? ApellidoNombre { get; set; }
        public string? Dni { get; set; }
        public string? Ocupacion { get; set; }
        public string? Categoria { get; set; }
        public string? Cargo { get; set; }
        public int? EmpresaActualId { get; set; }
        public string? EmpresaActual { get; set; }
        public bool Activo { get; set; }
    }
}
