using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Infrastructure.Models
{
    /// <summary>Snapshot semanal de la carga (ponderada por tipo de partida) de cada
    /// supervisor/arquitecto — permite ver si la sobrecarga de "Distribución de Carga"
    /// es puntual o un patrón que se repite semana a semana.</summary>
    [Table("ac_carga_semanal")]
    public class AcCargaSemanal
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("semana")]
        public DateOnly Semana { get; set; }

        [Column("hitos")]
        public int Hitos { get; set; }

        [Column("entregables")]
        public int Entregables { get; set; }

        [Column("consultas")]
        public int Consultas { get; set; }

        [Column("total")]
        public int Total { get; set; }

        [Column("total_ponderado")]
        public decimal TotalPonderado { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}
