namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Qué planillas de rendición cubre un Consolidado del S10. Un consolidado es UN registro en el
    /// S10 y puede agrupar varias planillas —de uno o de varios trabajadores, siempre de una misma
    /// razón social—, así que el vínculo vive en esta tabla y no en una columna del consolidado.
    ///
    /// Una planilla tiene a lo sumo UN vínculo vigente (índice único parcial
    /// <c>ux_ga_consolidado_s10_rendicion_una_vigente</c>). Reemplazar el consolidado no borra
    /// nada: los vínculos viejos quedan con <see cref="State"/> = false y el consolidado anterior
    /// conserva la lista exacta de planillas que cubrió.
    /// </summary>
    public class GaConsolidadoS10Rendicion
    {
        public int Id { get; set; }

        /// <summary>FK a <c>ga_consolidado_s10.id</c>.</summary>
        public int ConsolidadoS10Id { get; set; }

        /// <summary>FK a <c>ga_rendicion.id</c>: la planilla cubierta.</summary>
        public int RendicionId { get; set; }

        /// <summary>
        /// Soft delete: false = la planilla ya no está cubierta por este consolidado (se le adjuntó
        /// otro). El consolidado mismo pasa a state = false cuando no le queda ningún vínculo
        /// vigente.
        /// </summary>
        public bool State { get; set; } = true;
    }
}
