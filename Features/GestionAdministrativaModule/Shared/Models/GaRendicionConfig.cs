using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Configuración global de las rendiciones: una sola fila viva (índice único parcial
    /// <c>ux_ga_rendicion_config_singleton</c>), porque el plazo es de toda la organización y no
    /// por área ni por trabajador.
    ///
    /// Hoy solo guarda los días hábiles de plazo, que antes era la constante
    /// <c>CalendarioNoLaborable.DiasHabilesDePlazo = 7</c>. Se administra desde
    /// Mis Rendiciones → Configuración → Días reembolsables.
    /// </summary>
    [Table("ga_rendicion_config")]
    public class GaRendicionConfig
    {
        /// <summary>Días hábiles de plazo permitidos. Fuera de este rango la base también corta (CHECK).</summary>
        public const int DiasMinimo = 1;
        public const int DiasMaximo = 28;

        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// Días hábiles del mes SIGUIENTE que dura el plazo para rendir un mes. Con 5, las salidas
        /// de agosto se rinden hasta el 5.º día hábil de setiembre.
        /// </summary>
        [Column("dias_habiles_plazo")]
        public int DiasHabilesPlazo { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("created_user_id")]
        public int? CreatedUserId { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [Column("updated_user_id")]
        public int? UpdatedUserId { get; set; }

        /// <summary>Soft delete: false = eliminada (se conserva para auditoría).</summary>
        [Column("state")]
        public bool State { get; set; } = true;
    }
}
