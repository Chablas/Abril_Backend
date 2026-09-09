using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models
{
    // Categoría de activo rotativo: "Tambor Retráctil", "Freno de Cuerda", etc.
    [Table("ss_activo_rotativo_categoria")]
    public class SsActivoRotativoCategoria
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = null!;

        [Column("orden")]
        public int Orden { get; set; } = 0;

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        public ICollection<SsActivoRotativo> Activos { get; set; } = new List<SsActivoRotativo>();
    }
}
