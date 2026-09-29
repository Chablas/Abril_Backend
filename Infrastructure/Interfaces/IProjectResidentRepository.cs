using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Models;
namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IProjectResidentRepository
    {
        Task<List<ProjectSimpleDTO>> GetProjectsDescription();
        Task<List<ProjectSimpleDTO>> GetProjectByResidentUserId(int userId);
    }
}