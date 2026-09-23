using System.ComponentModel.DataAnnotations;

namespace Abril_Backend.Features.LearningModule.Infrastructure.Models
{
    /// <summary>
    /// Carpeta única (singleton) de SharePoint donde se suben los videos del centro de aprendizaje
    /// (tabla <c>learning_video_folder</c>). Hoy apunta a la biblioteca «Manuales y videos» del sitio
    /// bibliotecanm. Se define por base de datos —no por appsettings— para cambiar el destino sin
    /// redeploy: basta con actualizar <c>link_url</c> de la fila vigente. Existe a lo sumo una fila
    /// vigente (<c>state = true</c>).
    ///
    /// Cambiar el link no mueve los videos ya subidos: cada uno guarda en <c>learning_video</c> el
    /// <c>drive_id</c> / <c>item_id</c> y el link con los que se subió. Solo decide dónde van los
    /// NUEVOS.
    /// </summary>
    public class LearningVideoFolder
    {
        [Key]
        public int LearningVideoFolderId { get; set; }

        /// <summary>Link de la carpeta de SharePoint (se resuelve a driveId/folderId al subir).</summary>
        public string LinkUrl { get; set; } = null!;

        /// <summary>Nombre legible de la carpeta (opcional, solo referencia).</summary>
        public string? FolderName { get; set; }

        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
