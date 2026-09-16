using Abril_Backend.Features.LearningModule.Application.Dtos;

namespace Abril_Backend.Features.LearningModule.Application.Interfaces
{
    /// <summary>
    /// Sube los videos y manuales del centro de aprendizaje a la carpeta de SharePoint configurada
    /// en <c>learning_video_folder</c>.
    /// </summary>
    public interface ILearningVideoStorage
    {
        /// <summary>
        /// Valida el archivo (formato y peso), lo sube a la carpeta de <paramref name="folderLink"/> y
        /// devuelve dónde quedó, con el link listo para guardar como url: el del reproductor de Stream
        /// si es un video y el del propio archivo si es un PDF o una imagen.
        /// </summary>
        Task<LearningVideoArchivoDto> SubirAsync(IFormFile archivo, string? folderLink);
    }
}
