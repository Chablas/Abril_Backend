using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Sube y consulta el PDF "Consolidado del S10" de las planillas de rendición ya generadas.
    /// Vive en el Shared del módulo porque lo usan Mis Rendiciones (el autoservicio, que adjunta el
    /// de una planilla propia), Gestión de Rendiciones (los consolidadores, que además pueden
    /// adjuntar uno solo para varias planillas) y Gestión de Salidas, que solo lo lee para saber si
    /// ya hay algo que revisar.
    /// </summary>
    public interface IConsolidadoS10Service
    {
        /// <summary>
        /// Sube el PDF y lo asocia a las planillas indicadas —una o varias—: el consolidado es UN
        /// registro en el S10 y puede cubrir planillas de varios trabajadores, siempre que sean de
        /// una misma razón social (ver <see cref="ConsolidadoS10Agrupacion"/>). Las planillas que ya
        /// tenían consolidado pasan al nuevo, y el anterior queda con state = false en cuanto no le
        /// queda ninguna (auditoría).
        ///
        /// Todas tienen que tener la primera revisión aprobada y el reembolso por decidir, y si una
        /// ya tenía un consolidado compartido tienen que venir también las demás planillas abiertas
        /// de ese consolidado: el documento se reemplaza entero.
        ///
        /// Con <paramref name="ownerUserId"/> (autoservicio) cada planilla además tiene que incluir
        /// alguna salida del trabajador de ese usuario.
        /// </summary>
        /// <param name="montoTotal">
        /// Importe total del consolidado. Tiene que COINCIDIR con la suma de las planillas completas
        /// (<see cref="TotalPlanillaLoader"/>); si no, se rechaza con 400 y no se sube nada.
        /// </param>
        /// <param name="numeroGuia">Número de guía del S10. Texto obligatorio (no es un número nuestro).</param>
        Task<ConsolidadoS10Dto> UploadParaRendiciones(
            IReadOnlyCollection<int> rendicionIds,
            IFormFile file,
            decimal montoTotal,
            string numeroGuia,
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
