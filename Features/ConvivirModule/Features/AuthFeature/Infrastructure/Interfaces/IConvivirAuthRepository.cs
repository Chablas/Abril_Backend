using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Infrastructure.Interfaces
{
    public interface IConvivirAuthRepository
    {
        /// <summary>Cuenta vigente de la persona con ese DNI (activa o no).</summary>
        Task<ConvivirCuentaDto?> GetCuentaPorDni(string dni);

        /// <summary>Nombres y DNI de la persona del usuario.</summary>
        Task<(string? Nombres, string? Dni)> GetPersona(int userId);

        /// <summary>Crea la sesión en <c>user_session</c> con el vencimiento indicado.</summary>
        Task<(string Token, DateTime ExpiresAt)> CrearSesion(int userId, DateTime expiresAt);

        Task RevocarSesion(string sessionToken);

        /// <summary>
        /// Guarda la nueva contraseña y, en la misma transacción, revoca las demás sesiones del
        /// usuario (menos <paramref name="sessionTokenQueSeQueda"/>) y anula los enlaces de
        /// «crear contraseña» sin usar.
        /// </summary>
        Task CambiarContrasena(int userId, string nuevaPassword, string sessionTokenQueSeQueda);
    }
}
