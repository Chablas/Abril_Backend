using System.ComponentModel.DataAnnotations;

namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// project_ciclo_vida — en qué parte de su vida está el proyecto: activo, finalizado o inactivo.
    /// Es la única fuente: reemplazó a <c>project.estado</c>, <c>project.activo</c> y
    /// <c>project.operativo</c>, que se contradecían.
    ///
    /// No confundir con <c>project.active</c>, que es una columna de sistema (si el proyecto aparece
    /// en filtros y desplegables), ni con <c>project.state</c> (baja lógica).
    ///
    /// Los ids son fijos (los inserta <c>20260928_ProyectosTipoYCicloVida.sql</c>): ver
    /// <see cref="Constants.ProjectCicloVidaIds"/>.
    /// </summary>
    public class ProjectCicloVida
    {
        [Key]
        public int ProjectCicloVidaId { get; set; }

        /// <summary>ACTIVO, FINALIZADO o INACTIVO.</summary>
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        /// <summary>Qué significa, para mostrarlo junto al desplegable.</summary>
        public string? Descripcion { get; set; }
        public int Orden { get; set; }

        public DateTime CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
