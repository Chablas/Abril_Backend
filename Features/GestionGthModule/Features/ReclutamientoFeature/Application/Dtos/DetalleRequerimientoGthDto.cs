namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Detalle de un requerimiento para la vista de GTH (modal del ojo de la bandeja):
    /// cabecera + asignación interna actual + catálogos de los desplegables (con cupos por
    /// razón social) + canales de publicación, todo en una sola petición.
    /// </summary>
    public class DetalleRequerimientoGthDto
    {
        public int RequerimientoId { get; set; }

        /// <summary>Código REQ-AAAA-NNNN.</summary>
        public string Codigo { get; set; } = string.Empty;

        public string Puesto { get; set; } = string.Empty;

        /// <summary>Área solicitante (snapshot al registrar).</summary>
        public string? Area { get; set; }

        /// <summary>Proyecto/obra destino de la vacante.</summary>
        public string? ProyectoObra { get; set; }

        /// <summary>Tipo de requerimiento (Nuevo / Reemplazo).</summary>
        public string TipoRequerimiento { get; set; } = string.Empty;

        /// <summary>Vacantes de este requerimiento (cada vacante genera un requerimiento → 1).</summary>
        public int Vacantes { get; set; } = 1;

        public DateOnly FechaRequeridaIngreso { get; set; }

        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>Asignación interna de GTH actual del requerimiento.</summary>
        public AsignacionGthDto Asignacion { get; set; } = new();

        // ── Catálogos de los desplegables ─────────────────────────────────────
        /// <summary>Miembros GTH que pueden ser responsables del proceso.</summary>
        public List<OpcionDto> Responsables { get; set; } = new();

        public List<TipoProcesoOpcionDto> TiposProceso { get; set; } = new();

        public List<OpcionDto> Prioridades { get; set; } = new();

        /// <summary>Razones sociales activas (contributor.operativo = true) con sus cupos.</summary>
        public List<RazonSocialOpcionDto> RazonesSociales { get; set; } = new();

        /// <summary>Canales de publicación con su estado de publicación para este requerimiento.</summary>
        public List<CanalPublicacionDto> Canales { get; set; } = new();
    }

    /// <summary>Asignación interna de GTH de un requerimiento (todas opcionales/null = sin asignar).</summary>
    public class AsignacionGthDto
    {
        /// <summary>Id de gth_responsable_proceso (responsable del proceso).</summary>
        public int? ResponsableId { get; set; }

        /// <summary>Id de gth_tipo_proceso (tipo de proceso y SLA).</summary>
        public int? TipoProcesoId { get; set; }

        /// <summary>Id de gth_prioridad (prioridad interna).</summary>
        public int? PrioridadId { get; set; }

        /// <summary>Id de contributor (razón social activa).</summary>
        public int? ContributorId { get; set; }
    }

    /// <summary>Opción del desplegable "Tipo de proceso y SLA".</summary>
    public class TipoProcesoOpcionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int SlaDias { get; set; }
        public string? Descripcion { get; set; }
    }

    /// <summary>Opción del desplegable "Razón social activa", con sus cupos disponibles.</summary>
    public class RazonSocialOpcionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Cupos disponibles = tope (20) − trabajadores vigentes de la razón social en la base
        /// maestra (los practicantes no consumen cupo). Nunca negativo (se muestra 0).
        /// </summary>
        public int CuposDisponibles { get; set; }
    }

    /// <summary>Canal de publicación de vacantes y su estado para el requerimiento consultado.</summary>
    public class CanalPublicacionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        /// <summary>true = API disponible · publicación automática; false = registrar publicación manual.</summary>
        public bool ApiDisponible { get; set; }

        /// <summary>true = la vacante ya está registrada como publicada en este canal.</summary>
        public bool Publicado { get; set; }
    }

    /// <summary>Body del PATCH que guarda la asignación interna de GTH (reemplaza los 4 campos).</summary>
    public class AsignacionGthUpdateDto
    {
        public int? ResponsableId { get; set; }
        public int? TipoProcesoId { get; set; }
        public int? PrioridadId { get; set; }
        public int? ContributorId { get; set; }
    }

    /// <summary>Body del PUT que registra los canales donde se publicó la vacante.</summary>
    public class PublicacionesUpdateDto
    {
        public List<int> CanalIds { get; set; } = new();
    }

    /// <summary>
    /// Estado resultante de una transición del pipeline (respuesta de registrar la publicación
    /// y de iniciar la revisión de CV): el frontend actualiza el badge y las secciones del
    /// modal sin volver a pedir el detalle.
    /// </summary>
    public class EstadoRequerimientoResultDto
    {
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;
    }
}
