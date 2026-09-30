using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Interfaces
{
    public interface IProjectContractFolderService
    {
        Task<ProjectContractFolderDTO?> GetByProjectIdAsync(int projectId);
        Task<ProjectContractFolderDTO> SaveAsync(int projectId, ProjectContractFolderSaveDTO dto, int userId);
    }
}
