using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Models
{
    /// <summary>Huérfana desde el rediseño de Configuración Inicial (sectores ahora
    /// se derivan de BimProyectoTorre.CantidadSectoresX + BimTorreNivel.TipoEstructura,
    /// no se persisten). Ya no tiene ningún consumidor por FK: BimRestriccion.Sector y
    /// BimRegistroDiario.SectorId pasaron a ser int planos. Se mantiene el modelo/tabla
    /// solo para no perder los datos históricos que quedaron apuntando acá.</summary>
    [Table("bim_zona_sector")]
    public class BimZonaSector
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("zona_id")]
        public int ZonaId { get; set; }
        public BimProyectoTorre Zona { get; set; } = null!;

        /// <summary>NULL = sector compartido por todos los niveles de la zona
        /// (comportamiento historico). Con valor = sector exclusivo de ese nivel.</summary>
        [Column("zona_nivel_id")]
        public int? ZonaNivelId { get; set; }
        public BimTorreNivel? ZonaNivel { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("orden")]
        public int Orden { get; set; }
    }
}
