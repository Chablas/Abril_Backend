using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftProfile.Application.Dtos;

namespace Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Infrastructure.Interfaces
{
    public interface IMicrosoftLoginRepository
    {
        Task<UserDTO?> GetUserByEmailAsync(string email);
        Task<UserDTO> CreateUserFromGraphAsync(MicrosoftProfileDto profile);
    }
}
