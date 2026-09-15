using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Models
{
    /// <summary>
    /// La pareja de proyectos que le toca inspeccionarse en un mes concreto:
    /// "en {Mes}/{Anio}, {ProyectoInspectorId} inspecciona a {ProyectoInspeccionadoId}".
    /// Se genera automáticamente por la rotación (ver
    /// <see cref="InspeccionCruzadaProgramacionService.GenerarProgramacionAsync"/>) pero puede
    /// reasignarse a mano para un mes puntual sin afectar el avance del anillo para los demás
    /// meses.
    /// </summary>
    [Table("ss_inspeccion_cruzada_programacion")]
    public class SsInspeccionCruzadaProgramacion
    {
        public int Id { get; set; }
        public int Anio { get; set; }
        public int Mes { get; set; }
        public int AnilloId { get; set; }
        public int ProyectoInspectorId { get; set; }
        public int ProyectoInspeccionadoId { get; set; }

        /// <summary>true si esta pareja fue tocada a mano (reasignación) en vez de venir de la
        /// generación automática de la rotación.</summary>
        public bool EsManual { get; set; }
        public string? MotivoCambio { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(ProyectoInspectorId))]
        public Project? ProyectoInspector { get; set; }

        [ForeignKey(nameof(ProyectoInspeccionadoId))]
        public Project? ProyectoInspeccionado { get; set; }
    }
}
