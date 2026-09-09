using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Models
{
    [Table("ss_checklist_partida")]
    public class SsChecklistPartida
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = null!;

        [Column("descripcion")]
        public string? Descripcion { get; set; }

        [Column("orden")]
        public int Orden { get; set; } = 0;

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        public ICollection<SsChecklistPlantilla> Plantillas { get; set; } = new List<SsChecklistPlantilla>();
    }
}
