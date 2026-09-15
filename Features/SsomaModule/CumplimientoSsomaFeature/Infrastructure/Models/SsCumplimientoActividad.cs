using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Models
{
    // Catálogo de actividades mínimas obligatorias del Coordinador SSOMA / Prevencionista.
    [Table("ss_cumplimiento_actividad")]
    public class SsCumplimientoActividad
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = null!;

        [Column("descripcion")]
        public string? Descripcion { get; set; }

        // "coordinador_ssoma" | "prevencionista" | "ambos"
        [Column("rol_responsable")]
        public string RolResponsable { get; set; } = "ambos";

        // "diaria" | "semanal" | "mensual"
        [Column("frecuencia")]
        public string Frecuencia { get; set; } = "diaria";

        [Column("orden")]
        public int Orden { get; set; } = 0;

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        public ICollection<SsCumplimientoRegistro> Registros { get; set; } = new List<SsCumplimientoRegistro>();
    }
}
