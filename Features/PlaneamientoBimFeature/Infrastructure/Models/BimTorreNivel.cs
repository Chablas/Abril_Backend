using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Models
{
    [Table("bim_torre_nivel")]
    public class BimTorreNivel
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("torre_id")]
        public int TorreId { get; set; }
        public BimProyectoTorre Torre { get; set; } = null!;

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("orden")]
        public int Orden { get; set; }

        /// <summary>'SUBESTRUCTURA' | 'SUPERESTRUCTURA' | NULL (sin clasificar).
        /// Nullable a nivel de BD a propósito: BOSQUE REAL tiene 2 niveles reales
        /// sin este campo definido. La obligatoriedad para guardados nuevos se
        /// valida en PlaneamientoBimConfiguracionService, no acá.</summary>
        [Column("tipo_estructura")]
        public string? TipoEstructura { get; set; }
    }
}
