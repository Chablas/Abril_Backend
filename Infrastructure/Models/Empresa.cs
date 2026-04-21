using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Infrastructure.Models
{
    [Table("companies")]
    public class Empresa
    {
        [Column("id")]
        public int Id { get; set; }
    }
}
