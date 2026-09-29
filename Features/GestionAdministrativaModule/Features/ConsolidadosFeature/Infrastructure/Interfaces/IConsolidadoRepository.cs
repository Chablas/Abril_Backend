using Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;

namespace Abril_Backend.Features.GestionAdministrativa.Consolidados.Infrastructure.Interfaces
{
    /// <summary>
    /// Los Consolidados del S10 del alcance del usuario, la escritura de la decisión del reembolso
    /// (de la jefatura) y los dos trámites del consolidador (avisar a la jefatura y pedir la
    /// corrección al Coordinador ERP). Es el mismo recorte por área que el resto del módulo, solo
    /// que en el ámbito CONSOLIDADOS: ver las planillas en Gestión de Rendiciones y ver los
    /// consolidados acá son dos permisos distintos.
    /// </summary>
    public interface IConsolidadoRepository
    {
        Task<List<ConsolidadoListItemDto>> GetAll(ConsolidadoFiltersDto filters);

        Task<ConsolidadoDetalleDto?> GetDetalle(int consolidadoId, ConsolidadoFiltersDto scope);

        /// <summary>
        /// El detalle de UNA salida rendida del alcance —trayectos, capturas, montos y adjuntos—, en
        /// consulta. Null si la salida no está rendida o no está en su alcance.
        /// </summary>
        Task<SolicitudSalidaDetalleDto?> GetSalidaDetalle(int solicitudId, ConsolidadoFiltersDto scope);

        Task<ConsolidadoFilterDataDto> GetFilterData(ConsolidadoFiltersDto scope);

        /// <summary>
        /// Salidas del alcance del usuario cubiertas por esos consolidados. Un consolidado puede
        /// cubrir planillas de varias áreas, así que el recorte se aplica igual: mandar su id no
        /// alcanza salidas que el usuario no ve.
        /// </summary>
        Task<List<int>> ResolverSolicitudIds(IEnumerable<int> consolidadoIds, ConsolidadoFiltersDto scope);

        /// <summary>
        /// Observa el reembolso de esas salidas con el comentario de la jefatura. Solo toca las que
        /// el usuario decide (es su jefatura): 403 si no decide ninguna. Devuelve las que cambiaron.
        /// </summary>
        Task<List<int>> ObservarReembolso(IEnumerable<int> ids, string observacion, int reviewerUserId);

        /// <summary>
        /// De esas salidas, qué planillas se pueden aprobar hoy y qué PDF hay que firmar de cada
        /// una (el de la planilla y el del consolidado). Aplica los mismos guards que la escritura:
        /// solo las salidas de las que el usuario es la jefatura.
        /// </summary>
        Task<List<PlanillaParaFirmarDto>> GetPlanillasParaAprobarReembolso(
            IEnumerable<int> ids, int reviewerUserId);

        /// <summary>
        /// Escribe la aprobación una vez que las copias firmadas ya están en SharePoint: referencia
        /// los PDF firmados y deja en "Firmado" las salidas cuyo documento ya reunió TODAS las
        /// firmas que su área exige. Las que siguen esperando otra firma quedan como estaban, con
        /// la estampa ya puesta en el papel.
        /// </summary>
        /// <param name="firmadoAt">
        /// El momento de la firma: el MISMO que quedó impreso en el pie de los PDF.
        /// </param>
        Task<ReembolsoFirmaResultDto> AprobarReembolsoFirmado(
            IReadOnlyCollection<PlanillaFirmadaDto> planillas, int reviewerUserId, DateTimeOffset firmadoAt);

        /// <summary>
        /// Los consolidados de la selección que este usuario puede volver a firmar, con todo lo que
        /// hace falta para rehacer sus copias firmadas desde el original. 409 si en ninguno le
        /// corresponde: solo se puede mientras el documento siga esperando la firma de quien va
        /// detrás (ver <c>PuedeVolverAFirmar</c>).
        /// </summary>
        Task<List<ConsolidadoParaRefirmarDto>> GetConsolidadosParaVolverAFirmar(
            IEnumerable<int> consolidadoIds, ConsolidadoFiltersDto scope, int userId);

        /// <summary>
        /// Escribe las copias rehechas: reemplaza las referencias a los PDF firmados y pone al día
        /// la fecha de la firma de este usuario. NO agrega una firma más —es la misma— ni mueve el
        /// estado del reembolso: el documento sigue esperando exactamente lo que esperaba.
        /// </summary>
        Task<int> RegistrarVolverAFirmar(
            IReadOnlyCollection<ConsolidadoRefirmadoDto> refirmados, int userId, DateTimeOffset firmadoAt);

        /// <summary>
        /// Nombre y puesto de quien firma, para el pie de la firma. El puesto sale de su ficha
        /// vigente: una persona puede tener más de una en <c>workers</c>.
        /// </summary>
        Task<FirmanteDto> GetFirmante(int userId);

        /// <summary>
        /// Correos de los consolidadores (quien adjuntó cada consolidado) a los que les llegaría la
        /// decisión del reembolso de esos consolidados: solo los que tienen algo que el usuario
        /// decide.
        /// </summary>
        Task<List<string>> GetCorreosConsolidadorPorDecidir(
            IEnumerable<int> consolidadoIds, ConsolidadoFiltersDto scope, int userId);

