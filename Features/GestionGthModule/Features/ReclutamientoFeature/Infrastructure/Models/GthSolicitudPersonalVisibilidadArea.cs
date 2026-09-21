namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Configuración propia de «qué áreas ve este trabajador» en Solicitud de Personal (tabla
    /// <c>gth_solicitud_personal_visibilidad_area</c>): una fila viva por trabajador y nodo de
    /// <c>area_scope</c>. Se administra desde Solicitud de Personal → Configuración → Visibilidad.
    ///
    /// Si la ficha tiene al menos una fila viva, esas áreas reemplazan al algoritmo de
    /// <c>SolicitudPersonalScopeResolver</c> (su área y lo que cuelga de ella; GTH y el Gerente
    /// General ven todo). Lo que el usuario registró él mismo lo sigue viendo siempre.
    ///
    /// Es una tabla del módulo y no una fila más de <c>ga_visibilidad_area</c>: aquella es de
    /// Gestión Administrativa y su algoritmo es otro. El subárbol se guarda explícito (marcar un
    /// área marca sus subáreas), así que no hay columna de «incluye descendientes».
    /// </summary>
    public class GthSolicitudPersonalVisibilidadArea
    {
        public int GthSolicitudPersonalVisibilidadAreaId { get; set; }

        /// <summary>Ficha (<c>workers.id</c>) a la que se le concede la visibilidad.</summary>
        public int WorkerId { get; set; }

        /// <summary>Nodo del árbol de áreas cuyos requerimientos ve (<c>area_scope.area_scope_id</c>).</summary>
        public int AreaScopeId { get; set; }

        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }

        /// <summary>Soft delete: false = área quitada (la fila queda para auditoría).</summary>
        public bool State { get; set; } = true;
    }
}
