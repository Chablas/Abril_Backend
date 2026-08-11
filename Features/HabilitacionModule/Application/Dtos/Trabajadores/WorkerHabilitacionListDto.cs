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
        /// <summary>Nombre de la categoría (campo de lógica).</summary>
        public string? Categoria { get; set; }
        /// <summary>Nombre del puesto (campo de presentación).</summary>
        public string? Puesto { get; set; }
        public string? ContrataCasa { get; set; }
        public int? ObraOficinaStaffId { get; set; }
        public string? ObraOficina { get; set; }
        public string EstadoWorker { get; set; } = "ACTIVO";
        public bool TieneEmo { get; set; }
        public int? DiasRestantesEmo { get; set; }
        public string? EstadoProgramacionEmo { get; set; }
        public int? AniosExperiencia { get; set; }
        public string? FechaIngreso { get; set; }
        /// <summary>"Pendiente" si tiene una interconsulta sin levantar — usado para advertir
        /// antes de programarle un nuevo EMO (ver ProgramarEmoDialogComponent).</summary>
        public string? InterconsultaEstado { get; set; }
        public string? InterconsultaEspecialidad { get; set; }
    }
}
