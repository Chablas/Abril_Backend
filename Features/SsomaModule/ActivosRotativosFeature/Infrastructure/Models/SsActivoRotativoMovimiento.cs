using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models
{
    // Historial de traspasos de un activo rotativo entre proyectos (o hacia/desde almacén central).
    [Table("ss_activo_rotativo_movimiento")]
    public class SsActivoRotativoMovimiento
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("activo_id")]
        public int ActivoId { get; set; }

        // Null = almacén central
        [Column("proyecto_origen_id")]
        public int? ProyectoOrigenId { get; set; }

        [Column("proyecto_destino_id")]
        public int? ProyectoDestinoId { get; set; }

        [Column("fecha_movimiento")]
        public DateTimeOffset FechaMovimiento { get; set; }

        [Column("movido_por_id")]
        public int? MovidoPorId { get; set; }

        [Column("observacion")]
        public string? Observacion { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [ForeignKey(nameof(ActivoId))]
        public SsActivoRotativo? Activo { get; set; }

        [ForeignKey(nameof(ProyectoOrigenId))]
        public Project? ProyectoOrigen { get; set; }

        [ForeignKey(nameof(ProyectoDestinoId))]
        public Project? ProyectoDestino { get; set; }
    }
}
