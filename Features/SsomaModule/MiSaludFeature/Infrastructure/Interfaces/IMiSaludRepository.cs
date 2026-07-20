using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.MiSaludFeature.Infrastructure.Interfaces
{
    public interface IMiSaludRepository
    {
        Task<int> ResolverWorkerIdAsync(int userId);
        Task<MiSaludResumenDto> GetResumen(int workerId);
        Task<PagedResult<MiDescansoDto>> GetDescansos(int workerId, int page);
        Task<int> CreateDescanso(int workerId, CrearMiDescansoDto dto, int? userId, List<(string Url, string Nombre)> adjuntos);
        Task<DescansoNotificacionDatosDto> GetDatosNotificacionDescansoAsync(int workerId, int userId, int? motivoId);

        // ── Configuración de correos de descanso médico ──
        Task<List<MiDescansoCorreoConfigDto>> GetCorreoConfigsAsync();
        /// <summary>codigo → active. Se usa al enviar el correo para respetar los toggles.</summary>
        Task<Dictionary<string, bool>> GetCorreoConfigMapAsync();
        /// <summary>Actualiza el flag active de un destinatario. Devuelve false si no existe.</summary>
        Task<bool> SetCorreoConfigActiveAsync(int id, bool active);
    }
}
