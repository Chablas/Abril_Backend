using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Models
{
    [Table("ev_evaluacion_staff_detalle")]
    public class EvEvaluacionStaffDetalle
    {
        public int Id { get; set; }

        [Column("evaluacion_id")]
        public int EvaluacionId { get; set; }

        public int? PlantillaId { get; set; }

        /// <summary>Copia del criterio al momento de evaluar, aunque luego se edite la plantilla.</summary>
        public string Criterio { get; set; } = string.Empty;

        public int Puntaje { get; set; }

        [ForeignKey(nameof(EvaluacionId))]
        public EvEvaluacionStaff? Evaluacion { get; set; }
    }
}
