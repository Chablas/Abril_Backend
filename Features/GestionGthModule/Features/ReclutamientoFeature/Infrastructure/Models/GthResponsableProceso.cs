using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Catálogo de miembros del equipo GTH que pueden ser "Responsable del proceso" de un
    /// requerimiento (tabla <c>gth_responsable_proceso</c>). Cada fila apunta a un trabajador
    /// de la base maestra (<c>workers</c>); el nombre se resuelve vía <c>person.full_name</c>.
    /// Normalizado a tabla en vez de nombres en texto plano (regla de normalización del proyecto).
    /// </summary>
    public class GthResponsableProceso
    {
        public int GthResponsableProcesoId { get; set; }

        /// <summary>FK a <c>workers.id</c> (miembro del equipo GTH).</summary>
        public int WorkerId { get; set; }
        public Worker? Worker { get; set; }

        public int Orden { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
