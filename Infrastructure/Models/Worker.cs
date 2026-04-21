using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Infrastructure.Models
{
    [Table("workers")]
    public class Worker
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("apellido_nombre")]
        public string? ApellidoNombre { get; set; }

        [Column("categoria")]
        public string? Categoria { get; set; }

        [Column("ocupacion")]
        public string? Ocupacion { get; set; }

        [Column("estado")]
        public string? Estado { get; set; }
    }
}
