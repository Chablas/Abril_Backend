using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// Cómo se registra una firma personal: dibujándola con el mouse o subiendo una imagen.
    ///
    /// Es un catálogo y no dos columnas booleanas en `person` porque una persona puede tener
    /// registrada una firma POR CADA TIPO (ver <see cref="PersonFirma"/>), y un tipo nuevo tiene
    /// que ser una fila, no tres columnas más.
    /// </summary>
    [Table("firma_tipo")]
    public class FirmaTipo
    {
        /// <summary>Firma dibujada en el canvas. Es la única que existía antes de setiembre de 2026.</summary>
        public const string CodigoDibujo = "DIBUJO";

        /// <summary>Firma subida como archivo de imagen (se normaliza a PNG al guardarla).</summary>
        public const string CodigoImagen = "IMAGEN";

        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>DIBUJO | IMAGEN. Es lo que referencia el código; el id se resuelve por acá.</summary>
        [Column("codigo")]
        public string Codigo { get; set; } = null!;

        /// <summary>Texto que ve el usuario.</summary>
        [Column("nombre")]
        public string Nombre { get; set; } = null!;

        /// <summary>
        /// El tipo se ofrece al firmar. Son los dos checkboxes de Consolidados → Configuración →
        /// Firmas: al aprobar un consolidado se exige tener registrada una firma de alguno de los
        /// tipos activos, y el modal que salta en ese momento y Mi Perfil → Mi Firma ofrecen solo los
        /// activos. Contabilidad → Firma sigue ofreciendo solo el dibujo, así que desactivar un tipo
        /// no le cambia nada.
        /// </summary>
        [Column("active")]
        public bool Active { get; set; } = true;

        [Column("created_date_time")]
        public DateTimeOffset CreatedDateTime { get; set; } = DateTimeOffset.UtcNow;

        [Column("created_user_id")]
        public int? CreatedUserId { get; set; }

        [Column("updated_date_time")]
        public DateTimeOffset? UpdatedDateTime { get; set; }

        [Column("updated_user_id")]
        public int? UpdatedUserId { get; set; }

        /// <summary>Soft delete: false = eliminado (se conserva para auditoría).</summary>
        [Column("state")]
        public bool State { get; set; } = true;
    }
}
