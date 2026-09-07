using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces
{
    public interface ISolicitudSalidaService
    {
        Task<SolicitudSalidaFormDataDto> GetFormData(int? userId);
        /// <summary>
        /// Listado de las solicitudes del trabajador ya filtrado, junto con los números de las
        /// tarjetas contados sobre ese mismo conjunto.
        /// </summary>
        Task<SolicitudSalidaListResultDto> GetByUserId(int userId, SolicitudSalidaFiltersDto? filters = null);
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

        /// <summary>
        /// El propio solicitante cancela una solicitud SUYA que esté Pendiente. Lanza 403 si la
        /// solicitud es de otro trabajador y 400 si no está Pendiente. Estado resultante: Cancelado.
        /// </summary>
        Task Cancelar(int solicitudId, int userId);

        /// <summary>
        /// Sube N (imagen, monto) a SharePoint, asociadas a un trayecto de una solicitud propia
        /// que se pueda editar: aprobada y sin rendir, o rendida en una planilla OBSERVADA en
        /// primera revisión (ahí corregir capturas y montos es justamente lo que se pidió).
        /// </summary>
        Task<List<SolicitudSalidaCapturaDto>> UploadCapturasToTrayecto(int trayectoId, IEnumerable<(IFormFile File, decimal Monto)> items, int userId);

        /// <summary>
        /// Guarda los cambios de una captura propia: su monto y, si viene
        /// <paramref name="file"/>, además reemplaza su imagen (sube el archivo nuevo y apunta la
        /// misma fila a él). Mismo criterio de edición que la subida: solo antes de rendir o al
        /// subsanar una rendición observada.
        ///
        /// Es una sola operación porque en la pantalla es un solo botón "Guardar": lo que el
        /// trabajador corrige es la fila —monto, imagen o las dos—, no un campo suelto.
        /// </summary>
        /// <returns>La captura ya actualizada, para repintar la miniatura sin recargar el detalle.</returns>
        Task<SolicitudSalidaCapturaDto> ActualizarCaptura(
            int capturaId, decimal monto, IFormFile? file, int userId);

        /// <summary>
        /// Da de baja una captura propia (soft delete). Deja de contar para el importe rendido y
        /// para la planilla. Mismo criterio de edición que la subida.
        /// </summary>
        Task EliminarCaptura(int capturaId, int userId);

        /// <summary>
        /// Ids de las salidas PROPIAS del mes indicado (sin año/mes, el anterior; por fecha de
        /// salida en hora de Perú) que están aptas para rendir: aprobadas, no rendidas, con todos
        /// sus trayectos cubiertos y con un motivo reembolsable. Lanza 400 si no hay ninguna.
        /// </summary>
        Task<List<int>> GetIdsRendiblesMes(int userId, int? anio, int? mes);

        // El Consolidado del S10 y el aviso al revisor ya no viven acá: son de la PLANILLA de
        // rendición y los expone IRendicionService (Mis Rendiciones). Esta pantalla llega hasta
        // rendir.

        /// <summary>Envía email de confirmación al solicitante de que su solicitud fue aprobada. Best-effort, no lanza.</summary>
        Task NotifySolicitanteAprobada(int solicitudId);

        /// <summary>Envía email al solicitante de que su solicitud fue rechazada (mismos destinatarios/copias que el de aprobación). Best-effort, no lanza.</summary>
        Task NotifySolicitanteRechazada(int solicitudId);
    }
}
