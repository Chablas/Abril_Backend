namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Candidato de la long list de un requerimiento (tabla <c>gth_candidato</c>). GTH carga los
    /// CVs previamente filtrados y, por cada uno, registra el nombre, el puesto detectado, el
    /// tiempo de experiencia, la disponibilidad, la fuente de reclutamiento y un comentario
    /// interno; el CV (y un informe opcional) se suben a SharePoint. Estos datos los llena hoy
    /// GTH manualmente y en el futuro los prellenará una IA a partir del CV (con corrección
    /// manual como respaldo). El solicitante los revisa desde su vista para aprobar/rechazar.
    /// </summary>
    public class GthCandidato
    {
        public int GthCandidatoId { get; set; }

        /// <summary>FK al requerimiento (vacante) al que pertenece la long list.</summary>
        public int GthRequerimientoId { get; set; }

        /// <summary>Nombre y apellido del candidato (lo captura/corrige GTH; a futuro lo prellena la IA).</summary>
        public string Nombre { get; set; } = null!;

        /// <summary>Puesto detectado en el CV (texto libre; para que el solicitante lo vea en la revisión). Null si no se determinó.</summary>
        public string? Puesto { get; set; }

        /// <summary>Tiempo de experiencia en años (preferiblemente). Null si no se determinó.</summary>
        public int? ExperienciaAnios { get; set; }

        /// <summary>Disponibilidad declarada del candidato (texto libre: "15 días", "Inmediata"…). Null si no se determinó.</summary>
        public string? Disponibilidad { get; set; }

        /// <summary>FK a <c>gth_canal_publicacion</c>: fuente de reclutamiento del candidato. Null si no se indicó.</summary>
        public int? GthCanalPublicacionId { get; set; }

        /// <summary>Comentario interno de GTH sobre el candidato.</summary>
        public string? Comentario { get; set; }

        // ── CV (obligatorio) subido a SharePoint ──────────────────────────────
        public string? CvNombre { get; set; }
        public string? CvUrl { get; set; }
        public string? CvItemId { get; set; }
        public string? CvDriveId { get; set; }

        // ── Informe (opcional) subido a SharePoint ────────────────────────────
        public string? InformeNombre { get; set; }
        public string? InformeUrl { get; set; }
        public string? InformeItemId { get; set; }
        public string? InformeDriveId { get; set; }

        /// <summary>FK a <c>gth_candidato_estado</c>: estado de revisión (PENDIENTE por defecto).</summary>
        public int GthCandidatoEstadoId { get; set; }

        /// <summary>Orden del candidato dentro de la long list (posición de carga).</summary>
        public int Orden { get; set; }

        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
