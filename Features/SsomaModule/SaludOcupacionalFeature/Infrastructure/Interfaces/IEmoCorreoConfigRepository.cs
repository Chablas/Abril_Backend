using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Configuracion;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces
{
    public interface IEmoCorreoConfigRepository
    {
        /// <summary>Ambas listas (Para y CC) en un solo roundtrip, para la pantalla de configuración.</summary>
        Task<EmoCorreosConfigDto> GetConfigAsync();

        Task<int> CreateAsync(string tipoCodigo, string email, string? nombre);

        Task UpdateAsync(int id, string email, string? nombre);

        Task SetActiveAsync(int id, bool active);

        /// <summary>Soft delete (state = false). No aplica a los destinatarios fijos.</summary>
        Task DeleteAsync(int id);

        /// <summary>
        /// Configuración resuelta para el envío de correos de programación de EMO.
        /// La consumen la programación manual y la automática.
        /// </summary>
        Task<EmoCorreoEnvioConfigDto> GetEnvioConfigAsync();
    }
}
