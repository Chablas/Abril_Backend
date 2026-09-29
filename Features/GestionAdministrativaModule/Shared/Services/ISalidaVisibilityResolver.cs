using Abril_Backend.Shared.Services.Jerarquia;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Resuelve el alcance de visibilidad de un usuario en una de las bandejas del flujo: qué nodos
    /// <c>area_scope</c> puede ver y de qué obras ve todo (además de lo suyo y de lo que le toca
    /// decidir). El ámbito (<c>Abril_Backend.Shared.Constants.VisibilidadAmbitoIds</c>) dice de
    /// cuál se está preguntando: Gestión de Salidas, Gestión de Rendiciones o Consolidados.
    ///
    /// Comparten el algoritmo de jerarquía pero NO la configuración: cada una tiene sus propias
    /// filas de override en <c>ga_visibilidad_area</c>, porque ver todas las solicitudes de salida y
    /// ver todas las planillas de rendición son dos permisos distintos.
    ///
    /// Al conjunto siempre se le suman los nodos (con su subárbol) donde el usuario está designado
    /// como revisor de área (<c>area_revisores</c>) y, en rendiciones y consolidados, también donde
    /// está designado como consolidador (<c>area_consolidadores</c>): a esas ramas les tiene que
    /// poder hacer el trabajo que se le asignó. Sobre ese piso, primero manda el override manual y,
    /// si el usuario no tiene ninguna asignación en ese ámbito, el algoritmo de jerarquía.
    ///
    /// Y un piso más, por OBRA (2026-09-22): el residente y el administrador de obra de un proyecto
    /// ven todo lo de los trabajadores de esa obra, en los tres ámbitos y aunque tengan override. Es
    /// el algoritmo el que les pide aprobar la salida (residente), revisar la planilla
    /// (administrador) y firmar el consolidado (los dos), y lo hace leyendo <c>project</c> en vivo;
    /// sin este piso, cuando cambiaba el residente, el nuevo tenía que firmar documentos que no
    /// podía ver.
    /// </summary>
    public interface ISalidaVisibilityResolver
    {
        /// <param name="ambitoId">Ver <c>VisibilidadAmbitoIds</c>: Salidas o Rendiciones.</param>
        Task<SalidaVisibility> ResolveAsync(int userId, int ambitoId);

        /// <summary>
        /// Lo mismo pero para UNA ficha de <c>workers</c> en vez de para un usuario, que es como
        /// lo pide Configuración &#8594; Visibilidad: ahí cada fila es un trabajador y el override
        /// se guarda por <c>worker_id</c>, así que la previsualización tiene que ser de esa ficha y
        /// no del conjunto de fichas de su persona.
        ///
        /// Sale del MISMO recorrido que <see cref="ResolveAsync"/>, así que el modal no puede
        /// mostrar un alcance distinto del que va a tener la bandeja.
        /// </summary>
        Task<SalidaVisibility> ResolveByWorkerAsync(int workerId, int ambitoId);
    }

    /// <summary>
    /// Resultado de la resolución de visibilidad.
    ///   • <see cref="SeesAll"/> = true  → ve TODAS las solicitudes (sin restricción por área).
    ///   • <see cref="AreaScopeIds"/>     → conjunto de nodos cuyos trabajadores puede ver.
    ///   • <see cref="Obras"/>            → obras de las que es residente o administrador.
    ///   • <see cref="TrabajadoresDeSusObras"/> → trabajadores cuya obra vigente es una de esas:
    ///     sus salidas se ven enteras, sea cual sea su área.
    /// </summary>
    public record SalidaVisibility(bool SeesAll, HashSet<int> AreaScopeIds)
    {
        public IReadOnlyList<ObrasLoader.ObraACargo> Obras { get; init; } =
            Array.Empty<ObrasLoader.ObraACargo>();

        /// <summary>Vacío cuando <see cref="SeesAll"/>: no hace falta recortar nada.</summary>
        public HashSet<int> TrabajadoresDeSusObras { get; init; } = new();
    }
}
