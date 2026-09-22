namespace Abril_Backend.Shared.Services.Revisores.Interfaces
{
    /// <summary>
    /// Resuelve el jefe/revisor de un trabajador:
    ///   1) Su jefe personalizado — el primer revisor vivo (state) y activo (active) del
    ///      trabajador en <c>workers_revisores</c>, por orden_prioridad ascendente, cuyo
    ///      worker tenga correo corporativo @abril.pe. Se asigna con el checkbox
    ///      "Jefe personalizado" del formulario de trabajadores (Gestión de Ingresos) y
    ///      se sobrepone al revisor del área. Puede ser el propio trabajador.
    ///   2) El revisor de su área: se parte de su nodo puesto.area_destino_scope_id y se sube
    ///      por el árbol hasta el primer nodo que resuelva. En CADA nodo se mira, en este orden:
    ///        a) lo asignado a mano en <c>area_revisores</c> (/configuracion/revisores-areas),
    ///           primero lo del proyecto del trabajador y después lo del área (project_id NULL),
    ///           que por eso vale para todos los proyectos sin asignación propia;
    ///        b) el ALGORITMO, que deduce el revisor de la estructura sin que nadie lo cargue:
    ///           el residente del proyecto (<c>project.residente_workers_id</c>) si el nodo filtra
    ///           por proyecto y ese proyecto es una obra; en cualquier otro caso el Jefe del área
    ///           —o el Gerente, si el nodo es "Área de Gerencia"— cuyo puesto apunta a ese nodo.
    ///   3) Fallback: el área de GTH — nodo <c>area_scope</c> del área
    ///      "Gestión del Talento Humano" con <c>email</c> configurado.
    ///
    /// El algoritmo no reemplaza a lo asignado a mano: va DEBAJO. Configuración → Revisores de
    /// Áreas sigue sirviendo para sobreponerse a él, por área o por área+proyecto, y cada revisor
    /// resuelto dice de dónde salió en <see cref="JefeRevisorResolution.Origen"/>.
    ///
    /// <b>Nadie puede ser su propio jefe</b> rige en el paso 2, donde al revisor no lo elige
    /// nadie sino que lo deriva el área: un candidato que es el propio trabajador se descarta y la
    /// búsqueda sigue — con el siguiente revisor del mismo nodo si lo hay y, si no, subiendo al
    /// <c>area_scope</c> padre (normalmente la gerencia de la que cuelga su área). Esto es lo
    /// normal en los jefes de área: el jefe de SSOMA es el revisor de SSOMA, así que su propio
    /// jefe es el gerente del que depende esa área. La comparación es por PERSONA, no por ficha:
    /// un reingreso deja varias filas en <c>workers</c> para la misma persona y el revisor puede
    /// estar configurado en cualquiera.
    ///
    /// En el paso 1 NO rige: el jefe personalizado se elige a mano en el formulario de
    /// trabajadores, que ofrece al propio trabajador como opción, y esa elección se respeta.
    ///
    /// Servicio compartido: es la ÚNICA fuente de "quién es el jefe de este trabajador".
    /// Lo usan Gestión Administrativa (a quién se le manda a aprobar una solicitud de
    /// salida) y SSOMA · Salud Ocupacional (correos de EMO e interconsultas), y
    /// Evaluaciones (a qué jefe se le hace CC del recordatorio). Reemplazó a tres
    /// algoritmos previos: el cruce por nombre contra <c>cat_jefatura</c>, el campo 1:1
    /// <c>workers.worker_salida_jefe_id</c> y el recorrido del árbol de áreas por
    /// categoría de trabajador (ApproverResolver / JefeResolver).
    /// </summary>
    public interface IJefeRevisorResolver
    {
        /// <summary>Jefe/revisor de un trabajador, o null si no se resuelve ninguno.</summary>
        Task<JefeRevisorResolution?> ResolveAsync(int workerId);

        /// <summary>
        /// Versión por lotes: resuelve el jefe de varios trabajadores con un número FIJO de
        /// consultas (no depende de la cantidad de ids), para listas y envíos masivos.
        /// Los trabajadores sin jefe resuelto simplemente no aparecen en el diccionario.
        /// </summary>
        Task<Dictionary<int, JefeRevisorResolution>> ResolveManyAsync(IReadOnlyCollection<int> workerIds);

        /// <summary>
        /// Previsualización por ÁREA: para cada nodo <c>area_scope</c> pedido devuelve el revisor
        /// que le tocaría a un trabajador ubicado ahí, aplicando los pasos 2 y 3 (revisores del área
        /// subiendo por el árbol, y fallback GTH). No aplica el paso 1 (<c>workers_revisores</c>) a
        /// propósito: el formulario de trabajadores compone ese paso por su cuenta —muestra este
        /// revisor cuando el checkbox "Jefe personalizado" está desmarcado y el elegido a mano
        /// cuando está marcado—, que es exactamente lo que hará <see cref="ResolveAsync"/>.
        ///
        /// Es la MISMA elección que hace <see cref="ResolveManyAsync"/>, no una parecida: las dos
        /// salen del mismo ranking interno, así que la previsualización no puede mostrar a alguien
        /// distinto de quien va a recibir el correo. Lo único que cambia es de dónde sale el
        /// contexto: acá lo pone quien pregunta (un nodo y, por cada proyecto, su respuesta), y en
        /// la resolución por trabajador sale de su puesto y su vinculación vigente.
        ///
        /// Se devuelve un solo ganador por combinación y no la lista de candidatos: elegir es parte
        /// del algoritmo y vive de este lado. Por eso hace falta <paramref name="workerId"/>, para
        /// poder aplicar "nadie puede ser su propio jefe" — sin él se devuelve el ranking completo
        /// sin descartar a nadie, que es lo correcto para un trabajador que aún no existe.
        ///
        /// Es la previsualización del ámbito SALIDAS, donde la solicitud la aprueba UNA persona. La
        /// de Rendiciones es <see cref="ResolveAprobadoresByAreaScopeManyAsync"/>: ahí intervienen
        /// varios a la vez y devolver un ganador escondería a la mitad.
        ///
        /// El árbol se pide una sola vez y cubre toda la pantalla, sin volver al servidor al cambiar
        /// de puesto. Un número FIJO de consultas sea para 1 o para todos los nodos del árbol.
        /// </summary>
        /// <param name="areaScopeIds">Nodos a previsualizar.</param>
        /// <param name="workerId">
        /// Trabajador que se está editando, para descartarlo de sus propios candidatos. Null al
        /// crear uno nuevo (no hay a quién descartar).
        /// </param>
        Task<Dictionary<int, AreaScopeRevisorPreview>> ResolveByAreaScopeManyAsync(
            IReadOnlyCollection<int> areaScopeIds, int? workerId = null);

        /// <summary>
        /// Previsualización por ÁREA de RENDICIONES: para cada nodo <c>area_scope</c> pedido (y, en
        /// los que filtran por proyecto, para cada proyecto activo) TODOS los que tendrían que
        /// aprobar la planilla o firmar el consolidado de un trabajador ubicado ahí, con la casilla
        /// de cada paso ya resuelta.
        ///
        /// Es el gemelo de <see cref="ResolveByAreaScopeManyAsync"/> para la otra sección de
        /// Revisores de Áreas, y existe porque acá <b>no gana uno solo</b>: en una obra el
        /// administrador revisa la planilla y firma, y el residente solo firma, así que un único
        /// ganador dejaba al residente fuera de la pantalla aunque el sistema fuera a pedirle la
        /// firma. Sale de <see cref="ResolveAprobadoresDeDocumentoAsync"/> —el mismo recorrido,
        /// corrido una vez por paso— sin ningún trabajador dentro al que haya que saltarse, así que
        /// la pantalla no puede mostrar a alguien distinto de quien va a tener que aprobar.
        /// </summary>
        Task<Dictionary<int, AreaScopeAprobadoresPreview>> ResolveAprobadoresByAreaScopeManyAsync(
            IReadOnlyCollection<int> areaScopeIds);

        /// <summary>
        /// El <b>jefe del área</b> de un trabajador: el mismo paso 2 de <see cref="ResolveAsync"/>
        /// pero <b>ignorando su proyecto</b>, o sea lo que la sección Revisores muestra en la fila
        /// del área cuando no se filtra por obra — el revisor asignado a nivel de área si lo hay y,
        /// si no, el Jefe del área (o el Gerente de la gerencia de la que cuelga, subiendo por el
        /// árbol). Nunca el residente de la obra, que es justamente lo que se quiere saltar.
        ///
        /// Lo pide Solicitud de Salidas para el aviso informativo que sale cuando el revisor de la
        /// solicitud es un <c>CategoriaIds.Residente</c>: la salida la aprueba el residente y el jefe
        /// del área solo se entera.
        ///
        /// No aplica el paso 1 (<c>workers_revisores</c>) ni el fallback de GTH: el jefe
        /// personalizado es la jefatura de UNA persona y no la del área, y un área que no resuelve
        /// jefe no tiene a quién informar —mandarlo a GTH convertiría un aviso de jefatura en ruido
        /// para el buzón de siempre—. Sí aplica "nadie puede ser su propio jefe", igual que el
        /// paso 2: un trabajador que es el jefe de su área recibe el aviso de su gerencia.
        /// </summary>
        Task<JefeRevisorResolution?> ResolveJefeDeAreaAsync(int workerId);

        /// <summary>
        /// Versión por lotes de <see cref="ResolveJefeDeAreaAsync"/>, con un número FIJO de
        /// consultas. Los trabajadores cuya área no resuelve ningún jefe no aparecen en el
        /// diccionario.
        /// </summary>
        Task<Dictionary<int, JefeRevisorResolution>> ResolveJefeDeAreaManyAsync(
            IReadOnlyCollection<int> workerIds);

        /// <summary>
        /// Quién aprueba y firma un DOCUMENTO que agrupa a varios trabajadores: la planilla de
        /// rendición (1.ª revisión) y el Consolidado del S10 con su planilla grupal. Es UNA persona
        /// para el documento entero, y por eso no se puede derivar de <see cref="ResolveManyAsync"/>,
        /// que da un revisor por trabajador y dejaría un documento con dos dueños.
        ///
        /// La regla, en orden (2026-09-21):
        ///   1. si TODOS los trabajadores tienen el MISMO jefe personalizado (<c>workers_revisores</c>),
        ///      ese firma — y es el único caso en que el firmante puede estar DENTRO del documento,
        ///      porque se eligió a mano ficha por ficha;
        ///   2. si no, el revisor del área (lo asignado en Revisores de Áreas o lo que deduce el
        ///      algoritmo), resuelto desde el nodo común del grupo hacia la raíz y saltando a
        ///      cualquiera que esté incluido en el documento;
        ///   3. a una jefatura la firma su gerencia, igual que en la resolución por trabajador.
        ///
        /// En un área marcada "filtrar por proyecto" manda la OBRA —el administrador de obra revisa
        /// y firma, el residente firma detrás— siempre que el documento sea de una sola: con obras
        /// mezcladas no hay una a quién dárselo y vuelve a responder el área. Hasta el 2026-09-21
        /// hacía falta además el checkbox "Firma por obra"
        /// (<c>ga_salidas_area_config.firma_consolidado_por_proyecto</c>), que se quitó: con él
        /// apagado el administrador no intervenía en nada y firmaba el Jefe del área.
        /// </summary>
        /// <returns>
        /// Los aprobadores en ORDEN. Vacio si la rama no resuelve ninguno ni llega al fallback de
        /// GTH. Casi siempre es uno solo; en obra son dos (administrador y residente).
        /// </returns>
        Task<List<AprobadorDocumento>> ResolveAprobadoresDeDocumentoAsync(
            IReadOnlyCollection<int> workerIds, PasoAprobacion paso);

        /// <summary>
        /// <see cref="ResolveFirmanteDeDocumentoAsync"/> para varios documentos de una vez, con un
        /// número FIJO de consultas: el listado resuelve decenas de consolidados por página y
        /// hacerlo de a uno sería un N+1 en la pantalla.
        /// </summary>
        /// <param name="workersPorDocumento">
        /// id del documento (consolidado o planilla) → los trabajadores que agrupa. La clave la
        /// pone el llamador y solo sirve para devolverle el resultado indexado.
        /// </param>
        Task<Dictionary<int, List<AprobadorDocumento>>> ResolveAprobadoresDeDocumentosAsync(
            IReadOnlyDictionary<int, IReadOnlyCollection<int>> workersPorDocumento,
            PasoAprobacion paso);
    }

    /// <summary>
    /// Cual de los dos pasos del ciclo se esta resolviendo. No todos los aprobadores intervienen en
    /// los dos: en obra el administrador revisa la planilla solo el, pero el consolidado lo firman
    /// el administrador y el residente.
    /// </summary>
    public enum PasoAprobacion
    {
        /// <summary>La primera revision de la planilla (Gestion de Rendiciones).</summary>
        PrimeraRevision,

        /// <summary>La firma del Consolidado del S10 y su planilla grupal (Consolidados).</summary>
        Consolidado,
    }

    /// <summary>
    /// Uno de los que tiene que aprobar un documento, con el lugar que ocupa.
    /// </summary>
    /// <param name="Orden">
    /// 1 = primero. Con varios aprobadores el orden IMPORTA: nadie firma antes que quien lo
    /// precede, para que el residente no pueda firmar un consolidado que el administrador de obra
    /// todavia no vio.
    /// </param>
    public record AprobadorDocumento(JefeRevisorResolution Persona, int Orden);

    /// <summary>
    /// Revisor que le tocaría a un trabajador de un nodo del árbol de áreas. Se separa el caso sin
    /// proyecto del caso por proyecto porque hay nodos marcados como "filtrar por proyecto"
    /// (ga_salidas_area_config): ahí el revisor depende del proyecto del trabajador, así que se
    /// precalcula uno por proyecto configurado y el consumidor indexa por el proyecto que tenga a
    /// la vista, sin decidir nada.
    /// </summary>
    /// <param name="Area">Revisor a nivel de área (el que aplica cuando el trabajador no tiene proyecto).</param>
    /// <param name="PorProyecto">projectId -> revisor, solo para los proyectos con revisor propio en la rama.</param>
    public record AreaScopeRevisorPreview(
        RevisorElegido Area,
        Dictionary<int, RevisorElegido> PorProyecto);

    /// <summary>
    /// Lo mismo que <see cref="AreaScopeRevisorPreview"/> para RENDICIONES, con la única diferencia
    /// que importa: cada combinación (nodo, proyecto) devuelve la LISTA de los que intervienen y no
    /// un ganador.
    /// </summary>
    /// <param name="Area">Aprobadores a nivel de área (los que aplican sin obra de por medio).</param>
    /// <param name="PorProyecto">projectId -> aprobadores, solo si algún nodo de la rama filtra por proyecto.</param>
    public record AreaScopeAprobadoresPreview(
        List<AprobadorDeArea> Area,
        Dictionary<int, List<AprobadorDeArea>> PorProyecto);

    /// <summary>
    /// Uno de los que intervienen en el ciclo de la rendición de un área, con en cuál de los dos
    /// pasos lo hace. Las dos banderas son el resultado de resolver el paso, no lo que alguien
    /// marcó: en una obra el administrador sale con los dos en true y el residente solo con
    /// <paramref name="ApruebaConsolidado"/>, sin que nadie haya cargado una fila.
    /// </summary>
    public record AprobadorDeArea(
        JefeRevisorResolution Persona, bool ApruebaPrimeraRevision, bool ApruebaConsolidado);

    /// <summary>
    /// El revisor ya elegido para una combinación (nodo, proyecto).
    /// </summary>
    /// <param name="Revisor">El ganador, o null si la rama no tiene ninguno ni llega al fallback de GTH.</param>
    /// <param name="EsRevisorDeSuPropiaArea">
    /// True cuando el primer candidato de la rama era el propio trabajador y por eso
    /// <paramref name="Revisor"/> es el siguiente. Lo normal en los jefes de área, que son el
    /// revisor de su propia área: el formulario lo avisa para que no se lea como un error de
    /// configuración de Revisores de Áreas. Siempre false cuando no se pidió con un trabajador.
    /// </param>
    public record RevisorElegido(JefeRevisorResolution? Revisor, bool EsRevisorDeSuPropiaArea);

    /// <summary>
    /// Jefe/revisor resuelto: un trabajador (WorkerId) o un área (AreaScopeId, el
    /// fallback de GTH) — exactamente uno de los dos — con el correo a usar y el nombre
    /// para mostrar (nombre completo del revisor, o el del área en el fallback).
    /// </summary>
    /// <param name="PersonId">
    /// Persona del revisor (<c>workers.person_id</c>), null en el fallback de área. Es con lo que se
    /// aplica "nadie puede ser su propio jefe": la misma persona puede tener varias fichas en
    /// <c>workers</c> y comparar solo por ficha dejaría pasar el caso.
    /// </param>
    /// <param name="CategoriaId">
    /// Categoría del puesto del revisor (<c>workers.puesto_id → puesto.categoria_id</c>), null en el
    /// fallback de área y en las fichas sin puesto. Viene resuelta para que quien consume no tenga
    /// que volver a la base a preguntar QUÉ es el que salió elegido: Solicitud de Salidas la mira
    /// para avisarle al jefe del área cuando el revisor es <c>CategoriaIds.Residente</c>, y la regla
    /// es sobre la categoría —no sobre de dónde salió el revisor—, así que también alcanza al
    /// residente puesto a mano en Revisores o como jefe personalizado.
    /// </param>
    public record JefeRevisorResolution(
        int? WorkerId, int? AreaScopeId, string Email, string? Nombre = null, int? PersonId = null,
        RevisorOrigen Origen = RevisorOrigen.Personalizado, int? CategoriaId = null);

    /// <summary>
    /// De dónde salió un revisor, <b>visto desde el área por la que se preguntó</b>. Lo pide
    /// Configuración → Revisores de Áreas, que muestra el revisor efectivo de cada área/proyecto y
    /// tiene que decir si lo puso una persona ahí o si lo decidió el sistema.
    ///
    /// Es relativo a propósito: un revisor asignado a mano en la GERENCIA, que a un área de más
    /// abajo le llega porque el algoritmo sube por el árbol, es <see cref="Algoritmo"/> para esa
    /// área — nadie configuró esa área, el sistema fue a buscarlo. Etiquetarlo como
    /// <see cref="Personalizado"/> hacía leer la fila como si tuviera un revisor propio.
    /// </summary>
    public enum RevisorOrigen
    {
        /// <summary>
        /// Alguien lo asignó a mano <b>para esta área</b>: una fila de <c>area_revisores</c> de este
        /// mismo <c>area_scope</c> (la del proyecto, o la del área que vale para todos sus proyectos
        /// sin asignación propia). También el jefe personalizado de un trabajador
        /// (<c>workers_revisores</c>), que es una elección explícita sobre esa persona.
        /// </summary>
        Personalizado = 0,

        /// <summary>
        /// Lo resolvió el sistema: lo dedujo de la estructura —el residente del proyecto
        /// (<c>project.residente_workers_id</c>) o el Jefe/Gerente del área— o subió por el árbol
        /// hasta la configuración de otra área.
        /// </summary>
        Algoritmo = 1,

        /// <summary>Nadie: se cayó al correo del área de GTH.</summary>
        Gth = 2,
    }
}
