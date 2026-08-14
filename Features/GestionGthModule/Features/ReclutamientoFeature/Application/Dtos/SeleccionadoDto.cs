namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Quién obtuvo el puesto de un requerimiento: el candidato que el área solicitante aprobó en
    /// la decisión final (resultado SELECCIONADO), con la trazabilidad de quién y cuándo lo
    /// decidió. Null mientras el proceso no se haya cerrado con un seleccionado.
    ///
    /// Lo sirven las dos pantallas del proceso —el detalle del requerimiento (GTH) y el «Estado
    /// del reclutamiento» (solicitante)— para que el resultado quede registrado y consultable por
    /// ambos lados, no solo en el correo del momento.
    /// </summary>
    public class SeleccionadoDto
    {
        public int CandidatoId { get; set; }

        /// <summary>Nombre del candidato que obtuvo el puesto.</summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Puesto del requerimiento al cargar la long list (snapshot).</summary>
        public string? Puesto { get; set; }

        // ── CV en SharePoint ──────────────────────────────────────────────────
        public string? CvNombre { get; set; }
        public string? CvUrl { get; set; }

        /// <summary>
        /// Momento en que el solicitante lo aprobó, en hora de Perú (UTC-5). Null solo si la
        /// decisión quedó registrada sin fecha (no debería pasar en el flujo actual).
        /// </summary>
        public DateTime? SeleccionadoEn { get; set; }

        /// <summary>
        /// Nombre del usuario del área solicitante que tomó la decisión final. Null si no se pudo
        /// resolver su ficha de trabajador.
        /// </summary>
        public string? SeleccionadoPor { get; set; }

        /// <summary>
        /// Responsable del proceso en GTH (el reclutador que llevó la vacante) al momento de la
        /// consulta. Null si el requerimiento nunca tuvo responsable asignado.
        /// </summary>
        public string? ResponsableGth { get; set; }
    }
}
