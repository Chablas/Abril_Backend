using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Models
{
    [Table("bim_proyecto_torre")]
    public class BimProyectoTorre
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("project_id")]
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("orden")]
        public int Orden { get; set; }

        /// <summary>Cantidad de sectores para los niveles de esta torre con
        /// TipoEstructura = SUBESTRUCTURA. Los sectores (1..N) no se persisten
        /// como filas: se derivan de este número al vuelo.</summary>
        [Column("cantidad_sectores_subestructura")]
        public int CantidadSectoresSubestructura { get; set; }

        /// <summary>Análogo a <see cref="CantidadSectoresSubestructura"/> para
        /// niveles con TipoEstructura = SUPERESTRUCTURA.</summary>
        [Column("cantidad_sectores_superestructura")]
        public int CantidadSectoresSuperestructura { get; set; }

        public List<BimTorreNivel> Niveles { get; set; } = new();
    }
}
