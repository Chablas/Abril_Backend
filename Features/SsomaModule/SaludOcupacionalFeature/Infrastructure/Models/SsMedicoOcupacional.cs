using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models
{
    [Table("ss_medicos_ocupacionales")]
    public class SsMedicoOcupacional
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("apellido_nombre")]
        public string ApellidoNombre { get; set; } = string.Empty;

        [Column("cmp")]
        public string? Cmp { get; set; }

        [Column("especialidad")]
        public string? Especialidad { get; set; }

        [Column("clinica_id")]
        public int? ClinicaId { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("celular")]
        public string? Celular { get; set; }

        [Column("activo")]
        public bool Activo { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [ForeignKey(nameof(ClinicaId))]
        public SsClinica? Clinica { get; set; }
    }
}
