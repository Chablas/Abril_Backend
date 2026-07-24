namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Panel de la vista del solicitante ("Solicitud de Personal"), en una sola petición:
    /// las tarjetas de "Gestión de candidatos" (long lists que GTH ya le envió para revisar)
    /// y la tabla "Mis solicitudes de vacante".
    /// </summary>
    public class SolicitantePanelDto
    {
        /// <summary>Tarjetas "Long list enviada por GTH" pendientes de revisión del solicitante.</summary>
        public List<GestionCandidatoCardDto> GestionCandidatos { get; set; } = new();

        /// <summary>Filas de la tabla "Mis solicitudes de vacante".</summary>
        public List<SolicitudVacanteListItemDto> MisSolicitudes { get; set; } = new();
    }

    /// <summary>
    /// Tarjeta de "Gestión de candidatos": un requerimiento del solicitante cuya long list ya
    /// fue enviada por GTH (estado LONG_LIST_ENVIADA) y está pendiente de su revisión.
    /// </summary>
    public class GestionCandidatoCardDto
    {
        public int RequerimientoId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Puesto { get; set; } = string.Empty;
        public string? Area { get; set; }
        public string? ProyectoObra { get; set; }

        /// <summary>Cantidad de candidatos que GTH cargó en la long list.</summary>
        public int TotalCandidatos { get; set; }

        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;
    }

    /// <summary>
    /// Revisión de la long list de un requerimiento (modal del solicitante): cabecera del
    /// requerimiento + candidatos con sus datos y CV, en una sola petición.
    /// </summary>
    public class RevisionLongListDto
    {
        public int RequerimientoId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Puesto { get; set; } = string.Empty;
        public string? Area { get; set; }
        public string? ProyectoObra { get; set; }
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>Candidatos cargados por GTH en la long list, en orden.</summary>
        public List<CandidatoRevisionDto> Candidatos { get; set; } = new();
    }

    /// <summary>Un candidato de la long list como lo ve el solicitante en la revisión.</summary>
    public class CandidatoRevisionDto
    {
        public int CandidatoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Puesto { get; set; }
        public int? ExperienciaAnios { get; set; }
        public string? Disponibilidad { get; set; }

        /// <summary>Fuente de reclutamiento (nombre del canal). Null si no se indicó.</summary>
        public string? FuenteNombre { get; set; }

        public string? Comentario { get; set; }

        /// <summary>Nombre y link del CV en SharePoint (para "Ver CV completo").</summary>
        public string? CvNombre { get; set; }
        public string? CvUrl { get; set; }

        /// <summary>Nombre y link del informe en SharePoint (opcional).</summary>
        public string? InformeNombre { get; set; }
        public string? InformeUrl { get; set; }

        /// <summary>Estado de revisión (PENDIENTE / APROBADO / RECHAZADO).</summary>
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;
    }
}
