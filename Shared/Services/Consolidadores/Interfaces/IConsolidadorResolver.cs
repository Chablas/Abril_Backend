namespace Abril_Backend.Shared.Services.Consolidadores.Interfaces
{
    /// <summary>
    /// Resuelve QUIÉNES pueden consolidar (adjuntar el Consolidado del S10) por un trabajador.
    ///
    /// Es el mismo algoritmo que el del jefe/revisor de un trabajador —se parte del nodo
    /// <c>puesto.area_destino_scope_id</c> y se sube por el árbol hasta el primer nodo que
    /// resuelva; en cada nodo mandan primero las asignaciones a mano (acá
    /// <c>area_consolidadores</c>, primero las del proyecto del trabajador y después las del área)
    /// y, si no hay ninguna, el ALGORITMO: el residente de la obra si el nodo filtra por proyecto,
    /// y si no el Jefe del área o el Gerente de la gerencia— con DOS diferencias:
    ///
    ///   1. No gana uno solo. En revisores la solicitud se manda al primer revisor activo; acá
    ///      TODOS los activos del nodo que resuelve quedan habilitados, porque consolidar no es
    ///      decidir: es hacerle un trámite al trabajador y cuantos más puedan, mejor.
    ///   2. El propio trabajador SIEMPRE puede. Consolidar lo suyo es el caso normal (lo hace desde
    ///      Mis Rendiciones); esta lista existe para decir quién MÁS puede hacerlo por él, así que
    ///      "nadie puede ser su propio jefe" no aplica.
    ///
    /// La estructura de la que se deduce el candidato automático sale de
    /// <c>EstructuraAreaLoader</c>, compartido con <c>IJefeRevisorResolver</c>: el Jefe de un área
    /// tiene que ser el mismo para las dos pantallas.
    /// </summary>
    public interface IConsolidadorResolver
    {
        /// <summary>
        /// Consolidadores aptos de cada trabajador pedido, con el propio trabajador incluido. Un
        /// número FIJO de consultas sea para 1 o para 500 trabajadores.
        /// </summary>
        Task<Dictionary<int, List<ConsolidadorElegido>>> ResolveManyAsync(IReadOnlyCollection<int> workerIds);

        /// <summary>
        /// Previsualización por ÁREA para la pantalla de configuración: para cada nodo
        /// <c>area_scope</c> pedido, quiénes consolidarían por un trabajador ubicado ahí (y, en los
        /// nodos que filtran por proyecto, quiénes por cada proyecto). No incluye al "propio
        /// trabajador": acá no hay trabajador, la pregunta es por el área.
        ///
        /// Sale del MISMO recorrido que <see cref="ResolveManyAsync"/>, así que la pantalla no
        /// puede mostrar a alguien distinto de quien va a poder consolidar.
        /// </summary>
        Task<Dictionary<int, AreaScopeConsolidadoresPreview>> ResolveByAreaScopeManyAsync(
            IReadOnlyCollection<int> areaScopeIds);

        /// <summary>
        /// De los trabajadores indicados, cuáles puede consolidar el usuario. Es lo que consume
        /// Gestión de Rendiciones para habilitar (o no) el botón "Consolidado S10" de cada planilla
        /// y para validar la subida en el servidor.
        ///
        /// La comparación es por PERSONA además de por ficha: un reingreso deja varias filas en
        /// <c>workers</c> para la misma persona y la asignación puede estar en cualquiera.
        /// </summary>
        Task<HashSet<int>> FiltrarQuePuedeConsolidarAsync(int userId, IReadOnlyCollection<int> workerIds);
    }

    /// <summary>Un consolidador apto, con el motivo por el que lo es.</summary>
    /// <param name="PersonId">Persona (<c>workers.person_id</c>), para comparar por persona y no por ficha.</param>
    public record ConsolidadorElegido(
        int WorkerId, int? PersonId, string Email, string? Nombre, ConsolidadorOrigen Origen);

    /// <summary>
    /// De dónde salió un consolidador, <b>visto desde el área por la que se preguntó</b>. Mismo
    /// criterio relativo que <c>RevisorOrigen</c>: lo que alguien cargó más arriba del árbol le
    /// llega a esta área porque el sistema fue a buscarlo, así que para ella es
    /// <see cref="Algoritmo"/>.
    /// </summary>
    public enum ConsolidadorOrigen
    {
        /// <summary>Alguien lo asignó a mano <b>para esta área</b> (fila de <c>area_consolidadores</c>).</summary>
        Personalizado = 0,

        /// <summary>
        /// Lo dedujo el sistema de la estructura —el Jefe/Gerente del área o el residente de la
        /// obra— o subió por el árbol hasta la configuración de otra área.
        /// </summary>
        Algoritmo = 1,

        /// <summary>El propio trabajador: siempre puede consolidar lo suyo.</summary>
        Propio = 2,
    }

    /// <summary>
    /// Consolidadores que le tocarían a un trabajador de un nodo del árbol. Se separa el caso sin
    /// proyecto del caso por proyecto por los nodos marcados "filtrar por proyecto"
    /// (<c>ga_salidas_area_config</c>): ahí la respuesta depende de la obra, así que se precalcula
    /// una por proyecto y el consumidor indexa por la que tenga a la vista.
    /// </summary>
    public record AreaScopeConsolidadoresPreview(
        List<ConsolidadorElegido> Area,
        Dictionary<int, List<ConsolidadorElegido>> PorProyecto);
}
