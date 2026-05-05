using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.AuthModule.MicrosoftProfile.Application.Dtos;

namespace Abril_Backend.Features.AuthModule.MicrosoftLogin.Infrastructure.Interfaces
{
    public interface IMicrosoftLoginRepository
    {
        Task<UserDTO?> GetUserByEmailAsync(string email);
        Task<UserDTO> CreateUserFromGraphAsync(MicrosoftProfileDto profile);
        Task<PersonDTO> CreatePersonForUserAsync(int userId, MicrosoftProfileDto profile);
    }
}
