namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Dtos
{
    public class WorkItemEditDto
    {
        public int WorkItemId { get; set; }
        public string WorkItemDescription { get; set; } = null!;
        /// <summary>Especialidad a asignar (opcional; null = sin especialidad).</summary>
        public int? WorkSpecialtyId { get; set; }
        public bool Active { get; set; }
    }
}
