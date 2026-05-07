using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Infrastructure.Models
{
    [Table("cat_jefatura")]
    public class CatJefatura
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("email")]
        public string? Email { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;
    }
}
