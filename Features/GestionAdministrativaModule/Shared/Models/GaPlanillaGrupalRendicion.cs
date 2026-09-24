namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Qué planillas de rendición cubre una <see cref="GaPlanillaGrupal"/>. Mismo esquema que
    /// <see cref="GaConsolidadoS10Rendicion"/>: una planilla de rendición tiene a lo sumo UN vínculo
    /// vigente (índice único parcial <c>ux_ga_planilla_grupal_rendicion_una_vigente</c>), que es
    /// además lo que impide que entre en una segunda planilla grupal.
    /// </summary>
    public class GaPlanillaGrupalRendicion
    {
        public int Id { get; set; }

        /// <summary>FK a <c>ga_planilla_grupal.id</c>.</summary>
        public int PlanillaGrupalId { get; set; }

        /// <summary>FK a <c>ga_rendicion.id</c>: la planilla cubierta.</summary>
        public int RendicionId { get; set; }

        /// <summary>Soft delete: false = vínculo dado de baja (se conserva para auditoría).</summary>
        public bool State { get; set; } = true;
    }
}
