using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces
{
    public interface ISolicitudSalidaService
    {
        Task<SolicitudSalidaFormDataDto> GetFormData(int? userId);
        Task<List<SolicitudSalidaListItemDto>> GetByUserId(int userId, SolicitudSalidaFiltersDto? filters = null);
        Task<SolicitudSalidaFilterDataDto> GetFilterData(int userId);
        /// <summary>
        /// Crea la solicitud. <paramref name="adjuntos"/> trae los documentos adjuntos por índice
        /// de trayecto (0-based); son obligatorios para los trayectos cuyo motivo tiene
        /// requiere_adjunto = true y se suben a la carpeta configurada (ga_adjunto_folder).
        /// </summary>
        Task<int> Create(SolicitudSalidaCreateDto dto, int? userId, IReadOnlyList<(int TrayectoIndex, IFormFile File)>? adjuntos = null);

        Task<string> ProcessAprobarFromEmail(string token);
        Task<string> ProcessRechazarFromEmail(string token, string? motivoRechazo);
        string RenderRechazarForm(string token);

        Task<SolicitudSalidaDetalleDto> GetDetalle(int solicitudId, int userId);

        /// <summary>Sube N (imagen, monto) a SharePoint, asociadas a un trayecto específico de una solicitud aprobada/no rendida del propio usuario.</summary>
        Task<List<SolicitudSalidaCapturaDto>> UploadCapturasToTrayecto(int trayectoId, IEnumerable<(IFormFile File, decimal Monto)> items, int userId);

        /// <summary>Envía email de confirmación al solicitante de que su solicitud fue aprobada. Best-effort, no lanza.</summary>
        Task NotifySolicitanteAprobada(int solicitudId);
    }
}
