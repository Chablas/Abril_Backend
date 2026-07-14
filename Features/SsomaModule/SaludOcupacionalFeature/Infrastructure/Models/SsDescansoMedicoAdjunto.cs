using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models
{
    [Table("ss_descanso_medico_adjunto")]
    public class SsDescansoMedicoAdjunto
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("descanso_id")]
        public int DescansoId { get; set; }

        [Column("url")]
        public string Url { get; set; } = string.Empty;

        [Column("nombre_archivo")]
        public string? NombreArchivo { get; set; }

        [Column("state")]
        public bool State { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [ForeignKey(nameof(DescansoId))]
        public SsDescansoMedico? Descanso { get; set; }
    }
}
