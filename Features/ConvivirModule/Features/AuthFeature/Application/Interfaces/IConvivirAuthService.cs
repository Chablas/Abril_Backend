using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Interfaces
{
    public interface IConvivirAuthService
    {
        Task<ConvivirLoginResponseDto> Login(ConvivirLoginDto dto);
        Task<ConvivirRefreshResponseDto> Refresh(string sessionToken);
        Task Logout(string sessionToken);
        Task<ConvivirInvitacionDto> GetInvitacion(string token);
        Task<ConvivirLoginResponseDto> CrearContrasena(ConvivirCrearContrasenaDto dto);
        Task OlvideContrasena(ConvivirOlvideContrasenaDto dto);
        Task CambiarContrasena(int userId, ConvivirCambiarContrasenaDto dto);
        string PaginaAbrirApp(string token);
    }
}
