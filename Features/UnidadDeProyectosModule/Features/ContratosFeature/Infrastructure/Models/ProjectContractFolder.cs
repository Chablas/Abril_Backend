namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Models {
    /// <summary>Carpeta de SharePoint/OneDrive donde se guardan los documentos de Contratos de un
    /// proyecto — un registro vivo por proyecto (a diferencia de Adjudicaciones, acá no hay split
    /// 04_OBRAS/07_OT porque no tiene un equivalente claro en el flujo de diseño; si más adelante
    /// hace falta separar visibilidad, se puede agregar un FolderTypeId igual que allá).</summary>
    public class ProjectContractFolder {
        public int ProjectContractFolderId {get; set;}
        public int ProjectId {get; set;}
        /// <summary>Link original pegado por el usuario (para mostrar/reabrir).</summary>
        public string LinkUrl {get; set;} = null!;
        /// <summary>Ubicación estable resuelta vía Graph Shares API.</summary>
        public string DriveId {get; set;} = null!;
        public string FolderId {get; set;} = null!;
        public string? FolderName {get; set;}
        public string? WebUrl {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
    }
}
