namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Requerimiento independiente por vacante (tabla <c>gth_requerimiento</c>). Cada vacante de una
    /// <see cref="GthSolicitud"/> genera uno, con su propio código <c>REQ-AAAA-NNNN</c> y su propio
    /// seguimiento. Guarda el puesto, tipo (Nuevo/Reemplazo), proyecto/obra, fecha requerida de
    /// ingreso y estado actual.
    /// </summary>
    public class GthRequerimiento
    {
        public int GthRequerimientoId { get; set; }

        public int GthSolicitudId { get; set; }
        public GthSolicitud? Solicitud { get; set; }

        /// <summary>Código único del requerimiento en formato <c>REQ-AAAA-NNNN</c>.</summary>
        public string Codigo { get; set; } = null!;

        /// <summary>Año del código (AAAA) — usado para el correlativo anual.</summary>
        public int Anio { get; set; }

        /// <summary>Correlativo (NNNN) dentro del año.</summary>
        public int Numero { get; set; }

        public int GthPuestoId { get; set; }
        public int GthTipoRequerimientoId { get; set; }

        /// <summary>FK a <c>project</c> (proyecto/obra destino de la vacante).</summary>
        public int ProjectId { get; set; }

        public DateOnly FechaRequeridaIngreso { get; set; }

        public int GthEstadoRequerimientoId { get; set; }

        /// <summary>FK a <c>gth_prioridad</c> (Alta/Media/Baja). Null = sin prioridad asignada.</summary>
        public int? GthPrioridadId { get; set; }

        /// <summary>FK a <c>gth_responsable_proceso</c> (miembro GTH responsable). Null = sin asignar.</summary>
        public int? GthResponsableProcesoId { get; set; }

        /// <summary>FK a <c>gth_tipo_proceso</c> (Junior/Semisenior/Senior con su SLA). Null = sin clasificar.</summary>
        public int? GthTipoProcesoId { get; set; }

        /// <summary>FK a <c>contributor</c>: razón social activa asignada por GTH para la contratación.</summary>
        public int? ContributorId { get; set; }

        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
