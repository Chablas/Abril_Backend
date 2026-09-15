using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Override manual de visibilidad por área: define, por trabajador y por
    /// <see cref="AmbitoId"/>, qué nodo <c>area_scope</c> puede "ver" (es decir, ver lo de los
    /// trabajadores que pertenecen a ese nodo). Si un trabajador no tiene ninguna fila viva para
    /// ese ámbito, su visibilidad la resuelve el algoritmo de jerarquía
    /// (<c>SalidaVisibilityResolver</c>: GTH ve todo, Gerente su gerencia, Administración de Obra
    /// las áreas con personal de Obra/Staff).
    ///
    /// Antes era <c>ga_salida_visibilidad_area</c> y solo cubría Gestión de Salidas. Se renombró a
    /// <c>ga_visibilidad_area</c> con un ámbito porque Gestión de Rendiciones necesita el suyo,
    /// independiente: ver todas las solicitudes de salida y ver todas las planillas de rendición
    /// son dos permisos distintos.
    ///
    /// <see cref="IncluyeDescendientes"/> se conserva por las filas viejas: la pantalla de hoy
    /// guarda el subárbol explícito (marcar un área marca sus subáreas) y escribe siempre false,
    /// pero el resolver sigue expandiendo las filas antiguas que lo tienen en true.
    /// </summary>
    [Table("ga_visibilidad_area")]
    public class GaVisibilidadArea
    {
        [Column("id")]
        public int Id { get; set; }

        /// <summary>Ámbito (<c>ga_visibilidad_ambito</c>): salidas o rendiciones.</summary>
        [Column("ambito_id")]
        public int AmbitoId { get; set; }

        /// <summary>Trabajador (workers.id) al que se le concede la visibilidad.</summary>
        [Column("worker_id")]
        public int WorkerId { get; set; }

        /// <summary>Nodo del árbol de áreas que puede ver (area_scope.area_scope_id).</summary>
        [Column("area_scope_id")]
        public int AreaScopeId { get; set; }

        /// <summary>Si true, también ve todos los nodos descendientes de este nodo.</summary>
        [Column("incluye_descendientes")]
        public bool IncluyeDescendientes { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        /// <summary>Soft delete: false = eliminado (se conserva para histórico).</summary>
        [Column("state")]
        public bool State { get; set; } = true;
    }
}
