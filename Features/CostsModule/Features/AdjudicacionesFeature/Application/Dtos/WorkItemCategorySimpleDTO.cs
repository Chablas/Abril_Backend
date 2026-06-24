namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos {
    public class WorkItemCategorySimpleDTO {
        public int WorkItemCategoryId {get; set;}
        public string? WorkItemCategoryDescription {get; set;}
        public int? InstructivosSyncStatus {get; set;}
        /// <summary>Nombre del instructivo asociado (carpeta sincronizada en OneDrive), si existe.</summary>
        public string? InstructivosFolderName {get; set;}
    }
}
