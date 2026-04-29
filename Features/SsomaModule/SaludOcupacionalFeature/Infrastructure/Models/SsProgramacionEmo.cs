using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models
{
    [Table("ss_programacion_emos")]
    public class SsProgramacionEmo
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("worker_id")]
        public int WorkerId { get; set; }

        [Column("empresa_id")]
        public int? EmpresaId { get; set; }

        [Column("tipo_emo_id")]
        public int TipoEmoId { get; set; }

        [Column("fecha_programada")]
        public DateOnly FechaProgramada { get; set; }

        [Column("hora_programada")]
        public TimeOnly? HoraProgramada { get; set; }

        [Column("clinica_id")]
        public int? ClinicaId { get; set; }

        [Column("medico_id")]
        public int? MedicoId { get; set; }

        [Column("motivo")]
        public string? Motivo { get; set; }

        [Column("estado")]
        public string Estado { get; set; } = "Programado";

        [Column("emo_resultado_id")]
        public int? EmoResultadoId { get; set; }

        [Column("notas")]
        public string? Notas { get; set; }

        [Column("registrado_por_id")]
        public int? RegistradoPorId { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [ForeignKey(nameof(WorkerId))]
        public Worker? Worker { get; set; }

        [ForeignKey(nameof(TipoEmoId))]
        public SsEmoTipo? TipoEmo { get; set; }

        [ForeignKey(nameof(ClinicaId))]
        public SsClinica? Clinica { get; set; }

        [ForeignKey(nameof(MedicoId))]
        public SsMedicoOcupacional? Medico { get; set; }

        [ForeignKey(nameof(EmpresaId))]
        public Empresa? Empresa { get; set; }

        [ForeignKey(nameof(EmoResultadoId))]
        public WorkerEmo? EmoResultado { get; set; }
    }
}
