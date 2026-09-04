namespace Abril_Backend.Features.CostsModule.Shared.Models {
    /// <summary>
    /// Catálogo de los 9 pasos del flujo de adjudicaciones. Vive en el Shared del módulo
    /// (y no dentro de AdjudicacionesFeature) porque también lo usa la sección "Pasos" de
    /// Configuración, que agrupa sus opciones por este mismo catálogo.
    /// </summary>
    public class ProjectSubContractorStatus {
        public int ProjectSubContractorStatusId {get; set;}
        public string ProjectSubContractorStatusDescription {get; set;}
    }
}
