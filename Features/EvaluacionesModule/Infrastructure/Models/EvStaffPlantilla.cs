using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Models
{
    // Plantilla de criterios de la Evaluación 360° de Staff: criterios FUNCIONALES
    // (propios de cada puesto, ver Shared.Constants.PuestoIds.StaffEvaluablePuestoIds)
    // y TRANSVERSALES (idénticos para los 16 puestos evaluables). El Residente
    // evalúa a su staff contra la plantilla del puesto de cada evaluado.
    [Table("ev_staff_plantilla")]
    public class EvStaffPlantilla
    {
        public int Id { get; set; }

        [Column("puesto_id")]
        public int PuestoId { get; set; }

        public string Criterio { get; set; } = string.Empty;

        /// <summary>"FUNCIONAL" o "TRANSVERSAL" — ver EvStaffTipoCriterio.</summary>
        public string Tipo { get; set; } = string.Empty;

        public int Orden { get; set; }
        public bool Activo { get; set; } = true;
    }
}
