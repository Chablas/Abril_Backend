namespace Abril_Backend.Shared.Services.Revisores.Interfaces
{
    /// <summary>
    /// Resuelve el jefe/revisor de un trabajador a partir de la configuración global
    /// ("Revisores de Trabajadores" y "Revisores de Áreas" en /configuracion):
    ///   1) El primer revisor vivo (state) y activo (active) del trabajador en
    ///      <c>workers_revisores</c>, por orden_prioridad ascendente, cuyo worker
    ///      tenga correo corporativo @abril.pe.
    ///   2) Los revisores del área del trabajador en <c>area_revisores</c>: se parte
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
    }

    /// <summary>
    /// Jefe/revisor resuelto: un trabajador (WorkerId) o un área (AreaScopeId, el
    /// fallback de GTH) — exactamente uno de los dos — con el correo a usar y el nombre
    /// para mostrar (nombre completo del revisor, o el del área en el fallback).
    /// </summary>
    public record JefeRevisorResolution(int? WorkerId, int? AreaScopeId, string Email, string? Nombre = null);
}
