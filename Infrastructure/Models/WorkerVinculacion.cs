using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Infrastructure.Models
{
    [Table("worker_vinculaciones")]
    public class WorkerVinculacion
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("worker_id")]
        public int WorkerId { get; set; }

        [Column("empresa_id")]
        public int? EmpresaId { get; set; }

        [Column("fecha_inicio")]
        public DateOnly FechaInicio { get; set; }

        [Column("fecha_fin")]
        public DateOnly? FechaFin { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("proyecto_id")]
        public int? ProyectoId { get; set; }

        [Column("puesto")]
        public string? Puesto { get; set; }

        [Column("tipo_vinculacion")]
        public string? TipoVinculacion { get; set; }

        [Column("motivo_retiro")]
        public string? MotivoRetiro { get; set; }

        [Column("registrado_por_id")]
        public int? RegistradoPorId { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [ForeignKey(nameof(WorkerId))]
        public Worker? Worker { get; set; }

        [ForeignKey(nameof(EmpresaId))]
        public Empresa? Empresa { get; set; }

        [ForeignKey(nameof(ProyectoId))]
        public Projects? Proyecto { get; set; }
    }
}
