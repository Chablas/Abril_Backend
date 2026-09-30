using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Interfaces
{
    public interface IProjectContractFolderRepository
    {
        Task<ProjectContractFolderDTO?> GetByProjectIdAsync(int projectId);
        Task UpsertAsync(int projectId, string linkUrl, string driveId, string folderId, string? folderName, string? webUrl, int userId);

        /// <summary>DriveId/FolderId resueltos, para uso interno de ProjectContractStorage — no
        /// se expone al frontend (a diferencia de GetByProjectIdAsync, que sí es un DTO de UI).</summary>
        Task<(string DriveId, string FolderId)?> GetResolvedFolderAsync(int projectId);
    }
}
