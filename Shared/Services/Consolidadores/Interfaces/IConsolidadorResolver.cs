namespace Abril_Backend.Shared.Services.Consolidadores.Interfaces
{
    /// <summary>
    /// QUIÉNES pueden consolidar (preparar la planilla grupal y subir el Consolidado del S10) por un
    /// trabajador.
    ///
    /// Desde el 2026-09-25 es una FACHADA de <c>IActoresResolver</c> sobre el actor
    /// <c>ActorIds.Consolidador</c>: lo personalizado por trabajador, lo personalizado por área en
    /// Revisores de Áreas y, si no hay nada, el algoritmo — la jefatura del área para oficina central,
    /// el administrador de obra para el staff, y a una jefatura sus pares de la misma categoría (a un
    /// residente, los de su misma obra) —. A diferencia de los aprobadores, no gana uno solo: todos
    /// los de la lista pueden EMPEZAR; el trámite de una planilla grupal ya preparada lo sigue solo
    /// quien la preparó (ver <c>TramiteConsolidador</c>).
    /// </summary>
    public interface IConsolidadorResolver
    {
        /// <summary>
        /// Consolidadores aptos de cada trabajador pedido. Un número FIJO de consultas sea para 1 o
        /// para 500 trabajadores.
        /// </summary>
        Task<Dictionary<int, List<ConsolidadorElegido>>> ResolveManyAsync(IReadOnlyCollection<int> workerIds);

        /// <summary>
        /// De los trabajadores indicados, cuáles puede consolidar el usuario. La comparación es por
        /// PERSONA además de por ficha: un reingreso deja varias fichas para la misma persona.
        /// </summary>
        Task<HashSet<int>> FiltrarQuePuedeConsolidarAsync(int userId, IReadOnlyCollection<int> workerIds);

        /// <summary>
        /// <see cref="FiltrarQuePuedeConsolidarAsync"/> para varios usuarios de una vez (una sola
        /// resolución): usuario → trabajadores que puede consolidar. Lo usa la regla de quién sigue
        /// el trámite de una planilla grupal, que tiene que saber si quien la preparó sigue siendo
        /// consolidador.
        /// </summary>
        Task<Dictionary<int, HashSet<int>>> FiltrarQuePuedenConsolidarAsync(
            IReadOnlyCollection<int> userIds, IReadOnlyCollection<int> workerIds);
    }

    /// <summary>Un consolidador apto, con de dónde salió.</summary>
    /// <param name="PersonId">Persona (<c>workers.person_id</c>), para comparar por persona y no por ficha.</param>
    public record ConsolidadorElegido(
        int WorkerId, int? PersonId, string Email, string? Nombre, ConsolidadorOrigen Origen);

    /// <summary>De dónde salió un consolidador.</summary>
    public enum ConsolidadorOrigen
    {
        /// <summary>Alguien lo eligió a mano: en la ficha del trabajador o en Revisores de Áreas.</summary>
        Personalizado = 0,

        /// <summary>Lo dedujo el sistema de la estructura.</summary>
        Algoritmo = 1,
    }
}
