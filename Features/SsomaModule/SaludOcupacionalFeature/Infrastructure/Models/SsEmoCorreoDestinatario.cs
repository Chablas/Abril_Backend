using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models
{
    /// <summary>
    /// Destinatario de los correos de programación de EMO (tanto la programación
    /// manual desde /emos como la automática del cron diario). El
    /// <see cref="TipoId"/> decide si va en "Para" (PRINCIPAL) o en "CC" (COPIA);
    /// <see cref="Active"/> prende/apaga el envío a ese destinatario sin borrarlo,
    /// igual que <see cref="SsDescansoCorreoConfig"/> en Mi Salud.
    ///
    /// Hay dos clases de fila:
    ///  • Editables (<see cref="Editable"/> = true): correo escrito a mano, con CRUD
    ///    completo desde la pantalla de configuración.
    ///  • Fijas (<see cref="Editable"/> = false): destinatarios dinámicos cuyo correo
    ///    NO vive acá sino que se resuelve al enviar (hoy solo <c>CLINICA</c>, que
    ///    expande a los emails de contacto de la clínica de la programación). Solo
    ///    se pueden prender/apagar — no se editan ni se eliminan.
    /// </summary>
    [Table("ss_emo_correo_destinatario")]
    public class SsEmoCorreoDestinatario
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tipo_id")]
        public int TipoId { get; set; }

        /// <summary>Clave estable de los destinatarios fijos/dinámicos (hoy solo CLINICA). Null en los editables.</summary>
        [Column("codigo")]
        public string? Codigo { get; set; }

        /// <summary>Correo destino. Null solo en los destinatarios fijos/dinámicos.</summary>
        [Column("email")]
        public string? Email { get; set; }

        /// <summary>Nombre/etiqueta del destinatario, para mostrarlo en la pantalla.</summary>
        [Column("nombre")]
        public string? Nombre { get; set; }

        /// <summary>Texto informativo bajo el nombre (usado por los destinatarios fijos).</summary>
        [Column("descripcion")]
        public string? Descripcion { get; set; }

        /// <summary>false = fila fija: solo se puede prender/apagar, no editar ni eliminar.</summary>
        [Column("editable")]
        public bool Editable { get; set; } = true;

        [Column("orden")]
        public int Orden { get; set; }

        /// <summary>true = se le envía el correo; false = no se le envía.</summary>
        [Column("active")]
        public bool Active { get; set; } = true;

        [Column("state")]
        public bool State { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [ForeignKey(nameof(TipoId))]
        public SsEmoCorreoTipo? Tipo { get; set; }
    }
}
