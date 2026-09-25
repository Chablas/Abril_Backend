namespace Abril_Backend.Shared.Services.Actores.Interfaces
{
    /// <summary>
    /// El ÚNICO lugar que decide quiénes son los cinco actores (<c>ActorIds</c>) de un trabajador:
    /// quién aprueba su salida, qué jefe se entera, quién aprueba la 1.ª revisión de su planilla,
    /// quiénes la consolidan y quiénes firman su consolidado.
    ///
    /// Cada actor se resuelve igual, en este orden, y lo primero que responda gana:
    ///
    ///   1. lo personalizado PARA EL TRABAJADOR (ficha del trabajador, <c>workers_actor_asignacion</c>);
    ///   2. recorriendo su área hacia la raíz, en cada nodo:
    ///        a. lo personalizado para su obra en ese nodo, y si no, para el área entera
    ///           (Configuración → Revisores de Áreas, <c>area_actor_asignacion</c>), siempre para su
    ///           CASO (<c>ActorCasoIds</c>): lo del staff no toca a los jefes;
    ///        b. el ALGORITMO del nodo para su caso (ver <c>ActoresResolver.Algoritmo</c>);
    ///   3. el buzón de GTH, solo para los actores que deciden algo.
    ///
    /// Lo personalizado (1 y 2.a) le gana siempre al algoritmo, aunque la persona elegida esté
    /// dentro del documento que se aprueba: se eligió a mano.
    ///
    /// Hay tres formas de preguntar y todas pasan por el mismo recorrido, para que la ficha del
    /// trabajador, la pantalla de Revisores de Áreas y quien de verdad recibe el correo no puedan
    /// discrepar: por trabajador (<see cref="ResolverTrabajadoresAsync"/>), por un trabajador que
    /// todavía no existe o que se está editando (<see cref="ResolverContextoAsync"/>), por un
    /// documento que agrupa a varios (<see cref="ResolverDocumentosAsync"/>) y por fila de la
    /// pantalla (<see cref="PrevisualizarAsync"/>).
    ///
    /// Todo en un número FIJO de consultas, sean 1 o 500 trabajadores.
    /// </summary>
    public interface IActoresResolver
    {
        /// <summary>
        /// Los actores de cada trabajador pedido (todos, o solo <paramref name="actorIds"/>). Un
        /// trabajador que no existe no aparece en el diccionario.
        /// </summary>
        Task<Dictionary<int, ActoresDeTrabajador>> ResolverTrabajadoresAsync(
            IReadOnlyCollection<int> workerIds, IReadOnlyCollection<int>? actorIds = null);

        /// <summary>
        /// Los cinco actores de un trabajador descrito por su contexto — el formulario de la ficha,
        /// que puede estar creando uno nuevo o cambiándole el puesto a uno que existe —. Con
        /// <see cref="ContextoTrabajador.WorkerId"/> se suman sus personalizados guardados.
        /// </summary>
        Task<ActoresDeTrabajador> ResolverContextoAsync(ContextoTrabajador contexto);

        /// <summary>
        /// Quiénes intervienen en un DOCUMENTO que agrupa trabajadores —la planilla (1.ª revisión) o
        /// el consolidado—, en orden. <paramref name="actorId"/> es
        /// <c>ActorIds.AprobadorPrimeraRevision</c> o <c>ActorIds.AprobadorConsolidado</c>.
        ///
        /// Son los de CADA trabajador, resueltos como en su ficha, juntos y sin romper el orden de
        /// ninguno: si todos resuelven lo mismo es eso; si no, intervienen todos. Lo personalizado
        /// vale aunque la persona esté DENTRO del documento; el algoritmo, en cambio, no señala a
        /// nadie de adentro.
        /// </summary>
        Task<Dictionary<int, List<ActorPersona>>> ResolverDocumentosAsync(
            IReadOnlyDictionary<int, IReadOnlyCollection<int>> workersPorDocumento, int actorId);

        /// <summary>
        /// Lo que resolvería un trabajador hipotético de cada fila de Revisores de Áreas (un nodo, y
        /// opcionalmente una obra de ese nodo) para cada caso pedido: los cinco actores con su
        /// origen. Sin lo personalizado por trabajador — la pregunta es por el área —.
        /// </summary>
        Task<Dictionary<(int Nodo, int? Proyecto), Dictionary<int, Dictionary<int, ActorResultado>>>> PrevisualizarAsync(
            IReadOnlyCollection<FilaPrevisualizacion> filas);
    }

    /// <summary>De dónde salió el valor de un actor.</summary>
    public enum ActorOrigen
    {
        /// <summary>Lo personalizado para el trabajador en su ficha.</summary>
        Trabajador,

        /// <summary>Lo personalizado en Revisores de Áreas (ver <see cref="ActorResultado.NodoOrigen"/>).</summary>
        Area,

        /// <summary>Lo dedujo el sistema de la estructura (o no hay nadie).</summary>
        Algoritmo,

        /// <summary>El buzón de GTH, último recurso.</summary>
        Gth,
    }

    /// <summary>
    /// Una persona resuelta para un actor. Exactamente uno de <see cref="WorkerId"/> (una ficha) o
    /// <see cref="AreaScopeId"/> (el fallback de GTH, que es un buzón de área) tiene valor.
    /// </summary>
    public sealed record ActorPersona(
        int? WorkerId, int? AreaScopeId, int? PersonId, string Email, string? Nombre, int? CategoriaId);

    /// <summary>El valor de un actor para un trabajador (o una fila de la pantalla).</summary>
    public sealed class ActorResultado
    {
        public int ActorId { get; init; }

        /// <summary>
        /// false = el actor no existe para este caso (el jefe notificado solo existe para el staff).
        /// </summary>
        public bool Aplica { get; init; } = true;

        /// <summary>
        /// Quiénes son, en orden. En los actores de uno solo, a lo sumo uno. Vacía si no aplica, si no
        /// se resolvió a nadie o si hay <see cref="Descriptor"/>.
        /// </summary>
        public List<ActorPersona> Personas { get; init; } = new();

        /// <summary>
        /// Solo en la previsualización de una fila sin obra: el valor depende de la obra de cada
        /// trabajador ("Residente de la obra"), así que se describe la regla en vez de nombrar a nadie.
        /// </summary>
        public string? Descriptor { get; init; }

        public ActorOrigen Origen { get; init; } = ActorOrigen.Algoritmo;

        /// <summary>Con <see cref="ActorOrigen.Area"/>: el nodo donde está cargado lo que ganó.</summary>
        public int? NodoOrigen { get; init; }

        /// <summary>Con <see cref="ActorOrigen.Area"/>: la obra de esa carga (null = el área entera).</summary>
        public int? ProyectoOrigen { get; init; }

        /// <summary>
        /// Lo que valdría sin lo personalizado que manda: con <see cref="ActorOrigen.Trabajador"/>,
        /// sin lo de su ficha — lo que la ficha muestra al desmarcar "Personalizado"—; en la
        /// previsualización de una fila personalizada, sin lo de esa fila.
        /// </summary>
        public ActorResultado? SinPersonalizar { get; init; }

        /// <summary>Una copia con <see cref="SinPersonalizar"/>.</summary>
        public ActorResultado ConSinPersonalizar(ActorResultado? sinPersonalizar) => new()
        {
            ActorId         = ActorId,
            Aplica          = Aplica,
            Personas        = Personas,
            Descriptor      = Descriptor,
            Origen          = Origen,
            NodoOrigen      = NodoOrigen,
            ProyectoOrigen  = ProyectoOrigen,
            SinPersonalizar = sinPersonalizar,
        };
    }

    /// <summary>Los actores de un trabajador y el contexto con el que se resolvieron.</summary>
    public sealed class ActoresDeTrabajador
    {
        public int? WorkerId { get; init; }

        /// <summary>Nodo de su área (puesto.area_destino_scope_id). null = sin área.</summary>
        public int? AreaScopeId { get; init; }

        /// <summary><c>ActorCasoIds</c>: qué tipo de trabajador es.</summary>
        public int CasoId { get; init; }

        /// <summary>Su obra vigente (o la del formulario).</summary>
        public int? ProyectoId { get; init; }

        /// <summary>true = <see cref="ProyectoId"/> es una obra (no OFICINA CENTRAL).</summary>
        public bool EsObra { get; init; }

        /// <summary>actorId → valor.</summary>
        public Dictionary<int, ActorResultado> Actores { get; init; } = new();
    }

    /// <summary>
    /// Un trabajador descrito por sus datos y no por su ficha: el que se está creando o el que se
    /// está editando con otro puesto u otra obra.
    /// </summary>
    /// <param name="WorkerId">La ficha, si existe: su persona y sus personalizados guardados.</param>
    /// <param name="AreaScopeId">Nodo de su área (el del puesto elegido).</param>
    /// <param name="CategoriaId">Categoría del puesto elegido.</param>
    /// <param name="ProyectoId">La obra elegida.</param>
    public sealed record ContextoTrabajador(int? WorkerId, int? AreaScopeId, int? CategoriaId, int? ProyectoId);

    /// <summary>Una fila de Revisores de Áreas: un nodo, opcionalmente una obra, y los casos que muestra.</summary>
    public sealed record FilaPrevisualizacion(int Nodo, int? Proyecto, IReadOnlyCollection<int> Casos);
}
