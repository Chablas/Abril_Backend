namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Dtos
{
    public class WorkItemCreateDto
    {
        public string WorkItemDescription { get; set; } = null!;
        /// <summary>Especialidad a asignar (opcional; null = sin especialidad).</summary>
        public int? WorkSpecialtyId { get; set; }
    }
}
