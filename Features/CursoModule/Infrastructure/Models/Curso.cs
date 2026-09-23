using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.CursoModule.Infrastructure.Models
{
    [Table("curso")]
    public class Curso
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string? CategoriaNombre { get; set; }

        /// <summary>Rol destino del curso (mismo patrón de filtrado por rol que LearningModule). Nulo/vacío = visible para todos.</summary>
        public string? RolDestino { get; set; }

        public decimal NotaMinimaAprobacion { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
