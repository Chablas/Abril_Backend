namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models
{
    /// <summary>
    /// Catálogo de temas predefinidos para agendar reuniones (ej. Reunión de Jefatura de Proyectos).
    /// El tema personalizado escrito a mano no se registra aquí: solo queda en reunion.tema.
    /// </summary>
    public class ReunionTema
    {
        public int ReunionTemaId { get; set; }
        public string Descripcion { get; set; } = null!;

        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
