using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Application.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponseDTO> Login(LoginDTO dto);
        Task SetPassword(SetPasswordDTO dto);
        Task ForgotPassword(ForgotPasswordDTO dto);
    }
}