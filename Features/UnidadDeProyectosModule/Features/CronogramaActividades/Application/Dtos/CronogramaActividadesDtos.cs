namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos
{
    public class ProyectoSimpleCronogramaDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = string.Empty;
        public string? ResponsableUdp { get; set; }
    }

    public class ActividadDto
    {
        public int ProjectActivityId { get; set; }
        public int ProjectId { get; set; }
        public string ActivityDescription { get; set; } = string.Empty;
        public DateOnly? PlannedStartDate { get; set; }
        public DateOnly? PlannedEndDate { get; set; }
        public DateOnly? ActualEndDate { get; set; }
        public int ProgressPercentage { get; set; }
        public int Order { get; set; }
        public int HierarchyLevel { get; set; }
        public int? ParentId { get; set; }
    }

    public class CrearActividadRequest
    {
        public string ActivityDescription { get; set; } = string.Empty;
        public DateOnly? PlannedStartDate { get; set; }
        public DateOnly? PlannedEndDate { get; set; }
        public int ProgressPercentage { get; set; } = 0;
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
}
