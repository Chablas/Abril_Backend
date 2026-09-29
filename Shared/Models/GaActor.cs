using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// Catálogo de los cinco papeles que alguien cumple sobre el ciclo de una salida (aprobar la
    /// salida, enterarse de ella, aprobar la primera revisión, consolidar y firmar el consolidado).
    /// Ids fijos: los compara el código vía <c>ActorIds</c>.
    /// </summary>
    [Table("ga_actor")]
    public class GaActor
    {
        [Key]
        [Column("ga_actor_id")]
        public int GaActorId { get; set; }

        [Column("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>true = cuentan todos los activos de la lista; false = solo el primero activo.</summary>
        [Column("multiple")]
        public bool Multiple { get; set; }

        [Column("display_order")]
        public int DisplayOrder { get; set; }

        [Column("state")]
        public bool State { get; set; } = true;
    }
}
