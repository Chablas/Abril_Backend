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

        /// <summary>
        /// Área/gerencia por defecto de la convocatoria recurrente de este tema (ej. "Reunión de
        /// Jefaturas de Proyectos" siempre convoca a Gerencia de Proyectos). Null = sin convocatoria
        /// asociada; se combina con los puestos de ReunionTemaPuesto igual que en la convocatoria
        /// masiva manual.
        /// </summary>
        public int? AreaScopeId { get; set; }

        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
