namespace Abril_Backend.Features.GestionAdministrativa.DelegacionRevision.Application.Dtos
{
    /// <summary>
    /// Carga inicial de la funcionalidad "Delegación de Revisión" (usuario final).
    /// Lista las asignaciones (área, o área+obra, y el tipo de trabajador) en las que el usuario
    /// logueado figura como aprobador de la salida asignado a mano en Revisores de Áreas
    /// (area_actor_asignacion), para que pueda designar suplentes y activarse/desactivarse ("tomar/
    /// soltar el puesto").
    /// </summary>
    public class DelegacionInicialDto
    {
        /// <summary>workers.id del usuario logueado (0 si el usuario no tiene worker).</summary>
        public int CurrentWorkerId { get; set; }
        public List<DelegacionAsignacionItemDto> Asignaciones { get; set; } = new();
    }

    /// <summary>
    /// Una asignación que el usuario administra: un área (ProjectId null) o un proyecto dentro de
    /// un área "filtrada por proyecto" (ProjectId con valor), con su lista de revisores y las
    /// opciones de trabajadores que puede designar (los que pertenecen a esa área/proyecto).
    /// </summary>
    public class DelegacionAsignacionItemDto
    {
        public int AreaScopeId { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public string? ParentName { get; set; }
        /// <summary>null = asignación a nivel de área; con valor = asignación de ese proyecto.</summary>
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        /// <summary>A qué tipo de trabajador aplica (<c>ActorCasoIds</c>): oficina central, staff…</summary>
        public int CasoId { get; set; }
        public string CasoNombre { get; set; } = string.Empty;
        public List<DelegacionRevisorAsignadoDto> Revisores { get; set; } = new();
        /// <summary>Trabajadores designables (pertenecen al área/subárbol o al proyecto), con @abril.pe.</summary>
        public List<DelegacionOptionDto> Options { get; set; } = new();
    }

    /// <summary>Un revisor asignado (fila viva de area_actor_asignacion) mostrado en la delegación.</summary>
    public class DelegacionRevisorAsignadoDto
    {
        public int Id { get; set; }
        public int RevisorWorkerId { get; set; }
        public string? RevisorFullName { get; set; }
        public string? RevisorEmail { get; set; }
        public string? RevisorCategory { get; set; }
        public int OrdenPrioridad { get; set; }
        public bool Active { get; set; }
    }

    /// <summary>Opción del selector: worker con correo corporativo @abril.pe.</summary>
    public class DelegacionOptionDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    /// <summary>Cuerpo del PUT: reemplaza los revisores de una asignación (área o área+proyecto, y caso).</summary>
    public class DelegacionUpdateDto
    {
        public int? ProjectId { get; set; }
        /// <summary>Tipo de trabajador de la asignación (<c>ActorCasoIds</c>).</summary>
        public int CasoId { get; set; }
        public List<DelegacionAsignacionDto> Revisores { get; set; } = new();
    }

    /// <summary>Una asignación de revisor dentro del PUT.</summary>
    public class DelegacionAsignacionDto
    {
        public int RevisorWorkerId { get; set; }
        public int OrdenPrioridad { get; set; }
        public bool Active { get; set; } = true;
    }
}
