namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Models {
    /// <summary>Escaneo del contrato ya firmado (paso 7) — hasta 3 slots, mismo mecanismo que
    /// ProjectSubContractorScannedDoc en Adjudicaciones. La tabla queda lista, pero la subida real
    /// del archivo se conecta cuando se resuelva el almacenamiento (pendiente — ver CONTEXT.md).</summary>
    public class ProjectContractScannedDoc {
        public int ProjectContractScannedDocId {get; set;}
        public int ProjectContractId {get; set;}
        public int Slot {get; set;}
        public string? FileUrl {get; set;}
        public string? OriginalFileName {get; set;}
        public string? StorageItemId {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
    }
}
