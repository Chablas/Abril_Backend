namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Archivos
{
    public class SubirArchivoRequest
    {
        public IFormFile? File { get; set; }
        public string Contexto { get; set; } = "";
        public int? HabTrabajadorId { get; set; }
        public string? ObsContratista { get; set; }
    }
}
