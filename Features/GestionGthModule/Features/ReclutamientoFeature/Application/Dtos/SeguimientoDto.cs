namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Detalle de seguimiento de un requerimiento (modal "Estado del reclutamiento"): cabecera con
    /// datos clave + línea de tiempo vertical de las fases del pipeline. Se sirve en una sola petición.
    /// </summary>
    public class SeguimientoDto
    {
        public int RequerimientoId { get; set; }
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Puesto solicitado (para el subtítulo del modal).</summary>
        public string Puesto { get; set; } = string.Empty;

        /// <summary>Tipo de requerimiento (Nuevo / Reemplazo).</summary>
        public string TipoRequerimiento { get; set; } = string.Empty;

        public string? Area { get; set; }
        public string? ProyectoObra { get; set; }
        public string? Justificacion { get; set; }

        /// <summary>Fecha requerida de ingreso (solo fecha).</summary>
        public DateOnly FechaRequeridaIngreso { get; set; }

        /// <summary>Fecha de envío (created) en hora Perú (UTC-5).</summary>
        public DateTime Enviado { get; set; }

        // ── Estado actual ────────────────────────────────────────────────
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;
        public int EstadoOrden { get; set; }

        /// <summary>
        /// ¿Requiere aprobación de Gerencia General? Solo aplica a puestos nuevos
        /// (tipo "Nuevo"); un reemplazo no la requiere.
        /// </summary>
        public bool AprobacionGgRequerida { get; set; }

        // ── Sustento (adjunto opcional) ──────────────────────────────────
        public string? SustentoNombre { get; set; }
        public string? SustentoUrl { get; set; }

        /// <summary>Fases del pipeline en orden, con su estado (done/current/pending) ya calculado.</summary>
        public List<FaseSeguimientoDto> Fases { get; set; } = new();

        /// <summary>Descripción de la siguiente fase pendiente (null si el requerimiento ya cerró).</summary>
        public string? SiguientePaso { get; set; }
    }

    /// <summary>Una fase del pipeline dentro del seguimiento vertical.</summary>
    public class FaseSeguimientoDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int Orden { get; set; }

        /// <summary>Estado visual de la fase respecto a la fase actual del requerimiento: "done" | "current" | "pending".</summary>
        public string Estado { get; set; } = "pending";
    }
}
