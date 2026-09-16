using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Catálogo del alcance de rendición: hasta qué mes hacia atrás se pueden rendir las salidas.
    /// Hoy tiene dos filas —"Hasta el mes anterior" (1 mes) y "Hasta 6 meses atrás" (6)— y agregar
    /// otra opción es agregar una fila, no tocar código.
    ///
    /// Lo eligen las dos columnas de <c>ga_rendicion_config</c>:
    /// <c>alcance_plazo_id</c> (lo que alcanza la ventana de los días hábiles) y
    /// <c>alcance_permanente_id</c> (lo que alcanza en cualquier momento del mes). Ver
    /// <see cref="Abril_Backend.Shared.Constants.RendicionAlcanceIds"/>.
    /// </summary>
    [Table("ga_rendicion_alcance")]
    public class GaRendicionAlcance
    {
        [Key]
        [Column("ga_rendicion_alcance_id")]
        public int GaRendicionAlcanceId { get; set; }

        [Column("codigo")]
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Lo que se lee en el desplegable ("Hasta 6 meses atrás").</summary>
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Meses hacia atrás que abarca, contados desde el mes en curso: 1 = el mes anterior. El
        /// mes en curso siempre se puede rendir, así que el catálogo arranca en 1.
        /// </summary>
        [Column("meses_atras")]
        public int MesesAtras { get; set; }

        [Column("orden")]
        public int Orden { get; set; }

        /// <summary>false = no se ofrece en el desplegable (las filas ya elegidas siguen valiendo).</summary>
        [Column("active")]
        public bool Active { get; set; } = true;

        [Column("state")]
        public bool State { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
