using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Prepara la planilla grupal y sube y consulta el PDF "Consolidado del S10" de las planillas de
    /// rendición ya generadas. Vive en el Shared del módulo porque lo usan Gestión de Rendiciones
    /// (los consolidadores, los únicos que preparan la planilla grupal y adjuntan el consolidado: uno
    /// solo puede cubrir varias planillas), Consolidados (el reemplazo) y Gestión de Salidas, que
    /// solo lo lee para saber si ya hay algo que revisar.
    /// </summary>
    public interface IConsolidadoS10Service
    {
        /// <summary>
        /// Arma la PLANILLA GRUPAL de las planillas indicadas —el papel con el que el consolidador
        /// las registra en el S10— y la sube a SharePoint, sin firmar y sin número de reembolso
        /// (todavía no existe). Es el paso previo a subir el Consolidado del S10, que se sube sobre
        /// esta planilla. El código CONS-ÁREA-AAAA-NNN nace acá.
        ///
        /// Todas tienen que tener la primera revisión aprobada y ninguna una planilla grupal ni el
        /// Consolidado del S10 (409): una planilla grupal ya preparada no se rehace ni se reemplaza,
        /// porque es lo que el consolidador registró en el S10.
        ///
        /// No mira permisos: quién puede consolidar lo valida la pantalla que llama.
        /// </summary>
        Task<PlanillaGrupalDto> PrepararPlanillaGrupal(IReadOnlyCollection<int> rendicionIds, int userId);

        /// <summary>
        /// Sube el PDF y lo asocia a las planillas indicadas —una o varias—: el consolidado es UN
        /// registro en el S10 y puede cubrir planillas de varios trabajadores, de las razones
        /// sociales que sean (ver <see cref="ConsolidadoS10Agrupacion"/>). Las planillas que ya
        /// tenían consolidado pasan al nuevo, y el anterior queda con state = false en cuanto no le
        /// queda ninguna (auditoría).
        ///
        /// Todas tienen que tener la primera revisión aprobada y el reembolso por decidir, y si una
        /// ya tenía un consolidado compartido tienen que venir también las demás planillas abiertas
        /// de ese consolidado: el documento se reemplaza entero.
        ///
        /// La planilla grupal NUNCA se arma acá: el primer consolidado se sube sobre la que preparó
        /// el consolidador (<see cref="PrepararPlanillaGrupal"/>), que tiene que cubrir exactamente
        /// estas planillas, y hereda su código; reemplazarlo se queda con la del consolidado que
        /// reemplaza (un consolidado anterior a la planilla grupal sigue sin tenerla).
        ///
        /// No mira permisos: quién puede consolidar lo valida la pantalla que llama.
        /// </summary>
        /// <param name="montoTotal">
        /// Importe total del consolidado. Tiene que COINCIDIR con la suma de las planillas completas
        /// (<see cref="TotalPlanillaLoader"/>); si no, se rechaza con 400 y no se sube nada.
        /// </param>
        /// <param name="numeroReembolso">Número del reembolso del S10. Texto obligatorio (no es un número nuestro).</param>
        Task<ConsolidadoS10Dto> UploadParaRendiciones(
            IReadOnlyCollection<int> rendicionIds,
            IFormFile file,
            decimal montoTotal,
            string numeroReembolso,
            int userId);

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
        /// Consolidado vigente de N planillas, en lote. Devuelve solo las planillas que tienen
        /// consolidado.
        /// </summary>
        Task<Dictionary<int, ConsolidadoS10Dto>> GetForRendiciones(IEnumerable<int> rendicionIds);
    }
}
