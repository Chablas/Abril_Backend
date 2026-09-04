using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Sube y consulta el PDF "Consolidado del S10" de una planilla de rendición ya generada.
    /// Vive en el Shared del módulo porque lo usan Mis Rendiciones (el autoservicio, que es donde
    /// se adjunta), Gestión de Rendiciones (el revisor, que lo consulta y puede adjuntarlo en
    /// nombre del trabajador) y Gestión de Salidas, que solo lo lee para saber si ya hay algo que
    /// revisar.
    /// </summary>
    public interface IConsolidadoS10Service
    {
        /// <summary>
        /// Sube el PDF y lo asocia a la PLANILLA de rendición: el consolidado es la contraparte de
        /// la planilla en el S10, así que cubre todas sus salidas. Con
        /// <paramref name="ownerUserId"/> la planilla además tiene que incluir alguna salida del
        /// trabajador de ese usuario (autoservicio). Si ya había un consolidado vigente para esa
        /// planilla, queda con state = false (auditoría) y el nuevo pasa a ser el vigente.
        /// </summary>
        Task<ConsolidadoS10Dto> UploadParaRendicion(
            int rendicionId,
            IFormFile file,
            int userId,
            int? ownerUserId = null);

        /// <summary>
        /// Consolidado vigente de una solicitud: el de su planilla, o el propio de la salida en los
        /// registros antiguos (antes de que el consolidado fuera siempre de la planilla). Null si
        /// no hay ninguno.
        /// </summary>
        Task<ConsolidadoS10Dto?> GetForSolicitud(int solicitudId);

        /// <summary>
        /// Igual que <see cref="GetForSolicitud"/> pero en lote, para no caer en N+1 al armar las
        /// tablas. Devuelve solo las solicitudes que tienen consolidado.
        /// </summary>
        Task<Dictionary<int, ConsolidadoS10Dto>> GetForSolicitudes(IEnumerable<int> solicitudIds);

        /// <summary>
        /// Consolidado vigente de N planillas, en lote. Es lo que consume la tabla de Mis
        /// Rendiciones. Devuelve solo las planillas que tienen consolidado.
        /// </summary>
        Task<Dictionary<int, ConsolidadoS10Dto>> GetForRendiciones(IEnumerable<int> rendicionIds);
    }
}
