using Abril_Backend.Features.LearningModule.Application.Dtos;

namespace Abril_Backend.Features.LearningModule.Application.Interfaces
{
    public interface ILearningService
    {
        Task<List<LearningCategoryDto>> GetLoginCategories();
        Task<List<LearningCategoryDto>> GetInicioCategories(int[] roleIds);

        Task<LearningAdminDataDto> GetAdminData();

        Task<int> CreateCategory(LearningCategoryCreateDto dto);
        Task EditCategory(int id, LearningCategoryEditDto dto);
        Task<bool> ToggleCategory(int id);
        Task DeleteCategory(int id);

        /// <summary><paramref name="archivo"/> solo se usa si <c>dto.EsArchivo</c>.</summary>
        Task<int> CreateVideo(LearningVideoCreateDto dto, IFormFile? archivo);
        /// <summary>
        /// Con <c>dto.EsArchivo</c> y sin <paramref name="archivo"/> se conserva el archivo actual.
        /// </summary>
        Task EditVideo(int id, LearningVideoEditDto dto, IFormFile? archivo);
        Task<bool> ToggleVideo(int id);
        Task DeleteVideo(int id);
    }
}
