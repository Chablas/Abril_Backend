using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Infrastructure.Interfaces
{
    public interface IMicrosoftLoginRepository
    {
        Task<UserDTO?> GetUserByEmailAsync(string email);
    }
}
