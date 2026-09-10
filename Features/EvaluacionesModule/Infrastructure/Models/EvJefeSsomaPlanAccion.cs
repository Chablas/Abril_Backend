using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Models
{
    /// <summary>
    /// Acción concreta y medible que el Jefe SSOMA redacta sobre un criterio de su
    /// propia evaluación (ver EvEvaluacionJefeSsomaDetalle). Contenido del evaluado,
    /// no del evaluador — no lleva nada del lado anónimo de la evaluación.
    /// </summary>
    [Table("ev_jefe_ssoma_plan_accion")]
    public class EvJefeSsomaPlanAccion
    {
        public int Id { get; set; }
        public int PeriodoId { get; set; }
        public int? PlantillaId { get; set; }
        public string Criterio { get; set; } = string.Empty;
        public string Accion { get; set; } = string.Empty;
        public string Meta { get; set; } = string.Empty;
        public DateOnly? FechaLimite { get; set; }
        public string Estado { get; set; } = "Pendiente";
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
