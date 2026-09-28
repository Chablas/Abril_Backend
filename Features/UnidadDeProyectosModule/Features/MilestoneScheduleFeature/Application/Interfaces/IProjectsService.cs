using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces
{
    public interface IProjectsService
    {
        Task<PagedResult<MilestoneProjectDTO>> GetPagedWithResidents(int userId, int[] roleIds, bool esResidente, int page, int pageSize = 10, string? search = null);
        Task<string> UploadFotoAsync(int projectId, IFormFile foto, int userId, int[] roleIds, bool esResidente);
        Task<string?> UpdateLevelDescriptionAsync(int projectId, string? levelDescription, int userId, int[] roleIds, bool esResidente);
    }
}
