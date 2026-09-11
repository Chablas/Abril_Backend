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

        /// <summary>
        /// Todo lo que el modal de capturas necesita validar de una solicitud que todavía se puede
        /// tocar —antes de rendir, o al subsanar una rendición observada en primera revisión—: sus
        /// trayectos con los montos de sus capturas vivas y el tope con el que se compara cada
        /// uno. Con eso el servicio valida el lote entero (a qué trayecto entra cada captura
        /// nueva, de quién es cada captura editada y si algún trayecto se pasa del tope) sin
        /// gastar una consulta por fila.
        ///
        /// Null si la solicitud no existe, no es del usuario o ya está congelada.
        /// </summary>
        Task<TopeMovilidad.ContextoCapturas?> GetContextoCapturas(int solicitudId, int userId);

        /// <summary>
        /// Una captura del usuario que todavía se puede tocar (mismo criterio que
        /// <see cref="GetContextoCapturas"/>: antes de rendir, o al subsanar una
        /// rendición observada). Null si no existe, no es suya o su salida está congelada.
        /// </summary>
        Task<GaSolicitudCaptura?> GetCapturaEditable(int capturaId, int userId);

        /// <summary>Da de baja una captura (soft delete). Deja de contar para el importe rendido.</summary>
        Task EliminarCaptura(int capturaId);

        /// <summary>
        /// Escribe de un saque el lote del modal de capturas: da de alta las nuevas y aplica sobre
        /// las que ya estaban su monto y, si vino imagen, también la imagen (la fila es la misma,
        /// se le apunta el archivo nuevo). Un solo SaveChanges: o entra el lote entero o no entra
        /// nada. Los guards de propiedad los hace el servicio.
        /// </summary>
        Task GuardarCapturas(
            IReadOnlyList<(int TrayectoId, string Url, string? ItemId, string Filename, decimal Monto)> nuevas,
            IReadOnlyList<(int CapturaId, decimal Monto, (string Url, string? ItemId, string Filename)? Imagen)> ediciones,
            int userId);
    }
}
