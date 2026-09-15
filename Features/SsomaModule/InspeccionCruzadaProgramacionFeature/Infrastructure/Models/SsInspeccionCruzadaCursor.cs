using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Models
{
    /// <summary>
    /// Una fila por anillo: recuerda el desplazamiento circular vigente
    /// (<see cref="Offset"/>, entre 1 y CantidadMiembros-1) y el último mes ya generado, para
    /// que agregar o quitar un proyecto del anillo no reinicie la continuidad de los demás
    /// meses. Cada mes el desplazamiento avanza uno (mod cantidad de miembros activos, nunca 0
    /// — evita que un proyecto se "inspeccione a sí mismo").
    /// </summary>
    [Table("ss_inspeccion_cruzada_cursor")]
    public class SsInspeccionCruzadaCursor
    {
        [Key]
        public int AnilloId { get; set; }

        [Column("offset_valor")]
        public int Offset { get; set; } = 1;
        public int? UltimoAnio { get; set; }
        public int? UltimoMes { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
