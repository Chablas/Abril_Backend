namespace Abril_Backend.Features.Habilitacion.Application.Dtos.SctrVidaley
{
    public class SctrWorkerDto
    {
        public int WorkerId { get; set; }
        public string ApellidoNombre { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public bool Aprobado { get; set; }
    }
}
