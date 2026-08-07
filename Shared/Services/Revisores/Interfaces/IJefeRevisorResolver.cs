namespace Abril_Backend.Shared.Services.Revisores.Interfaces
{
    /// <summary>
    /// Resuelve el jefe/revisor de un trabajador:
    ///   1) Su jefe personalizado — el primer revisor vivo (state) y activo (active) del
    ///      trabajador en <c>workers_revisores</c>, por orden_prioridad ascendente, cuyo
    ///      worker tenga correo corporativo @abril.pe. Se asigna con el checkbox
    ///      "Jefe personalizado" del formulario de trabajadores (Gestión de Ingresos) y
    ///      se sobrepone al revisor del área.
    ///   2) Los revisores del área del trabajador en <c>area_revisores</c>
    ///      (/configuracion/revisores-areas): se parte
    ///      de su nodo workers.area_scope_id y se sube por el árbol hasta el primer
    ///      nodo con un revisor vivo + activo con correo válido (por prioridad).
    ///   3) Fallback: el área de GTH — nodo <c>area_scope</c> del área
    ///      "Gestión del Talento Humano" con <c>email</c> configurado.
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
        /// Previsualización por ÁREA, sin trabajador: para cada nodo <c>area_scope</c> pedido devuelve
        /// el revisor que le tocaría a un trabajador ubicado ahí, aplicando los pasos 2 y 3 (revisores
        /// del área subiendo por el árbol, y fallback GTH). No aplica el paso 1 (<c>workers_revisores</c>)
        /// porque no hay trabajador: un trabajador con revisor propio configurado usa ese y no el del área.
        ///
        /// La usa el formulario de trabajadores para mostrar, al elegir el área, quién quedaría como su
        /// revisor. Un número FIJO de consultas sea para 1 o para todos los nodos del árbol.
        /// </summary>
        Task<Dictionary<int, AreaScopeRevisorPreview>> ResolveByAreaScopeManyAsync(
            IReadOnlyCollection<int> areaScopeIds);
    }

    /// <summary>
    /// Revisor que le tocaría a un trabajador de un nodo del árbol de áreas. Se separa el caso sin
    /// proyecto del caso por proyecto porque hay nodos marcados como "filtrar por proyecto"
    /// (ga_salidas_area_config): ahí el revisor depende del proyecto del trabajador, así que se
    /// precalcula uno por proyecto configurado y el consumidor elige según el proyecto del formulario.
    /// </summary>
    /// <param name="Area">Revisor a nivel de área (o el fallback GTH). Null si no hay ninguno.</param>
    /// <param name="PorProyecto">projectId -> revisor, solo para los proyectos con revisor propio en la rama.</param>
    public record AreaScopeRevisorPreview(
        JefeRevisorResolution? Area,
        Dictionary<int, JefeRevisorResolution> PorProyecto);

    /// <summary>
    /// Jefe/revisor resuelto: un trabajador (WorkerId) o un área (AreaScopeId, el
    /// fallback de GTH) — exactamente uno de los dos — con el correo a usar y el nombre
    /// para mostrar (nombre completo del revisor, o el del área en el fallback).
    /// </summary>
    public record JefeRevisorResolution(int? WorkerId, int? AreaScopeId, string Email, string? Nombre = null);
}
