using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces
{
    public interface IProjectsRepository
    {
        Task<PagedResult<MilestoneProjectDTO>> GetPagedWithResidents(int userId, bool soloDelResidente, int page, int pageSize = 10, string? search = null);
        Task UpdateFotoUrlAsync(int projectId, string? fotoUrl, int userId);
        Task<string?> UpdateLevelDescriptionAsync(int projectId, string? levelDescription, int userId);
    }
}
