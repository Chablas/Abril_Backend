using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Repositories
{
    public class ProjectContractFolderRepository : IProjectContractFolderRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ProjectContractFolderRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<ProjectContractFolderDTO?> GetByProjectIdAsync(int projectId)
        {
            using var ctx = _factory.CreateDbContext();

            return await ctx.ProjectContractFolder
                .Where(f => f.ProjectId == projectId && f.State)
                .Select(f => new ProjectContractFolderDTO
                {
                    ProjectContractFolderId = f.ProjectContractFolderId,
                    ProjectId = f.ProjectId,
                    LinkUrl = f.LinkUrl,
                    FolderName = f.FolderName,
                    WebUrl = f.WebUrl
                })
                .FirstOrDefaultAsync();
        }

        public async Task UpsertAsync(
            int projectId, string linkUrl, string driveId, string folderId,
            string? folderName, string? webUrl, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var existente = await ctx.ProjectContractFolder
                .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.State);

            if (existente != null)
            {
                existente.LinkUrl = linkUrl;
                existente.DriveId = driveId;
                existente.FolderId = folderId;
                existente.FolderName = folderName;
                existente.WebUrl = webUrl;
                existente.UpdatedDateTime = DateTime.UtcNow;
                existente.UpdatedUserId = userId;
            }
            else
            {
                ctx.ProjectContractFolder.Add(new ProjectContractFolder
                {
                    ProjectId = projectId,
                    LinkUrl = linkUrl,
                    DriveId = driveId,
                    FolderId = folderId,
                    FolderName = folderName,
                    WebUrl = webUrl,
                    Active = true,
                    State = true,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = userId
                });
            }

            await ctx.SaveChangesAsync();
        }

        public async Task<(string DriveId, string FolderId)?> GetResolvedFolderAsync(int projectId)
        {
            using var ctx = _factory.CreateDbContext();

            var folder = await ctx.ProjectContractFolder
                .Where(f => f.ProjectId == projectId && f.State)
                .Select(f => new { f.DriveId, f.FolderId })
                .FirstOrDefaultAsync();

            return folder == null ? null : (folder.DriveId, folder.FolderId);
        }
    }
}
