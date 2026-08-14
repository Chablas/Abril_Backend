namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Un candidato que quedó rechazado en algún punto del proceso de un requerimiento, con la
    /// etapa en la que se lo rechazó. Arma el «Historial de candidatos rechazados» que ven tanto
    /// GTH (detalle del requerimiento) como el área solicitante (Estado del reclutamiento).
    ///
    /// Incluye a los candidatos de long lists anteriores (los que quedaron con <c>state = false</c>
    /// al enviar una nueva): cuando el solicitante rechaza a todos, el requerimiento vuelve a
    /// LONG_LIST y esos son precisamente los que hay que poder consultar para no volver a
    /// presentarlos.
    /// </summary>
    public class CandidatoRechazadoDto
    {
        public int CandidatoId { get; set; }

        public string Nombre { get; set; } = string.Empty;

        /// <summary>Puesto del requerimiento al momento de cargar la long list (snapshot).</summary>
        public string? Puesto { get; set; }

        /// <summary>
        /// En qué long list del requerimiento estaba el candidato (1 = la primera). Sin este dato,
        /// dos rechazados en la etapa "Long list" de vueltas distintas se leen igual.
        /// </summary>
        public int NumeroLongList { get; set; }

        /// <summary>
        /// Etapa en la que se lo rechazó: <c>LONG_LIST</c> (revisión de CVs), <c>ENTREVISTAS</c>
        /// (descartado por GTH tras la entrevista) o <c>DECISION_FINAL</c> (el solicitante rechazó
        /// al finalista). Se deriva del estado del candidato y del resultado de su evaluación.
        /// </summary>
        public string EtapaCodigo { get; set; } = string.Empty;

        /// <summary>Nombre de la etapa para mostrar ("Long list", "Entrevistas", "Decisión final").</summary>
        public string EtapaNombre { get; set; } = string.Empty;

        /// <summary>Quién lo rechazó: <c>SOLICITANTE</c> (área usuaria) o <c>GTH</c>.</summary>
        public string RechazadoPorCodigo { get; set; } = string.Empty;

        /// <summary>Nombre de quien lo rechazó ("Área solicitante" / "GTH").</summary>
        public string RechazadoPorNombre { get; set; } = string.Empty;

        /// <summary>
        /// Momento del rechazo en hora de Perú (UTC-5). En los candidatos decididos antes de que
        /// existiera <c>gth_candidato.decision_date_time</c> cae a la fecha de actualización y,
        /// en última instancia, a la de creación: nunca queda una fila del historial sin fecha.
        /// </summary>
        public DateTime RechazadoEn { get; set; }

        /// <summary>Comentario interno que GTH registró sobre el candidato al cargar la long list.</summary>
        public string? Comentario { get; set; }

        // ── CV en SharePoint (para poder volver a revisarlo desde el historial) ─
        public string? CvNombre { get; set; }
        public string? CvUrl { get; set; }
    }
}
