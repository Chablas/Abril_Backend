namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Dtos
{
    /// <summary>
    /// Una fila por área de tipo "Área Estándar" (solo el primer nodo estándar de cada
    /// rama del árbol area_scope: si un Área Estándar tiene como hijo otro Área Estándar,
    /// el hijo no se lista), junto a sus n revisores (area_revisores) ordenados por
    /// prioridad. Los revisores del área aplican a los trabajadores cuyo
    /// workers.area_scope_id cae en el subárbol del nodo, cuando el trabajador no tiene
    /// revisores propios en workers_revisores.
    /// </summary>
    public class AreaRevisorItemDto
    {
        public int AreaScopeId { get; set; }
        public string AreaName { get; set; } = string.Empty;
        /// <summary>Nombre del área padre (normalmente la gerencia). null = nodo raíz.</summary>
        public string? ParentName { get; set; }
        /// <summary>Revisores vivos del área, ordenados por prioridad ascendente.</summary>
        public List<AreaRevisorAsignadoDto> Revisores { get; set; } = new();
    }

    /// <summary>Un revisor asignado a un área (fila viva de area_revisores).</summary>
    public class AreaRevisorAsignadoDto
    {
        /// <summary>area_revisores_id.</summary>
        public int Id { get; set; }
        public int RevisorWorkerId { get; set; }
        public string? RevisorFullName { get; set; }
        public string? RevisorEmail { get; set; }
        public string? RevisorCategory { get; set; }
        /// <summary>1 = primero en ser considerado; a mayor número, menor prioridad.</summary>
        public int OrdenPrioridad { get; set; }
        /// <summary>false = no se considera (ej. ausencia temporal del revisor).</summary>
        public bool Active { get; set; }
    }

    /// <summary>Opción del selector de revisor: worker con correo corporativo @abril.pe.</summary>
    public class AreaRevisorOptionDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    /// <summary>Carga inicial de la página: áreas estándar con sus revisores + opciones del selector.</summary>
    public class AreaRevisorInicialDto
    {
        public List<AreaRevisorItemDto> Areas { get; set; } = new();
        public List<AreaRevisorOptionDto> Options { get; set; } = new();
    }

    /// <summary>Cuerpo del PUT: reemplaza el conjunto completo de revisores del área.</summary>
    public class AreaRevisoresUpdateDto
    {
        public List<AreaRevisorAsignacionDto> Revisores { get; set; } = new();
    }

    /// <summary>Una asignación de revisor dentro del PUT.</summary>
    public class AreaRevisorAsignacionDto
    {
        public int RevisorWorkerId { get; set; }
        public int OrdenPrioridad { get; set; }
        public bool Active { get; set; } = true;
    }
}
