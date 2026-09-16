using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.LearningModule.Application.Dtos;
using Abril_Backend.Features.LearningModule.Application.Interfaces;
using Abril_Backend.Features.LearningModule.Infrastructure.Interfaces;

namespace Abril_Backend.Features.LearningModule.Application.Services
{
    /// <summary>
    /// Orquesta el centro de aprendizaje (videos y manuales). Envoltorio delgado sobre el
    /// repositorio: la validación de datos vive en el repositorio (patrón de las demás
    /// features de configuración de este backend). Lo único propio es la subida de archivos
    /// a SharePoint.
    /// </summary>
    public class LearningService : ILearningService
    {
        private readonly ILearningRepository _repo;
        private readonly ILearningVideoStorage _storage;

        public LearningService(ILearningRepository repo, ILearningVideoStorage storage)
        {
            _repo = repo;
            _storage = storage;
        }

        public Task<List<LearningCategoryDto>> GetLoginCategories() => _repo.GetLoginCategories();
        public Task<List<LearningCategoryDto>> GetInicioCategories(int[] roleIds) => _repo.GetInicioCategories(roleIds);

        public Task<LearningAdminDataDto> GetAdminData() => _repo.GetAdminData();

        public Task<int> CreateCategory(LearningCategoryCreateDto dto) => _repo.CreateCategory(dto);
        public Task EditCategory(int id, LearningCategoryEditDto dto) => _repo.EditCategory(id, dto);
        public Task<bool> ToggleCategory(int id) => _repo.ToggleCategory(id);
        public Task DeleteCategory(int id) => _repo.DeleteCategory(id);

        public async Task<int> CreateVideo(LearningVideoCreateDto dto, IFormFile? archivo)
        {
            if (!dto.EsArchivo) return await _repo.CreateVideo(dto, null);

            // Lo que puede rechazar el alta se valida ANTES de subir, para no dejar en SharePoint un
            // archivo que ninguna fila usa.
            ValidarTitulo(dto.Titulo);
            if (archivo is null || archivo.Length == 0)
                throw new AbrilException("Selecciona el archivo.", 400);

            var destino = await _repo.GetDestinoPorCategoria(dto.CategoriaId)
                ?? throw new AbrilException("Grupo no encontrado.", 404);
            ValidarNoEsLogin(destino);

            var subido = await _storage.SubirAsync(archivo, destino.FolderLink);
            return await _repo.CreateVideo(dto, subido);
        }

        public async Task EditVideo(int id, LearningVideoEditDto dto, IFormFile? archivo)
        {
            // Enlace, o archivo sin reemplazo: no se sube nada y el repositorio valida el resto.
            if (!dto.EsArchivo || archivo is null || archivo.Length == 0)
            {
                await _repo.EditVideo(id, dto, null);
                return;
            }

            ValidarTitulo(dto.Titulo);
            var destino = await _repo.GetDestinoPorVideo(id)
                ?? throw new AbrilException("Video o manual no encontrado.", 404);
            ValidarNoEsLogin(destino);

            var subido = await _storage.SubirAsync(archivo, destino.FolderLink);
            await _repo.EditVideo(id, dto, subido);
        }

        public Task<bool> ToggleVideo(int id) => _repo.ToggleVideo(id);
        public Task DeleteVideo(int id) => _repo.DeleteVideo(id);

        private static void ValidarTitulo(string? titulo)
        {
            if (string.IsNullOrWhiteSpace(titulo))
                throw new AbrilException("El título no puede estar vacío.", 400);
        }

        /// <summary>
        /// El login es público y un archivo de SharePoint solo se abre con cuenta de Abril: ahí un
        /// contratista vería el inicio de sesión de Microsoft en vez del archivo.
        /// </summary>
        private static void ValidarNoEsLogin(LearningVideoDestinoDto destino)
        {
            if (destino.EsLogin)
                throw new AbrilException(
                    "Los grupos del login solo admiten enlaces: un archivo de SharePoint no se abre sin cuenta de Abril.", 400);
        }
    }
}
