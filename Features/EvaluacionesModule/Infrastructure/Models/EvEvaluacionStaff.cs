using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Models
{
    // Evaluación 360° de Staff: el Residente de un proyecto evalúa, de forma
    // IDENTIFICADA (a diferencia del flujo anónimo de Jefe SSOMA/Gestión SSOMA),
    // a cada trabajador de staff de SU proyecto. Sí guarda EvaluadorUserId
    // porque acá no hay anonimato — es reporte 360° normal.
    [Table("ev_evaluacion_staff")]
    public class EvEvaluacionStaff
    {
        public int Id { get; set; }
        public int PeriodoId { get; set; }

        [Column("evaluador_user_id")]
        public int EvaluadorUserId { get; set; }

        [Column("evaluado_worker_id")]
        public int EvaluadoWorkerId { get; set; }

        [Column("project_id")]
        public int ProjectId { get; set; }

        public decimal? Nota { get; set; }
        public string? Comentario { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<EvEvaluacionStaffDetalle> Detalles { get; set; } = [];
    }
}
