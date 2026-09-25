namespace Abril_Backend.Shared.Services.Revisores.Interfaces
{
    /// <summary>
    /// "El jefe de este trabajador", en los términos que usan las pantallas y los correos: quién
    /// aprueba su salida, qué jefe se entera, y quiénes aprueban la planilla y firman el consolidado
    /// de un documento.
    ///
    /// Desde el 2026-09-25 es una FACHADA de <c>IActoresResolver</c>, que es el único lugar que decide
    /// (lo personalizado por trabajador → lo personalizado por área en Revisores de Áreas → el
    /// algoritmo → GTH). Acá solo se traduce su respuesta a la forma que ya consumían Gestión
    /// Administrativa, SSOMA (correos de EMO e interconsultas) y Evaluaciones (recordatorios), para
    /// no tocar a esos consumidores.
    /// </summary>
    public interface IJefeRevisorResolver
    {
        /// <summary>Quien aprueba la salida del trabajador (su "jefe"), o null si nadie.</summary>
        Task<JefeRevisorResolution?> ResolveAsync(int workerId);

        /// <summary>
        /// Versión por lotes de <see cref="ResolveAsync"/>, con un número FIJO de consultas. Los
        /// trabajadores sin nadie resuelto no aparecen en el diccionario.
        /// </summary>
        Task<Dictionary<int, JefeRevisorResolution>> ResolveManyAsync(IReadOnlyCollection<int> workerIds);

        /// <summary>
        /// El <b>jefe notificado</b> de la salida del trabajador: solo existe para el personal de
        /// staff, al que aprueba su residente, y es su jefatura de área (o quien se haya personalizado).
        /// null cuando no aplica o no se resuelve a nadie. Sin fallback a GTH: sin jefatura no hay a
        /// quién informar.
        /// </summary>
        Task<JefeRevisorResolution?> ResolveJefeNotificadoAsync(int workerId);

        /// <summary>Versión por lotes de <see cref="ResolveJefeNotificadoAsync"/>.</summary>
        Task<Dictionary<int, JefeRevisorResolution>> ResolveJefeNotificadoManyAsync(
            IReadOnlyCollection<int> workerIds);

        /// <summary>
        /// Quiénes aprueban un DOCUMENTO que agrupa trabajadores —la planilla (1.ª revisión) o el
        /// consolidado con su planilla grupal—, en orden. Ver
        /// <c>IActoresResolver.ResolverDocumentosAsync</c>: los de cada trabajador, como en su ficha, y
        /// juntos; lo personalizado vale aunque esté dentro, el algoritmo no señala a nadie de adentro.
        /// </summary>
        /// <returns>
        /// Los aprobadores en ORDEN. Vacío si no se resuelve a nadie ni hay GTH. Casi siempre uno; en
        /// obra, el consolidado lo firman dos (el administrador de obra y después el residente).
        /// </returns>
        Task<List<AprobadorDocumento>> ResolveAprobadoresDeDocumentoAsync(
            IReadOnlyCollection<int> workerIds, PasoAprobacion paso);

        /// <summary>
        /// <see cref="ResolveAprobadoresDeDocumentoAsync"/> para varios documentos de una vez, con un
        /// número FIJO de consultas (el listado resuelve decenas de consolidados por página).
        /// </summary>
        /// <param name="workersPorDocumento">
        /// id del documento → los trabajadores que agrupa. La clave la pone el llamador y solo sirve
        /// para devolverle el resultado indexado.
        /// </param>
        Task<Dictionary<int, List<AprobadorDocumento>>> ResolveAprobadoresDeDocumentosAsync(
            IReadOnlyDictionary<int, IReadOnlyCollection<int>> workersPorDocumento,
            PasoAprobacion paso);
    }

    /// <summary>Cuál de los dos pasos del ciclo de la planilla se está resolviendo.</summary>
    public enum PasoAprobacion
    {
        /// <summary>La primera revisión de la planilla (Gestión de Rendiciones).</summary>
        PrimeraRevision,

        /// <summary>La firma del Consolidado del S10 y su planilla grupal (Consolidados).</summary>
        Consolidado,
    }

    /// <summary>Uno de los que tiene que aprobar un documento, con el lugar que ocupa.</summary>
    /// <param name="Orden">
    /// 1 = primero. Con varios aprobadores el orden IMPORTA: nadie firma antes que quien lo precede,
    /// para que el residente no pueda firmar un consolidado que el administrador de obra todavía no vio.
    /// </param>
    public record AprobadorDocumento(JefeRevisorResolution Persona, int Orden);

    /// <summary>
    /// Persona resuelta: un trabajador (WorkerId) o un área (AreaScopeId, el fallback de GTH) —
    /// exactamente uno de los dos — con el correo a usar y el nombre para mostrar.
    /// </summary>
    /// <param name="PersonId">
    /// Persona (<c>workers.person_id</c>), null en el fallback de área: la misma persona puede tener
    /// varias fichas en <c>workers</c> y comparar solo por ficha dejaría pasar un reingreso.
    /// </param>
    /// <param name="CategoriaId">
    /// Categoría del puesto (<c>workers.puesto_id → puesto.categoria_id</c>), null en el fallback de
    /// área y en fichas sin puesto.
    /// </param>
    public record JefeRevisorResolution(
        int? WorkerId, int? AreaScopeId, string Email, string? Nombre = null, int? PersonId = null,
        RevisorOrigen Origen = RevisorOrigen.Personalizado, int? CategoriaId = null);

    /// <summary>De dónde salió la persona.</summary>
    public enum RevisorOrigen
    {
        /// <summary>Alguien la eligió a mano: en la ficha del trabajador o en Revisores de Áreas.</summary>
        Personalizado = 0,

        /// <summary>La dedujo el sistema de la estructura.</summary>
        Algoritmo = 1,

        /// <summary>Nadie: se cayó al correo del área de GTH.</summary>
        Gth = 2,
    }
}
