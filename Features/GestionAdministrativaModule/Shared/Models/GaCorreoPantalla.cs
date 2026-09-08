using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Catálogo de las pantallas de Gestión Administrativa que tienen correos configurables.
    /// Agrupa <see cref="GaCorreoEvento"/> por el lugar DONDE SE ORIGINA cada correo (no por quién
    /// lo recibe): cada pantalla del flujo tiene su propio botón «Configuración» y administra solo
    /// los suyos, en vez de la pantalla única Configuración → Correos que se dio de baja.
    ///
    /// Los correos que no nacen de una acción del solicitante sino de la decisión de un revisor
    /// (solicitud aprobada/rechazada, primera revisión, reembolso) cuelgan de la pantalla del
    /// revisor que los dispara: Gestión de Salidas o Gestión de Rendiciones.
    /// </summary>
    [Table("ga_correo_pantalla")]
    public class GaCorreoPantalla
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// Clave estable de la pantalla (SOLICITUD_SALIDAS, RENDICIONES, GESTION_SALIDAS,
        /// GESTION_RENDICIONES, REEMBOLSOS). Ver <c>CorreoPantallaCodigos</c>.
        /// </summary>
        [Column("codigo")]
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Nombre de la pantalla, como se muestra en su Configuración.</summary>
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Orden de visualización (el del flujo).</summary>
        [Column("orden")]
        public int Orden { get; set; }

        /// <summary>false = la pantalla ya no administra correos (no aparece en desplegables).</summary>
        [Column("active")]
        public bool Active { get; set; } = true;

        /// <summary>Soft delete: false = eliminada (se conserva para auditoría).</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
