using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Application.Dtos
{
    /// <summary>Una fila por trabajador con correo @abril.pe en la lista de configuración.</summary>
    public class VisibilidadWorkerItemDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public int? CategoryId { get; set; }
        public string? Category { get; set; }
        /// <summary>Nodo area_scope al que pertenece el trabajador (para filtrar por área). null = sin área.</summary>
        public int? AreaScopeId { get; set; }
        /// <summary>Cuántos nodos area_scope tiene asignados (override). 0 = usa el algoritmo automático.</summary>
        public int AreasAsignadas { get; set; }

        /// <summary>
        /// Obras de las que la ficha es hoy residente o administrador: ve todo lo de sus
        /// trabajadores, con o sin override. Vacío = ninguna.
        /// </summary>
        public List<string> Obras { get; set; } = new();
    }

    /// <summary>Una obra que el trabajador ve entera por estar a cargo de ella.</summary>
    public class VisibilidadObraDto
    {
        public int ProjectId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        /// <summary>"Residente", "Administrador de obra" o los dos.</summary>
        public string Rol { get; set; } = string.Empty;
    }

    /// <summary>Carga inicial de la página: trabajadores (tabla) + árbol de áreas (filtro en cascada).</summary>
    public class VisibilidadInicialDto
    {
        public List<VisibilidadWorkerItemDto> Workers { get; set; } = new();
        public List<GaAreaNodeDto> AreaTree { get; set; } = new();
    }

    /// <summary>
    /// Una asignación de visibilidad: un nodo y si incluye sus descendientes.
    ///
    /// La pantalla de hoy manda siempre <see cref="IncluyeDescendientes"/> en false y el subárbol
    /// explícito (marcar un área marca sus subáreas), así que lo que se guarda es la lista de nodos
    /// tal cual se ve. El campo se conserva porque las filas cargadas antes lo tienen en true y el
    /// resolver las sigue expandiendo.
    /// </summary>
    public class VisibilidadAsignacionDto
    {
        public int AreaScopeId { get; set; }
        public bool IncluyeDescendientes { get; set; }
    }

    /// <summary>
    /// Lo que los modales de un trabajador necesitan saber de él, en una sola llamada: lo que tiene
    /// cargado a mano y lo que REALMENTE ve hoy.
    ///
    /// Van los dos juntos porque los modales los combinan distinto: el de detalle muestra siempre
    /// <see cref="Efectivas"/> (la pregunta es "¿qué ve?", no "¿qué le cargaron?") y el de edición
    /// parte de <see cref="Asignaciones"/> si las hay y, si no, de <see cref="Efectivas"/>, para que
    /// el trabajador sin configuración propia no abra el modal en blanco sino sobre lo que hoy
    /// resuelve el algoritmo.
    /// </summary>
    public class VisibilidadWorkerDetalleDto
    {
        /// <summary>Override vivo del trabajador en este ámbito. Vacío = lo resuelve el algoritmo.</summary>
        public List<VisibilidadAsignacionDto> Asignaciones { get; set; } = new();

        /// <summary>
        /// Nodos <c>area_scope</c> que el trabajador ve hoy: el override si lo tiene y, si no, lo que
        /// deduce el algoritmo de jerarquía. En los dos casos se le suman las ramas donde está
        /// designado revisor o consolidador, que ve siempre.
        /// </summary>
        public List<int> Efectivas { get; set; } = new();

        /// <summary>true = lo de <see cref="Efectivas"/> nace de un override cargado a mano.</summary>
        public bool EsPersonalizado { get; set; }

        /// <summary>true = ve TODO sin recorte por área (hoy, el personal de GTH).</summary>
        public bool VeTodo { get; set; }

        /// <summary>
        /// Obras de las que es residente o administrador: ve a todos sus trabajadores, sea cual sea
        /// su área. Se suma a <see cref="Efectivas"/> y ni el override la quita: sale de
        /// <c>project</c> en vivo, así que cambia sola cuando cambia quien ocupa el puesto.
        /// </summary>
        public List<VisibilidadObraDto> Obras { get; set; } = new();
    }

    /// <summary>Cuerpo del PUT: reemplaza el conjunto completo de asignaciones del trabajador.</summary>
    public class VisibilidadUpdateDto
    {
        public List<VisibilidadAsignacionDto> Areas { get; set; } = new();
    }
}
