using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IAuthRepository
    {
        Task<UserDTO?> ValidateUserAsync(string email, string password);
        Task<UserSession> CreateSessionAsync(int userId);
    }
}