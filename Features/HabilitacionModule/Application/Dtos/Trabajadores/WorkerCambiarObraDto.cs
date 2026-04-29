namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores
{
    public class WorkerCambiarObraDto
    {
        public int NuevoProyectoId { get; set; }
        public int? NuevaEmpresaId { get; set; }
        public DateTime FechaCambio { get; set; }
    }
}
