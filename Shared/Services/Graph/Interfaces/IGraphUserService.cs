using Abril_Backend.Shared.Services.Graph.Dtos;

namespace Abril_Backend.Shared.Services.Graph.Interfaces
{
    public interface IGraphUserService
    {
        /// <summary>
        /// Obtiene los perfiles de Graph de una lista de emails en una sola consulta (o en chunks de 15).
        /// Usa permiso de aplicación (client credentials) — no requiere token del usuario.
        /// Retorna un diccionario email → perfil para lookup O(1).
        /// Emails que no existan en el tenant simplemente no aparecerán en el resultado.
        /// </summary>
        Task<Dictionary<string, GraphUserProfileDto>> GetUsersByEmailsAsync(List<string> emails);
    }
}
