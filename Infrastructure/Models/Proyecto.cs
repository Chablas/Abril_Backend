using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Infrastructure.Models
{
    [Table("projects")]
    public class Proyecto
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("nombre")]
        public string? Nombre { get; set; }

        [Column("estado")]
        public string? Estado { get; set; }

        [Column("responsable_arq_com")]
        public string? ResponsableArqCom { get; set; }

        [Column("responsable_arq_com_id")]
        public int? ResponsableArqComId { get; set; }
    }
}
