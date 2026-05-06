using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.AuthModule.Role.Application.Dtos;

namespace Abril_Backend.Features.AuthModule.Role.Infrastructure.Interfaces
{
    public interface IRoleFeatureRepository
    {
        Task<PagedResult<RoleDto>> GetPaged(int page, int pageSize);
        Task Create(RoleCreateDto dto, int userId);
    }
}
