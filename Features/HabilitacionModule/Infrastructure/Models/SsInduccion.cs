using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Models
{
    [Table("ss_induccion")]
    public class SsInduccion
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public int ProyectoId { get; set; }
        public int EmpresaId { get; set; }
        public DateTime FechaProgramada { get; set; }
        public bool TrabajoAltura { get; set; } = false;
        public string Estado { get; set; } = "PROGRAMADA";
        public int? ProgramadoPor { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(WorkerId))]
        public Worker? Worker { get; set; }

        [ForeignKey(nameof(ProyectoId))]
        public Projects? Proyecto { get; set; }

        [ForeignKey(nameof(EmpresaId))]
        public SsEmpresaContratista? Empresa { get; set; }
    }
}
