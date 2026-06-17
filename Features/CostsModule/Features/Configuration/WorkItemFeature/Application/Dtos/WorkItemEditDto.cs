namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Dtos
{
    public class WorkItemEditDto
    {
        public int WorkItemId { get; set; }
        public string WorkItemDescription { get; set; } = null!;
        /// <summary>Especialidad a asignar (opcional; null = sin especialidad).</summary>
        public int? WorkSpecialtyId { get; set; }
        public bool Active { get; set; }

        /// <summary>Formas de valorización (cláusula 5.1) a guardar (upsert completo).</summary>
        public List<WorkItemValorizationFormUpsertDto> ValorizationForms { get; set; } = [];
    }

    public class WorkItemValorizationFormUpsertDto
    {
        /// <summary>null = nueva; con valor = actualizar la existente.</summary>
        public int?    WorkItemValorizationFormId { get; set; }
        public string  Concept                    { get; set; } = null!;
        public decimal Percentage                 { get; set; }
        public int     SortOrder                  { get; set; }
    }
}
