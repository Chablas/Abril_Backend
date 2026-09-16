using Abril_Backend.Features.LearningModule.Application.Dtos;

namespace Abril_Backend.Features.LearningModule.Infrastructure.Interfaces
{
    public interface ILearningRepository
    {
        // Display
        Task<List<LearningCategoryDto>> GetLoginCategories();
        Task<List<LearningCategoryDto>> GetInicioCategories(int[] roleIds);

        // Admin
        Task<LearningAdminDataDto> GetAdminData();

        Task<int> CreateCategory(LearningCategoryCreateDto dto);
        Task EditCategory(int id, LearningCategoryEditDto dto);
        Task<bool> ToggleCategory(int id);
        Task DeleteCategory(int id);

        /// <summary><paramref name="archivo"/> null = el video es el enlace del dto.</summary>
        Task<int> CreateVideo(LearningVideoCreateDto dto, LearningVideoArchivoDto? archivo);
        /// <summary>
        /// <paramref name="archivo"/> null = conserva el archivo actual (<c>EsArchivo</c>) o pasa a ser
        /// el enlace del dto.
        /// </summary>
        Task EditVideo(int id, LearningVideoEditDto dto, LearningVideoArchivoDto? archivo);
        Task<bool> ToggleVideo(int id);
        Task DeleteVideo(int id);

        /// <summary>Destino de un archivo nuevo en un grupo. Null si el grupo no existe.</summary>
        Task<LearningVideoDestinoDto?> GetDestinoPorCategoria(int categoriaId);
        /// <summary>Destino del archivo que reemplaza al de un video. Null si el video no existe.</summary>
        Task<LearningVideoDestinoDto?> GetDestinoPorVideo(int videoId);
    }
}
