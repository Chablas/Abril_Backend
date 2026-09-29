using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Infrastructure.Interfaces
{
    public interface IConvivirAuthRepository
    {
        /// <summary>Cuenta vigente por correo, sin distinguir mayúsculas.</summary>
        Task<ConvivirCuentaDto?> GetCuentaPorEmail(string email);

        Task<string?> GetNombres(int userId);

        /// <summary>Crea la sesión en <c>user_session</c> con el vencimiento indicado.</summary>
        Task<(string Token, DateTime ExpiresAt)> CrearSesion(int userId, DateTime expiresAt);

        Task RevocarSesion(string sessionToken);
    }
}
