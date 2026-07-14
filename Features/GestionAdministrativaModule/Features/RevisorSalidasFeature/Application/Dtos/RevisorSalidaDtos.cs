using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Dtos
{
    /// <summary>
    /// Una fila por trabajador con email_corporativo @abril.pe, junto a sus n revisores
    /// (workers_revisores) ordenados por prioridad. El primer revisor vivo + activo con
    /// correo @abril.pe recibe las solicitudes de salida del trabajador; sin revisores
    /// válidos, la solicitud se envía al área de GTH (fallback).
    /// </summary>
    public class WorkerRevisorSalidaItemDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public int? CategoryId { get; set; }
        public string? Category { get; set; }
        /// <summary>Nodo area_scope al que pertenece el trabajador (para el filtro por área). null = sin área.</summary>
        public int? AreaScopeId { get; set; }
        /// <summary>Revisores vivos del trabajador, ordenados por prioridad ascendente.</summary>
        public List<WorkerRevisorAsignadoDto> Revisores { get; set; } = new();
    }

    /// <summary>Un revisor asignado a un trabajador (fila viva de workers_revisores).</summary>
    public class WorkerRevisorAsignadoDto
    {
        /// <summary>workers_revisores_id.</summary>
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
    public class WorkerRevisorSalidaOptionDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    /// <summary>Carga inicial de la página: tabla + opciones del selector + árbol de áreas (filtro).</summary>
    public class RevisorSalidaInicialDto
    {
        public List<WorkerRevisorSalidaItemDto> Workers { get; set; } = new();
        public List<WorkerRevisorSalidaOptionDto> Options { get; set; } = new();
        public List<GaAreaNodeDto> AreaTree { get; set; } = new();
    }

    /// <summary>Cuerpo del PUT: reemplaza el conjunto completo de revisores del trabajador.</summary>
    public class WorkerRevisoresUpdateDto
    {
        public List<WorkerRevisorAsignacionDto> Revisores { get; set; } = new();
    }

    /// <summary>Una asignación de revisor dentro del PUT.</summary>
    public class WorkerRevisorAsignacionDto
    {
        public int RevisorWorkerId { get; set; }
        public int OrdenPrioridad { get; set; }
        public bool Active { get; set; } = true;
    }
}
