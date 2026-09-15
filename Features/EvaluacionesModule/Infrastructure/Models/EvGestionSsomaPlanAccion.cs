using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Models
{
    /// <summary>
    /// Acción concreta y medible que un Coordinador SSOMA o Prevencionista redacta
    /// sobre un criterio de su propia evaluación de Gestión SSOMA (ver
    /// EvEvaluacionGestionSsomaDetalle). A diferencia de EvJefeSsomaPlanAccion, aquí
    /// SÍ hay muchos dueños posibles — cada fila pertenece a quien la creó
    /// (CreatedByUserId) y las consultas siempre filtran por ese usuario, nunca solo
    /// por período.
    /// </summary>
    [Table("ev_gestion_ssoma_plan_accion")]
    public class EvGestionSsomaPlanAccion
    {
        public int Id { get; set; }
        public int PeriodoId { get; set; }
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
