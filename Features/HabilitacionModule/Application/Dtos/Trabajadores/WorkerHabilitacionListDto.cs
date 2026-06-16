namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores
{
    public class WorkerHabilitacionListDto
    {
        public int WorkerId { get; set; }
        public string ApellidoNombre { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public string? EmpresaNombre { get; set; }
        public int? EmpresaId { get; set; }
        public string? ProyectoActual { get; set; }
        public int? ProyectoActualId { get; set; }
        public string EstadoHabilitacion { get; set; } = string.Empty;
        public string? Categoria { get; set; }
        public string? Ocupacion { get; set; }
        public string? ContrataCasa { get; set; }
        public string? ObraOficina { get; set; }
        public string EstadoWorker { get; set; } = "ACTIVO";
        public bool TieneEmo { get; set; }
        public int? DiasRestantesEmo { get; set; }
        public string? EstadoProgramacionEmo { get; set; }
        public int? AniosExperiencia { get; set; }
        public string? FechaIngreso { get; set; }
    }
}
