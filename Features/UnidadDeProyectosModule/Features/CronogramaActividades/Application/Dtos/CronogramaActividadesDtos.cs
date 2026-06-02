namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos
{
    public class ProyectoSimpleCronogramaDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = string.Empty;
        public string? ResponsableUdp { get; set; }
        public int TotalActividades { get; set; }
        /// <summary>Avance del/los nodo(s) de nivel 0 (promedio recursivo de hijos; hoja = 100 si culminada, si no su progreso).</summary>
        public int Avance { get; set; }
    }

    public class ActividadDto
    {
        public int ProjectActivityId { get; set; }
        public int ProjectId { get; set; }
        public string ActivityDescription { get; set; } = string.Empty;
        public DateOnly? PlannedStartDate { get; set; }
        public DateOnly? PlannedEndDate { get; set; }
        public DateOnly? ActualEndDate { get; set; }
        public DateOnly? BaselineStartDate { get; set; }
        public DateOnly? BaselineEndDate { get; set; }
        public int ProgressPercentage { get; set; }
        public int Order { get; set; }
        public int HierarchyLevel { get; set; }
        public int? ParentId { get; set; }
        /// <summary>Ids de las actividades predecesoras (Fin a Inicio). Solo hojas participan.</summary>
        public List<int> Predecesoras { get; set; } = new();
        /// <summary>True si la actividad tiene hijos (es nodo padre, fechas calculadas).</summary>
        public bool EsPadre { get; set; }
    }

    public class ActualizarLineaBaseRequest
    {
        public DateOnly? BaselineStartDate { get; set; }
        public DateOnly? BaselineEndDate { get; set; }
    }

    public class CrearActividadRequest
    {
        public string ActivityDescription { get; set; } = string.Empty;
        public DateOnly? PlannedStartDate { get; set; }
        public DateOnly? PlannedEndDate { get; set; }
        public int ProgressPercentage { get; set; } = 0;
        public int HierarchyLevel { get; set; } = 0;
        public int? ParentId { get; set; }
    }

    public class ReordenarItem
    {
        public int ProjectActivityId { get; set; }
        public int Order { get; set; }
    }

    public class CambiarJerarquiaRequest
    {
        public int ProjectActivityId { get; set; }
        public int NuevoHierarchyLevel { get; set; }
        public int? NuevoParentId { get; set; }
    }

    public class EditarActividadRequest
    {
        public string ActivityDescription { get; set; } = string.Empty;
        public DateOnly? PlannedStartDate { get; set; }
        public DateOnly? PlannedEndDate { get; set; }
        public DateOnly? ActualEndDate { get; set; }
        public int ProgressPercentage { get; set; }
    }

    public class CulminarActividadDto
    {
        public int ProjectActivityId { get; set; }
        public DateOnly? ActualEndDate { get; set; }
        public int ProgressPercentage { get; set; }
    }

    public class DebugProyectoDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = string.Empty;
        public bool TieneUnidadDeProyectos { get; set; }
        public bool State { get; set; }
    }

    public class ImportarMppResultDto
    {
        public int ActividadesImportadas { get; set; }
        public int ActividadesEliminadas { get; set; }
    }

    public class DebugActividadOrdenDto
    {
        public int ProjectActivityId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Order { get; set; }
        public int? ParentId { get; set; }
        public int HierarchyLevel { get; set; }
    }

    // ─────────────────────────── Feriados ───────────────────────────

    public class FeriadoDto
    {
        public int Id { get; set; }
        public DateOnly Fecha { get; set; }
        public string? Descripcion { get; set; }
    }

    public class CrearFeriadoRequest
    {
        public DateOnly Fecha { get; set; }
        public string? Descripcion { get; set; }
    }

    // ─────────────────────────── Predecesoras ───────────────────────────

    /// <summary>Reemplaza por completo el conjunto de predecesoras de una actividad.</summary>
    public class ActualizarPredecesorasRequest
    {
        public List<int> PredecessorIds { get; set; } = new();
    }

    /// <summary>
    /// Resultado de fijar predecesoras: la lista resultante + el preview de
    /// la cascada que se aplicaría (sin persistir todavía).
    /// </summary>
    public class ActualizarPredecesorasResultDto
    {
        public int ProjectActivityId { get; set; }
        public List<int> Predecesoras { get; set; } = new();
        public CascadaResultDto PreviewCascada { get; set; } = new();
    }

    // ─────────────────────────── Cascada ───────────────────────────

    /// <summary>Una actividad que se movería (o se movió) al recalcular la cascada.</summary>
    public class CascadaCambioDto
    {
        public int ProjectActivityId { get; set; }
        public string ActivityDescription { get; set; } = string.Empty;
        public DateOnly? InicioAnterior { get; set; }
        public DateOnly? InicioNuevo { get; set; }
        public DateOnly? FinAnterior { get; set; }
        public DateOnly? FinNuevo { get; set; }
    }

    public class CascadaResultDto
    {
        public bool HayCambios { get; set; }
        public List<CascadaCambioDto> Cambios { get; set; } = new();
    }
}
