using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Models
{
    [Table("bim_zona_nivel")]
    public class BimZonaNivel
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("zona_id")]
        public int ZonaId { get; set; }
        public BimProyectoZona Zona { get; set; } = null!;

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("orden")]
        public int Orden { get; set; }
    }
}
