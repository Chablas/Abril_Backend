using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.CursoModule.Infrastructure.Models
{
    /// <summary>
    /// Catálogo reutilizable de preguntas evaluables entre cursos (V/F, opción múltiple,
    /// ordenar, etc.). Elegir una desde aquí CLONA su ConfiguracionJson dentro de una nueva
    /// CursoSlide del curso destino — no queda enlazada al banco, así que editar la entrada
    /// del banco después nunca altera exámenes ya rendidos en otros cursos (evidencia SUNAFIL).
    /// </summary>
    [Table("curso_pregunta_banco")]
    public class CursoPreguntaBanco
    {
        public int Id { get; set; }
        public string TipoCodigo { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string? Categoria { get; set; }
        public decimal? PuntajeSugerido { get; set; }
        public string ConfiguracionJson { get; set; } = "{}";
        public bool Activo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
