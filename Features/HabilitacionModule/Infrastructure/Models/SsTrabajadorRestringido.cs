using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Models
{
    [Table("ss_trabajador_restringido")]
    public class SsTrabajadorRestringido
    {
        public int Id { get; set; }
        public string? Dni { get; set; }
        public int? WorkerId { get; set; }
        public string? ApellidoNombre { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? ProyectoOrigen { get; set; }
        public string? RestringidoPor { get; set; }
        public DateOnly? FechaRestriccion { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(WorkerId))]
        public Worker? Worker { get; set; }
    }
}
