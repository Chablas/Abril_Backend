using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Models
{
    [Table("ev_evaluacion_supervisor_contratista")]
    public class EvEvaluacionSupervisorContratista
    {
        public int Id { get; set; }
        public int PeriodoId { get; set; }
        public int ProyectoId { get; set; }
        public int ContributorId { get; set; }

        [Column("supervisor_ss_contratista_usuario_id")]
        public int SupervisorSsContratistaUsuarioId { get; set; }
        public string SupervisorNombre { get; set; } = string.Empty;
        public int EvaluadorUserId { get; set; }
        public decimal? Nota { get; set; }
        public string? Comentario { get; set; }
        public bool NoAplica { get; set; }
        public string? NoAplicaMotivo { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<EvEvaluacionSupervisorContratistaDetalle> Detalles { get; set; } = [];
    }
}
