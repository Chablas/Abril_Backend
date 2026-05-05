namespace Abril_Backend.Features.Habilitacion.Application.Dtos.ControlAcceso
{
    public class ControlAccesoWorkerDto
    {
        public int WorkerId { get; set; }
        public string ApellidoNombre { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public string EmpresaNombre { get; set; } = string.Empty;
        public string ProyectoNombre { get; set; } = string.Empty;
        public string EstadoHabilitacion { get; set; } = string.Empty;
        public bool EmpresaActiva { get; set; }
        public List<string> DocumentosFaltantes { get; set; } = [];
        public List<string> DocumentosPorVencer { get; set; } = [];
        public List<EntregableResumenDto> Entregables { get; set; } = [];
    }
}
