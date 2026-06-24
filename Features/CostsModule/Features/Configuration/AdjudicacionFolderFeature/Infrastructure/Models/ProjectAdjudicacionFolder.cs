namespace Abril_Backend.Features.CostsModule.Features.Configuration.AdjudicacionFolderFeature.Infrastructure.Models
{
    /// <summary>
    /// Carpeta de OneDrive/SharePoint donde se guardan los documentos de adjudicaciones
    /// de un proyecto. Un registro vivo por proyecto.
    /// </summary>
    public class ProjectAdjudicacionFolder
    {
        public int ProjectAdjudicacionFolderId { get; set; }
        public int ProjectId { get; set; }
        /// <summary>Link original pegado por el usuario (para mostrar/reabrir).</summary>
        public string LinkUrl { get; set; } = null!;
        /// <summary>Ubicación estable resuelta vía Graph Shares API.</summary>
        public string DriveId { get; set; } = null!;
        public string FolderId { get; set; } = null!;
        public string? FolderName { get; set; }
        public string? WebUrl { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
