namespace Abril_Backend.Features.LearningModule.Infrastructure.Models
{
    /// <summary>
    /// Video o manual de una categoría: un enlace (Loom/YouTube/etc., normalmente un video) o un
    /// archivo subido a la carpeta de SharePoint de <c>learning_video_folder</c> (normalmente un
    /// manual en PDF o imagen). Hereda de su categoría la superficie (login/inicio) y la visibilidad
    /// por rol. La tabla conserva el nombre de cuando solo había videos.
    /// </summary>
    public class LearningVideo
    {
        public int LearningVideoId { get; set; }
        public int LearningCategoryId { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Lo que abre la tarjeta. Si es un archivo, el link que se armó al subirlo: el del
        /// reproductor de Stream para un video y el del propio archivo para un PDF o una imagen.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Nombre original del archivo subido. Null = es un enlace.</summary>
        public string? FileName { get; set; }

        /// <summary>Drive de SharePoint del archivo subido. Null = es un enlace.</summary>
        public string? DriveId { get; set; }

        /// <summary>Item de SharePoint del archivo subido. Null = es un enlace.</summary>
        public string? ItemId { get; set; }

        /// <summary>Miniatura opcional; si es null el front muestra un ícono genérico.</summary>
        public string? ThumbnailUrl { get; set; }

        /// <summary>Orden de aparición dentro de su categoría (menor primero).</summary>
        public int DisplayOrder { get; set; }

        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;

        public DateTimeOffset CreatedDateTime { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }

        public LearningCategory? Category { get; set; }
    }
}
