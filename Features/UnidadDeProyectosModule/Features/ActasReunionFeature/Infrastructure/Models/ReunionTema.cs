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

        /// <summary>Si este tema exige que los convocados carguen una agenda antes de la reunión.</summary>
        public bool RequiereAgenda { get; set; }
        /// <summary>
        /// True = agenda fija (siempre el mismo <see cref="AgendaTexto"/>, se edita una sola vez acá).
        /// False = agenda dinámica (cada participante propone sus temas en cada ocurrencia, ver
        /// ReunionAgendaItem). Solo aplica si RequiereAgenda es true.
        /// </summary>
        public bool AgendaFija { get; set; }
        public string? AgendaTexto { get; set; }
        /// <summary>
        /// Horas de anticipación (admite decimales) para recordar a los convocados que carguen su
        /// agenda. Solo aplica si RequiereAgenda es true y AgendaFija es false.
        /// </summary>
        public decimal? RecordatorioHorasAntes { get; set; }

        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
