using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Application.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponseDTO> Login(LoginDTO dto);
        Task ForgotPassword(ForgotPasswordDTO dto);
        Task ResetPassword(ResetPasswordDTO dto);
    }
}