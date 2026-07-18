using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models
{
    [Table("ss_descanso_medico")]
    public class SsDescansoMedico
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("worker_id")]
        public int WorkerId { get; set; }

        [Column("tipo")]
        public string Tipo { get; set; } = string.Empty;

        [Column("fecha_inicio")]
        public DateOnly FechaInicio { get; set; }

        [Column("fecha_fin")]
        public DateOnly FechaFin { get; set; }

        [Column("diagnostico")]
        public string? Diagnostico { get; set; }

        [Column("diagnostico_cie10")]
        public string? DiagnosticoCie10 { get; set; }

        [Column("medico_certifica")]
        public string? MedicoCertifica { get; set; }

        [Column("establecimiento")]
        public string? Establecimiento { get; set; }

        [Column("url_certificado")]
        public string? UrlCertificado { get; set; }

        [Column("estado")]
        public string Estado { get; set; } = "Pendiente";

        [Column("motivo_rechazo")]
        public string? MotivoRechazo { get; set; }

        [Column("aprobado_por_id")]
        public int? AprobadoPorId { get; set; }

        [Column("fecha_aprobacion")]
        public DateTimeOffset? FechaAprobacion { get; set; }

        [Column("accidente_id")]
        public int? AccidenteId { get; set; }

        [Column("es_recaida")]
        public bool EsRecaida { get; set; } = false;

        [Column("proyecto_id")]
        public int? ProyectoId { get; set; }

        [Column("empresa_id")]
        public int? EmpresaId { get; set; }

        [Column("notificado_gth")]
        public bool NotificadoGth { get; set; } = false;

        [Column("notificado_jefe")]
        public bool NotificadoJefe { get; set; } = false;

        [Column("reportado_por_trabajador")]
        public bool ReportadoPorTrabajador { get; set; } = false;

        [Column("observaciones")]
        public string? Observaciones { get; set; }

        [Column("dias")]
        public int Dias { get; set; }

        [Column("motivo")]
        public string? Motivo { get; set; }

        [Column("motivo_id")]
        public int? MotivoId { get; set; }

        [Column("tipo_id")]
        public int? TipoId { get; set; }

        [Column("url_documento")]
        public string? UrlDocumento { get; set; }

        [Column("topico_origen_id")]
        public int? TopicoOrigenId { get; set; }

        [Column("prorroga_del_id")]
        public int? ProrrogaDelId { get; set; }

        [Column("fecha_alta")]
        public DateOnly? FechaAlta { get; set; }

        [Column("alta_por_id")]
        public int? AltaPorId { get; set; }

        [Column("alta_observaciones")]
        public string? AltaObservaciones { get; set; }

        [Column("state")]
        public bool State { get; set; } = true;

        [Column("registrado_por_id")]
        public int RegistradoPorId { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [ForeignKey(nameof(WorkerId))]
        public Worker? Worker { get; set; }

        [ForeignKey(nameof(MotivoId))]
        public SsDescansoMotivo? MotivoCatalogo { get; set; }

        [ForeignKey(nameof(TipoId))]
        public SsDescansoTipo? TipoCatalogo { get; set; }

        [ForeignKey(nameof(AccidenteId))]
        public SsAccidenteTrabajo? Accidente { get; set; }

        [ForeignKey(nameof(TopicoOrigenId))]
        public TopicoAtencion? TopicoOrigen { get; set; }
    }
}
