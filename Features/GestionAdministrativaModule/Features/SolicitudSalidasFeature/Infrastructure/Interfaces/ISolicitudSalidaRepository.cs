using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Interfaces
{
    public interface ISolicitudSalidaRepository
    {
        Task<SolicitudSalidaFormDataDto> GetFormData();
        Task<List<SolicitudSalidaListItemDto>> GetByUserId(int userId, SolicitudSalidaFiltersDto? filters = null);

        /// <summary>
        /// Feriados y días no laborables (Configuración → Feriados) ya resueltos, para calcular el
        /// plazo de rendición fuera del repositorio.
        /// </summary>
        Task<CalendarioNoLaborable> GetCalendarioNoLaborable();
        Task<SolicitudSalidaFilterDataDto> GetFilterData(int userId);

        /// <summary>Crea la solicitud + sus trayectos en una transacción. Devuelve la solicitud (con trayectos) y el worker solicitante.
        /// <paramref name="adjuntosPorIndice"/>: documentos adjuntos ya subidos a SharePoint por índice de trayecto (0-based); cada trayecto puede tener N.</summary>
        Task<(GaSolicitudSalida Solicitud, List<GaSolicitudTrayecto> Trayectos, Worker Solicitante)> Create(SolicitudSalidaCreateDto dto, int? userId, Dictionary<int, List<TrayectoAdjuntoSubidoDto>>? adjuntosPorIndice = null);

        /// <summary>Guarda el correo al que se envió la solicitud para aprobación (enviado_a_correo).</summary>
        Task SetEnviadoACorreo(int solicitudId, string correo);
        Task<GaSolicitudSalida?> Aprobar(int solicitudId);
        Task<GaSolicitudSalida?> Rechazar(int solicitudId, string? motivoRechazo);

        /// <summary>
        /// El propio solicitante cancela su solicitud. Valida que la solicitud sea del worker
        /// del usuario (403 si es de otro) y que esté en estado Pendiente (400 si no). Deja el
        /// estado en Cancelado y registra quién/cuándo la canceló.
        /// </summary>
        Task Cancelar(int solicitudId, int userId);

        /// <summary>Detalle de la solicitud (con trayectos y capturas). Solo retorna si pertenece al usuario.</summary>
        Task<SolicitudSalidaDetalleDto?> GetDetalleForUser(int solicitudId, int userId);

        /// <summary>Carga un trayecto verificando que pertenezca al user y que la solicitud esté aprobada + no rendida.</summary>
        Task<GaSolicitudTrayecto?> GetTrayectoForUploadingCapturas(int trayectoId, int userId);

        /// <summary>
        /// Una captura del usuario que todavía se puede tocar (mismo criterio que
        /// <see cref="GetTrayectoForUploadingCapturas"/>: antes de rendir, o al subsanar una
        /// rendición observada). Null si no existe, no es suya o su salida está congelada.
        /// </summary>
        Task<GaSolicitudCaptura?> GetCapturaEditable(int capturaId, int userId);

        /// <summary>
        /// Guarda los cambios de una captura ya subida: su monto y, si se pasa
        /// <paramref name="imagen"/>, también la imagen (la fila es la misma, se le apunta el
        /// archivo nuevo). El guard de propiedad lo hace el servicio.
        ///
        /// Va en una sola operación porque en la pantalla es un solo botón: el trabajador corrige
        /// la fila —monto, imagen o las dos— y guarda. Devuelve la captura ya actualizada para que
        /// la pantalla repinte la miniatura sin recargar el detalle entero.
        /// </summary>
        Task<SolicitudSalidaCapturaDto> ActualizarCaptura(
            int capturaId,
            decimal monto,
            (string Url, string? ItemId, string Filename)? imagen);

        /// <summary>Da de baja una captura (soft delete). Deja de contar para el importe rendido.</summary>
        Task EliminarCaptura(int capturaId);

        Task<List<SolicitudSalidaCapturaDto>> InsertCapturas(int trayectoId, IEnumerable<(string Url, string? ItemId, string Filename, decimal Monto)> items, int userId);
    }
}
