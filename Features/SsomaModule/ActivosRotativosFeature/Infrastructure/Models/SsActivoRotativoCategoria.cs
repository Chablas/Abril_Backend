using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models
{
    // Catálogo de materiales/equipos rotativos: "Tambor Retráctil", "Freno de Cuerda", etc.
    // Lista única — al crear un activo se elige de acá (o se agrega uno nuevo si falta).
    [Table("ss_activo_rotativo_material")]
    public class SsActivoRotativoMaterial
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = null!;

        [Column("orden")]
        public int Orden { get; set; } = 0;

        // Vínculo opcional al ítem real de Presupuesto Materiales (S10), para poder
        // sumar cuánto se compró/despachó de este material vs. cuántos activos hay
        // registrados. Null = sin vincular, no se muestra comparación.
        [Column("presupuesto_item_id")]
        public int? PresupuestoItemId { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [ForeignKey(nameof(PresupuestoItemId))]
        public SsMaterialItem? PresupuestoItem { get; set; }

        public ICollection<SsActivoRotativo> Activos { get; set; } = new List<SsActivoRotativo>();
    }
}