        /// <summary>Correos corporativos de quien tiene el rol TESORERO.</summary>
        Task<List<string>> GetCorreosTesoreria();

        /// <summary>
        /// De esos consolidados, los que vuelven de una observación de Tesorería (sus salidas
        /// pendientes todavía la llevan): al aprobarlos, el aviso a Tesorería es el de observación
        /// subsanada. Lo usa el preview; el envío lo decide la propia escritura.
        /// </summary>
        Task<HashSet<int>> GetConsolidadosQueVuelvenATesoreria(IEnumerable<int> consolidadoIds);

        /// <summary>
        /// Qué pasaría si el usuario firmara ahora los consolidados de la selección: a quién le
        /// pasaría el turno y si alguno reuniría todas sus firmas. En obra el documento lo firman
        /// dos, así que la primera firma no avisa al consolidador ni a Tesorería —el reembolso sigue
        /// Pendiente— y el único correo que sale es el que le pasa el turno al residente.
        /// </summary>
        Task<ProximaFirmaDto> GetProximaFirma(
            IEnumerable<int> consolidadoIds, ConsolidadoFiltersDto scope, int? userId);

        /// <summary>Carpeta de SharePoint donde viven las planillas y sus copias firmadas.</summary>
        Task<string?> GetRendicionFolderUrl();

        /// <summary>
        /// Lo que necesitan los correos al consolidador sobre esas salidas: UNA entrada por
        /// consolidado (ver <c>ConsolidadoCorreoLoader</c>).
        /// </summary>
        Task<List<ConsolidadoCorreoDatos>> GetConsolidadoCorreoDatos(IReadOnlyCollection<int> solicitudIds);

        /// <summary>
        /// Lo que necesita el aviso a Tesorería de UN consolidado firmado: el resumen del documento
        /// entero y quiénes tienen el rol TESORERO. Null si el consolidado no existe.
        /// </summary>
        Task<TesoreriaCorreoInfoDto?> GetTesoreriaCorreoInfo(int consolidadoId);

        // ── Trámites del consolidador ────────────────────────────────────────

        /// <summary>
        /// Lo que necesita "Avisar a la jefatura" sobre un consolidado del alcance. Null si el
        /// usuario no ve ninguna de sus salidas.
        /// </summary>
        Task<AvisoJefaturaInfoDto?> GetAvisoJefatura(int consolidadoId, ConsolidadoFiltersDto scope, int userId);

        /// <summary>
        /// Lo mismo, pero para el aviso que dispara una firma: a quién le toca firmar ahora que el
        /// anterior ya firmó. Sin alcance ni consolidador de por medio —no lo pide un usuario— y
        /// null si el consolidado no existe. Con todas las firmas puestas devuelve la lista de
        /// destinatarios vacía y no hay nada que mandar.
        /// </summary>
        Task<AvisoJefaturaInfoDto?> GetAvisoSiguienteFirmante(int consolidadoId);

        /// <summary>Estampa en esas salidas que se le avisó a la jefatura, y quién lo hizo.</summary>
        Task MarcarJefaturaAvisada(IReadOnlyCollection<int> solicitudIds, int userId);

        /// <summary>
        /// Lo que necesita "Solicitar corrección al ERP" sobre un consolidado del alcance. Null si el
        /// consolidado no existe o el usuario no ve ninguna de sus salidas.
        /// </summary>
        Task<CorreccionConsolidadoPlanDto?> GetCorreccionPlan(int consolidadoId, ConsolidadoFiltersDto scope, int userId);

        /// <summary>
        /// Lo que necesita "Reemplazar el consolidado" sobre un consolidado del alcance: si el
        /// usuario es su consolidador, qué planillas pasan al documento nuevo (las que siguen con el
        /// reembolso abierto) y a qué jefatura se le avisa. Null si el consolidado no existe o el
        /// usuario no ve ninguna de sus salidas.
        /// </summary>
        Task<ReemplazoConsolidadoPlanDto?> GetReemplazoPlan(int consolidadoId, ConsolidadoFiltersDto scope, int userId);

        /// <summary>
        /// Registra la solicitud de corrección del consolidado al Coordinador ERP: una fila por cada
        /// planilla observada que no tenga otra corrección viva (la bandeja del ERP es por planilla),
        /// todas con el mismo motivo y atadas al mismo consolidado. Copia la observación y el número
        /// de reembolso: el ERP los necesita y la observación de la salida se sobrescribe si vuelven a
        /// observar. No mueve el estado del reembolso. 409 si no queda ninguna por registrar.
        /// </summary>
        Task<List<GaCorreccionS10>> CrearCorrecciones(
            int consolidadoId, string? numeroReembolso, IReadOnlyCollection<int> rendicionIds,
            string motivo, int userId);

        /// <summary>
        /// Correos de los usuarios con el rol COORDINADOR ERP: el destinatario principal de la
        /// solicitud de corrección. Se resuelve por ROL y no por área porque el responsable ERP no
        /// cuelga del organigrama — mismo criterio que el aviso a Tesorería.
        /// </summary>
        Task<List<string>> GetCorreosCoordinadorErp();
    }
}
