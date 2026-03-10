using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IUserRegistrationTokenRepository
    {
        Task CreateAsync(UserRegistrationTokenDTO token);
        Task<UserRegistrationTokenDTO?> GetValidTokenAsync(string token);
        Task InvalidateTokensByUserAsync(int userId);
        Task SaveAsync();
    }
}