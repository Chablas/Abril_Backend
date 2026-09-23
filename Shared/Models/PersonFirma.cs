using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// La firma de una persona, una por <see cref="FirmaTipo"/>. Reemplaza a las columnas
    /// <c>person.signature_*</c>, que solo aguantaban UNA firma y siempre dibujada.
    ///
    /// Que sean varias filas y no columnas nuevas en <c>person</c> es lo que permite la regla de
    /// Consolidados: si la configuración pide firma subida como imagen y la persona solo tiene la
    /// dibujada, se le pide la que falta sin perder la que ya tenía — y al revés igual.
    ///
    /// Un índice único parcial (<c>ux_person_firma_vigente</c>) deja UNA fila viva por persona y
    /// tipo: volver a registrar reemplaza la imagen de esa fila.
    /// </summary>
    [Table("person_firma")]
    public class PersonFirma
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("person_id")]
        public int PersonId { get; set; }

        [Column("firma_tipo_id")]
        public int FirmaTipoId { get; set; }

        public FirmaTipo? FirmaTipo { get; set; }

        /// <summary>
        /// Siempre PNG. Lo que se sube en JPG/WEBP se normaliza a PNG antes de llegar acá: es el
        /// formato con el que el estampador del PDF respeta la transparencia.
        /// </summary>
        [Column("image_bytes")]
        public byte[] ImageBytes { get; set; } = null!;

        [Column("mime")]
        public string Mime { get; set; } = null!;

        [Column("created_date_time")]
        public DateTimeOffset CreatedDateTime { get; set; } = DateTimeOffset.UtcNow;

        [Column("created_user_id")]
        public int? CreatedUserId { get; set; }

        [Column("updated_date_time")]
        public DateTimeOffset? UpdatedDateTime { get; set; }

        [Column("updated_user_id")]
        public int? UpdatedUserId { get; set; }

        /// <summary>Soft delete: false = eliminada (se conserva para auditoría).</summary>
        [Column("state")]
        public bool State { get; set; } = true;
    }
}
