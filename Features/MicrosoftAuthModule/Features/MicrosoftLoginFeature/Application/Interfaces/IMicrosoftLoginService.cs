using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Application.Dtos;

namespace Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Application.Interfaces
{
    public interface IMicrosoftLoginService
    {
        Task<MicrosoftLoginResponseDto> Login(string graphAccessToken);
    }
}
