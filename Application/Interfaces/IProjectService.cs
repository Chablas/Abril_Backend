using Abril_Backend.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Application.Interfaces
{
    public interface IProjectService
    {
        Task<PagedResult<ProjectDTO>> GetPagedWithResidents(int page, int pageSize = 10, string? search = null);
        Task<string> UploadFotoAsync(int projectId, IFormFile foto);
    }
}