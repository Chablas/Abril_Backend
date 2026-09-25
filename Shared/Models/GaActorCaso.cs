using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// Catálogo de los tipos de trabajador para los que se resuelven los actores: oficina central,
    /// staff, jefe, residente y subgerente. Ids fijos: los compara el código vía <c>ActorCasoIds</c>.
    /// </summary>
    [Table("ga_actor_caso")]
    public class GaActorCaso
    {
        [Key]
        [Column("ga_actor_caso_id")]
        public int GaActorCasoId { get; set; }

        [Column("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("display_order")]
        public int DisplayOrder { get; set; }

        [Column("state")]
        public bool State { get; set; } = true;
    }
}
