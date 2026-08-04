using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Models
{
    [Table("bim_bloqueo")]
    public class BimBloqueo
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("project_id")]
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        [Column("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [Column("estado")]
        public string Estado { get; set; } = string.Empty;

        [Column("fecha_creacion")]
        public DateTimeOffset FechaCreacion { get; set; }

        [Column("fecha_actualizacion")]
        public DateTimeOffset FechaActualizacion { get; set; }

        [Column("fecha_cierre")]
        public DateTimeOffset? FechaCierre { get; set; }

        [Column("created_user_id")]
        public int CreatedUserId { get; set; }
    }
}
