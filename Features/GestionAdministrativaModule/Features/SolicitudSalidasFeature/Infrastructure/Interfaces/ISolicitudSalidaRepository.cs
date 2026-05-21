using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Interfaces
{
    public interface ISolicitudSalidaRepository
    {
        Task<SolicitudSalidaFormDataDto> GetFormData();
        Task<List<SolicitudSalidaListItemDto>> GetByUserId(int userId);

        /// <summary>Crea la solicitud y devuelve el id + el worker del solicitante (para resolver aprobador y notificar).</summary>
        Task<(int Id, Worker Solicitante)> Create(SolicitudSalidaCreateDto dto, int? userId);

        /// <summary>Persiste el email del aprobador asignado a una solicitud.</summary>
        Task SetAprobadorEmail(int solicitudId, string aprobadorEmail);

        /// <summary>Marca una solicitud como aprobada. Devuelve null si ya no estaba pendiente.</summary>
        Task<GaSolicitudSalida?> Aprobar(int solicitudId);

        /// <summary>Marca una solicitud como rechazada con motivo. Devuelve null si ya no estaba pendiente.</summary>
        Task<GaSolicitudSalida?> Rechazar(int solicitudId, string? motivoRechazo);
    }
}
