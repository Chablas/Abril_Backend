using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models
{
    [Table("ss_descanso_seguimiento")]
    public class SsDescansoSeguimiento
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("descanso_id")]
        public int DescansoId { get; set; }

        [Column("fecha_seguimiento")]
        public DateTimeOffset FechaSeguimiento { get; set; }

        [Column("tipo")]
        public string Tipo { get; set; } = string.Empty;

        [Column("realizado_por_rol")]
        public string? RealizadoPorRol { get; set; }

        [Column("realizado_por_id")]
        public int? RealizadoPorId { get; set; }

        [Column("nota")]
        public string? Nota { get; set; }

        [Column("proxima_cita")]
        public DateOnly? ProximaCita { get; set; }

        [Column("url_evidencia")]
        public string? UrlEvidencia { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("state")]
        public bool State { get; set; } = true;

        [ForeignKey(nameof(DescansoId))]
        public SsDescansoMedico? Descanso { get; set; }
    }
}
