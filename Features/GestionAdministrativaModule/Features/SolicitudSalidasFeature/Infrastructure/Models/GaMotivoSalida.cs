namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models
{
    public class GaMotivoSalida
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
