using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Application.Interfaces
{
    public interface IUserService
    {
        Task<PagedResult<UserDTO>> GetPagedFactory(int page, int pageSize);
        Task Create(UserCreateDTO dto);
        Task CompleteRegistration(CompleteRegistrationDTO dto);
    }
}