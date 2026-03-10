using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IUserRepository
    {
        Task<List<UserFilterDTO>> GetAllUsersFactory();
        Task<List<UserPersonFilterDTO>> GetAllFilterFactory();
        Task<PagedResult<UserDTO>> GetPagedFactory(int page, int pageSizeQuery);
        Task<User?> Create(UserCreateDTO dto);
        Task SetPassword(int userId, string plainPassword);
        Task<List<UserFilterDTO>> GetResidentsFullName();
    }
}