using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models
{
    [Table("ss_topico_atencion")]
    public class TopicoAtencion
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("worker_id")]
        public int WorkerId { get; set; }

        [Column("fecha")]
        public DateOnly Fecha { get; set; }

        [Column("hora")]
        public TimeOnly Hora { get; set; }

        [Column("tipo_atencion")]
        public string TipoAtencion { get; set; } = string.Empty;

        [Column("motivo")]
        public string? Motivo { get; set; }

        [Column("diagnostico")]
        public string? Diagnostico { get; set; }

        [Column("diagnostico_cie10")]
        public string? DiagnosticoCie10 { get; set; }

        [Column("tratamiento")]
        public string? Tratamiento { get; set; }

        [Column("medicamentos")]
        public string? Medicamentos { get; set; }

        [Column("presion_arterial")]
        public string? PresionArterial { get; set; }

        [Column("temperatura")]
        public decimal? Temperatura { get; set; }

        [Column("frecuencia_cardiaca")]
        public int? FrecuenciaCardiaca { get; set; }

        [Column("saturacion_oxigeno")]
        public int? SaturacionOxigeno { get; set; }

        [Column("peso")]
        public decimal? Peso { get; set; }

        [Column("derivado_clinica")]
        public bool DerivadoClinica { get; set; } = false;

        [Column("clinica_derivacion")]
        public string? ClinicaDerivacion { get; set; }

        [Column("genera_descanso")]
        public bool GeneraDescanso { get; set; } = false;

        [Column("descanso_dias")]
        public int? DescansoDias { get; set; }

        [Column("genera_accidente")]
        public bool GeneraAccidente { get; set; } = false;

        [Column("accidente_id")]
        public int? AccidenteId { get; set; }

        [Column("proyecto_id")]
        public int? ProyectoId { get; set; }

        [Column("empresa_id")]
        public int? EmpresaId { get; set; }

        [Column("observaciones")]
        public string? Observaciones { get; set; }

        [Column("registrado_por_id")]
        public int RegistradoPorId { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [ForeignKey(nameof(WorkerId))]
        public Worker? Worker { get; set; }
    }
}
