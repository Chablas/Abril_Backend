using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Configuración global de las rendiciones: una sola fila viva (índice único parcial
    /// <c>ux_ga_rendicion_config_singleton</c>), porque el plazo es de toda la organización y no
    /// por área ni por trabajador.
    ///
    /// Guarda los días hábiles de plazo —que antes era la constante
    /// <c>CalendarioNoLaborable.DiasHabilesDePlazo = 7</c>— y el tope de movilidad por fecha de
    /// salida. Se administra desde Solicitud de Salidas → Configuración → Días reembolsables.
    /// </summary>
    [Table("ga_rendicion_config")]
    public class GaRendicionConfig
    {
        /// <summary>Días hábiles de plazo permitidos. Fuera de este rango la base también corta (CHECK).</summary>
        public const int DiasMinimo = 1;
        public const int DiasMaximo = 28;

        /// <summary>Tope de movilidad aceptado. Fuera de este rango la base también corta (CHECK).</summary>
        public const decimal LimiteDiarioMinimo = 0.01m;
        public const decimal LimiteDiarioMaximo = 1000m;

        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// Días hábiles del mes SIGUIENTE que dura el plazo para rendir un mes. Con 5, las salidas
        /// de agosto se rinden hasta el 5.º día hábil de setiembre.
        /// </summary>
        [Column("dias_habiles_plazo")]
        public int DiasHabilesPlazo { get; set; }

        /// <summary>
        /// Tope de movilidad en soles. El mismo número manda en dos momentos: es el máximo de UN
        /// TRAYECTO al subir capturas y montos, y el máximo de UN DÍA al imprimir la planilla —lo
        /// que un día no aguanta se imputa al siguiente. Ver <c>TopeMovilidad</c> e
        /// <c>ImputacionMovilidadPlanilla</c>.
        ///
        /// La columna conserva el nombre <c>limite_diario_movilidad</c> con el que nació: el tope
        /// sigue siendo diario en la planilla, que es donde el número se hace valer contra el día.
        /// </summary>
        [Column("limite_diario_movilidad")]
        public decimal LimiteDiarioMovilidad { get; set; }

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
