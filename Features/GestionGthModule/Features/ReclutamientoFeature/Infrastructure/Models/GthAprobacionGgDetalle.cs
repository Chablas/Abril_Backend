namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Decisión de Gerencia General sobre una vacante concreta de la solicitud
    /// (tabla <c>gth_aprobacion_gg_detalle</c>): una fila por requerimiento de la
    /// <see cref="GthAprobacionGg"/>.
    ///
    /// Es el registro de auditoría de lo que el GG decidió, aparte del estado que el requerimiento
    /// vaya tomando después (una vacante aprobada sigue avanzando por el pipeline, así que su
    /// estado deja de reflejar esta decisión).
    /// </summary>
    public class GthAprobacionGgDetalle
    {
        public int GthAprobacionGgDetalleId { get; set; }

        public int GthAprobacionGgId { get; set; }
        public int GthRequerimientoId { get; set; }

        /// <summary>true = aprobada; false = rechazada; null = el GG todavía no decide.</summary>
        public bool? Aprobado { get; set; }

        public DateTimeOffset? DecididoDateTime { get; set; }

        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
