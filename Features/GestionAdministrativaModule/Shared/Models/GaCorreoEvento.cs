using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Catálogo de los correos configurables del flujo de solicitud de salidas. Cada fila es
    /// uno de los correos que el sistema envía (REVISOR, CONFIRMACION, APROBADA, RECHAZADA) y
    /// sobre el que se pueden definir reglas de inclusión/exclusión de destinatarios
    /// (<see cref="GaCorreoRegla"/>). Sirve para no hardcodear los destinatarios en código.
    /// </summary>
    [Table("ga_correo_evento")]
    public class GaCorreoEvento
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>Clave estable del correo (REVISOR, CONFIRMACION, APROBADA, RECHAZADA).</summary>
        [Column("codigo")]
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Nombre para mostrar en la pantalla de configuración.</summary>
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Descripción de cuándo se envía y a quién (informativa para la UI).</summary>
        [Column("descripcion")]
        public string? Descripcion { get; set; }

        /// <summary>Orden de visualización.</summary>
        [Column("orden")]
        public int Orden { get; set; }

        /// <summary>Si false, el correo no aparece en la configuración (no se usa hoy, previsto para el futuro).</summary>
        [Column("active")]
        public bool Active { get; set; } = true;

        /// <summary>Soft delete: false = eliminado (se conserva para auditoría).</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
