using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Infrastructure.Interfaces
{
    public interface IGestionRendicionRepository
    {
        /// <summary>Planillas visibles para el usuario, ya filtradas.</summary>
        Task<List<GestionRendicionListItemDto>> GetAll(GestionRendicionFiltersDto filters);

        /// <summary>Una planilla con el desglose de sus salidas visibles. Null si no ve ninguna.</summary>
        Task<GestionRendicionDetalleDto?> GetDetalle(int rendicionId, GestionRendicionFiltersDto scope);

        /// <summary>Opciones de los filtros (trabajadores, árbol de áreas y periodos) del alcance.</summary>
        Task<GestionRendicionFilterDataDto> GetFilterData(GestionRendicionFiltersDto scope);

        /// <summary>
        /// Traduce una selección de planillas + salidas sueltas a los ids de salida sobre los que
        /// se va a actuar, recortados a lo que el usuario puede ver. Sin esto, mandar un
        /// rendicion_id permitiría tocar salidas de áreas ajenas que cuelgan de la misma planilla.
        /// </summary>
        Task<List<int>> ResolverSolicitudIds(
            IEnumerable<int> rendicionIds, IEnumerable<int> solicitudIds, GestionRendicionFiltersDto scope);

        // ── Primera revisión (el paso anterior al Consolidado del S10) ──

        /// <summary>
        /// Aprueba u observa la PRIMERA revisión de las planillas indicadas. Solo mueve las que
        /// están "En primera revisión" y de las que el usuario ve alguna salida; el resto se ignora
        /// en silencio (la selección de la pantalla puede traer de todo). Devuelve las que sí se
        /// movieron, para avisarles a sus solicitantes.
        ///
        /// Nadie revisa una planilla con salidas propias, salvo que sea su propio revisor (jefe
        /// personalizado apuntándose a sí mismo): en ese caso lanza 403 sin mover nada.
        /// </summary>
        /// <param name="aprobar">true = Aprobada; false = Observada (exige observación, RG-20).</param>
        Task<List<int>> DecidirPrimeraRevision(
            IEnumerable<int> rendicionIds, bool aprobar, string? observacion,
            GestionRendicionFiltersDto scope, int reviewerUserId);

        /// <summary>
        /// Lo que necesitan los correos de la decisión de la primera revisión. Devuelve UNA entrada
        /// por trabajador de la planilla: el aviso va al dueño de las salidas, y una planilla
        /// generada por el revisor puede agrupar a varios. Vacío si no hay a quién avisarle.
        /// </summary>
        Task<List<PrimeraRevisionCorreoInfoDto>> GetPrimeraRevisionCorreoInfo(int rendicionId);

        // ── Reembolso (movido desde Gestión de Salidas: es un paso posterior al consolidado) ──

        /// <summary>
        /// Aprueba o rechaza el reembolso de las salidas indicadas. Solo pasan las que están
        /// Rendidas, tienen Consolidado del S10 y su reembolso sigue Pendiente o Rechazado — el
        /// resto se ignora en silencio (la selección de la pantalla puede traer de todo).
        ///
        /// Un usuario no decide el reembolso de sus propias salidas, misma regla que la aprobación
        /// de la salida: la única excepción es que él sea su propio revisor (jefe personalizado
        /// apuntándose a sí mismo).
        /// </summary>
        /// <returns>Ids de las salidas que efectivamente cambiaron de estado.</returns>
        Task<List<int>> RechazarReembolso(IEnumerable<int> ids, string observacion, int reviewerUserId);

        /// <summary>
        /// Planillas cuyo reembolso se puede aprobar, con los documentos que hay que firmar (su PDF
        /// y el Consolidado del S10). Aplica los MISMOS guards que la escritura —elegibilidad y
        /// "nadie decide lo suyo"— porque se corre antes de tocar SharePoint: firmar y subir para
        /// después descubrir que la salida no era elegible dejaría archivos huérfanos.
        /// </summary>
        Task<List<PlanillaParaFirmarDto>> GetPlanillasParaAprobarReembolso(
            IEnumerable<int> ids, int reviewerUserId);

        /// <summary>
        /// Aprueba el reembolso y guarda las copias firmadas. Las salidas quedan en FIRMADO, no en
        /// "Aprobado": aprobar ES la firma del revisor, y Firmado es lo que Tesorería ve como
        /// pagable. Se escribe todo junto (planilla, consolidados y salidas) recién cuando los PDF
        /// ya están subidos.
        /// </summary>
        /// <returns>Ids de las salidas que efectivamente cambiaron de estado.</returns>
        Task<List<int>> AprobarReembolsoFirmado(
            IReadOnlyCollection<PlanillaFirmadaDto> planillas, int reviewerUserId);


        /// <summary>
        /// Correos de los solicitantes de las salidas de la selección que todavía tienen el
        /// reembolso por decidir. Son los destinatarios principales del aviso de la decisión, y la
        /// pantalla los usa para anunciar a quién le va a llegar antes de aprobar o rechazar.
        ///
        /// Toma la misma selección que la escritura (planillas y/o salidas sueltas) y le aplica el
        /// mismo recorte por visibilidad y la misma elegibilidad, para que el preview no anuncie a
        /// alguien a quien la acción no va a tocar. Vacío si no queda ninguna.
        /// </summary>
        Task<List<string>> GetCorreosSolicitantesPorDecidir(
            IEnumerable<int> rendicionIds, IEnumerable<int> solicitudIds, GestionRendicionFiltersDto scope);

        /// <summary>
        /// Correos de los solicitantes a los que llegaría el aviso de la decisión de la PRIMERA
        /// REVISIÓN de las planillas seleccionadas. Mismo criterio que
        /// <see cref="GetCorreosSolicitantesPorDecidir"/>: recorta por visibilidad y por la
        /// elegibilidad de la escritura (solo planillas esperando la primera revisión).
        /// </summary>
        Task<List<string>> GetCorreosSolicitantesPrimeraRevision(
            IEnumerable<int> rendicionIds, GestionRendicionFiltersDto scope);

        /// <summary>
        /// Correos de quienes atienden la bandeja de Reembolsos, destinatarios principales del
        /// aviso a Tesorería que dispara la aprobación del reembolso. No dependen de la planilla
        /// —se resuelven por puesto y rol, igual que en <see cref="GetTesoreriaCorreoInfo"/>— así
        /// que el preview de una selección los resuelve una sola vez.
        /// </summary>
        Task<List<string>> GetCorreosTesoreria();

        /// <summary>Carpeta de SharePoint donde se guardan las planillas (y sus copias firmadas).</summary>
        Task<string?> GetRendicionFolderUrl();

        /// <summary>
        /// Datos de una salida para armar los correos del reembolso (trabajador, área, planilla,
        /// monto rendido y a quién avisar). Null si la salida no existe.
        /// </summary>
        Task<ReembolsoCorreoInfoDto?> GetReembolsoCorreoInfo(int solicitudId);

        /// <summary>
        /// Lo que necesita el aviso a Tesorería de que una planilla quedó firmada (RF-TES-01): sus
        /// datos y los correos de quienes atienden esa bandeja. Es por planilla y no por
        /// trabajador —Tesorería paga el documento completo— y devuelve null si la planilla no
        /// existe. Los destinatarios pueden venir vacíos: no hay nadie con puesto de Tesorería.
        /// </summary>
        Task<TesoreriaCorreoInfoDto?> GetTesoreriaCorreoInfo(int rendicionId);
    }
}
