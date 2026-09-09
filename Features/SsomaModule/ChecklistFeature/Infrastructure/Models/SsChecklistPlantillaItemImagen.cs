using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Models
{
    // Galería de fotos de referencia ("cómo debe quedar") de un ítem de plantilla.
    // Sin límite de cantidad — distinta de UrlAdjunto (evidencia real subida al completar el ítem).
    [Table("ss_checklist_plantilla_item_imagen")]
    public class SsChecklistPlantillaItemImagen
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("plantilla_item_id")]
        public int PlantillaItemId { get; set; }

        [Column("url")]
        public string Url { get; set; } = null!;

        [Column("orden")]
        public int Orden { get; set; } = 0;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [ForeignKey(nameof(PlantillaItemId))]
        public SsChecklistPlantillaItem? PlantillaItem { get; set; }
    }
}
