using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.AuthModule.UserFeature.Application.Dtos;
using UserModel = Abril_Backend.Infrastructure.Models.User;

namespace Abril_Backend.Features.AuthModule.UserFeature.Application.Interfaces
{
    public interface IUserFeatureRepository
    {
        Task<PagedResult<UserListItemDto>> GetPaged(int page, int pageSize);
        Task<UserModel> Create(UserFeatureCreateDto dto);
        Task Update(int userId, UserFeatureUpdateDto dto, int updatedUserId);
        Task ToggleActive(int userId, int updatedUserId);
    }
}
