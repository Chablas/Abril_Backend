using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces
{
    public interface ISolicitudSalidaService
    {
        Task<SolicitudSalidaFormDataDto> GetFormData(int? userId);
        Task<List<SolicitudSalidaListItemDto>> GetByUserId(int userId);
        Task<int> Create(SolicitudSalidaCreateDto dto, int? userId);

        /// <summary>Aprueba una solicitud usando el token firmado del email. Devuelve HTML para mostrar al usuario.</summary>
        Task<string> ProcessAprobarFromEmail(string token);

        /// <summary>Rechaza una solicitud usando el token firmado del email + motivo. Devuelve HTML.</summary>
        Task<string> ProcessRechazarFromEmail(string token, string? motivoRechazo);

        /// <summary>Renderiza el formulario de rechazo (textarea para el motivo).</summary>
        string RenderRechazarForm(string token);
    }
}
