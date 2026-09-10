using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models
{
    // Equipo/material que rota entre proyectos (tambores retráctiles, frenos de cuerda, etc.).
    // Visibilidad + contacto: no hay flujo de aprobación, solo trazabilidad de quién lo tiene.
    [Table("ss_activo_rotativo")]
    public class SsActivoRotativo
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = null!;

        // Material/equipo del catálogo único (Tambor Retráctil, Freno de Cuerda, etc.).
        [Column("material_id")]
        public int MaterialId { get; set; }

        [Column("codigo")]
        public string? Codigo { get; set; }

        // "disponible" | "en_uso" | "mantenimiento" | "baja"
        [Column("estado")]
        public string Estado { get; set; } = "disponible";

        // Null = en almacén central, sin asignar a ningún proyecto
        [Column("proyecto_actual_id")]
        public int? ProyectoActualId { get; set; }

        // A quién contactar para solicitarlo (puede no coincidir con quien lo registró)
        [Column("responsable_nombre")]
        public string? ResponsableNombre { get; set; }

        [Column("responsable_telefono")]
        public string? ResponsableTelefono { get; set; }

        [Column("observaciones")]
        public string? Observaciones { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [ForeignKey(nameof(MaterialId))]
        public SsActivoRotativoMaterial? Material { get; set; }

        [ForeignKey(nameof(ProyectoActualId))]
        public Project? ProyectoActual { get; set; }

        public ICollection<SsActivoRotativoMovimiento> Movimientos { get; set; } = new List<SsActivoRotativoMovimiento>();
    }
}
