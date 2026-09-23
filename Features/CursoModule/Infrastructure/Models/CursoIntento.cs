using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.CursoModule.Infrastructure.Models
{
    [Table("curso_intento")]
    public class CursoIntento
    {
        public int Id { get; set; }
        public int CursoId { get; set; }
        public int UserId { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public decimal? NotaFinal { get; set; }
        public bool? Aprobado { get; set; }

        /// <summary>"en_progreso" | "finalizado".</summary>
        public string Estado { get; set; } = "en_progreso";
    }
}
