namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Body del PUT que guarda la evaluación de la entrevista de un candidato: los cuatro
    /// puntajes (0-100, null = sin registrar) y los tres comentarios del informe de finalista.
    /// El resultado (PASO / NO_PASO) no viaja aquí: lo define el correo de agradecimiento.
    /// </summary>
    public class EvaluacionGuardarDto
    {
        public int? PuntajeEntrevista { get; set; }
        public int? PuntajePsicotecnico { get; set; }
        public int? PuntajeTecnica { get; set; }
        public int? PuntajeResultado { get; set; }

        /// <summary>Resultado de la entrevista (qué se observó).</summary>
        public string? ComentarioEntrevista { get; set; }

        /// <summary>Informe psicotécnico del candidato.</summary>
        public string? ComentarioPsicotecnico { get; set; }

        /// <summary>Recomendación de GTH al área solicitante.</summary>
        public string? ComentarioRecomendacion { get; set; }
    }

    /// <summary>Evaluación de la entrevista de un candidato, como la muestran GTH y el solicitante.</summary>
    public class EvaluacionResumenDto
    {
        public int? PuntajeEntrevista { get; set; }
        public int? PuntajePsicotecnico { get; set; }
        public int? PuntajeTecnica { get; set; }
        public int? PuntajeResultado { get; set; }

        public string? ComentarioEntrevista { get; set; }
        public string? ComentarioPsicotecnico { get; set; }
        public string? ComentarioRecomendacion { get; set; }

        /// <summary>Resultado alcanzado: PENDIENTE / PASO / NO_PASO (código estable).</summary>
        public string ResultadoCodigo { get; set; } = string.Empty;
        public string ResultadoNombre { get; set; } = string.Empty;

        /// <summary>Correo al que se envió el agradecimiento (null si no se envió).</summary>
        public string? AgradecimientoCorreo { get; set; }

        /// <summary>Momento del envío del agradecimiento (hora de Perú). Null si no se envió.</summary>
        public DateTime? AgradecimientoEnviadoEn { get; set; }

        /// <summary>
        /// Momento de la decisión final del área solicitante sobre el finalista (hora de Perú).
        /// Null mientras no haya decidido.
        /// </summary>
        public DateTime? DecididoEn { get; set; }
    }

    /// <summary>Resultado de guardar la evaluación o de enviar el correo de agradecimiento.</summary>
    public class EvaluacionAccionResultDto
    {
        public string Message { get; set; } = string.Empty;
        public EvaluacionResumenDto Evaluacion { get; set; } = new();
    }

    /// <summary>Datos que necesita el servicio para armar el correo de agradecimiento.</summary>
    public class AgradecimientoEnvioContextoDto
    {
        public string CandidatoNombre { get; set; } = string.Empty;
        public string Puesto { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Correo del postulante al que se envía el agradecimiento.</summary>
        public string Correo { get; set; } = string.Empty;

        public EvaluacionResumenDto Resumen { get; set; } = new();
    }

    /// <summary>
    /// Informe de finalistas de un requerimiento tal como lo ve el área solicitante (modal
    /// "Finalistas enviados por GTH"): cabecera + finalistas con su evaluación y su CV.
    /// </summary>
    public class RevisionFinalistasDto
    {
        public int RequerimientoId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Puesto { get; set; } = string.Empty;
        public string? Area { get; set; }
        public string? ProyectoObra { get; set; }
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>Finalistas ordenados por puntaje de resultado (mejor primero).</summary>
        public List<FinalistaDto> Finalistas { get; set; } = new();
    }

    /// <summary>
    /// Body del POST con la decisión final del área solicitante sobre un finalista: aprobarlo
    /// (cierra el proceso y pasa a onboarding) o rechazarlo (se le envía el correo de
    /// agradecimiento).
    /// </summary>
    public class FinalistaDecisionDto
    {
        public int CandidatoId { get; set; }

        /// <summary>true = aprobar y cerrar el proceso; false = rechazar al finalista.</summary>
        public bool Aprobado { get; set; }
    }

    /// <summary>Resultado de registrar la decisión final sobre un finalista.</summary>
    public class FinalistaDecisionResultDto
    {
        public string Message { get; set; } = string.Empty;

        /// <summary>Estado en el que quedó el requerimiento tras la decisión.</summary>
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>true si la decisión fue aprobar (el proceso de reclutamiento queda cerrado).</summary>
        public bool Aprobado { get; set; }

        /// <summary>
        /// true si con este rechazo ya no queda ningún finalista en carrera: el requerimiento
        /// vuelve a LONG_LIST para que GTH envíe una nueva long list.
        /// </summary>
        public bool TodosRechazados { get; set; }

        /// <summary>Nombre del finalista decidido (para el mensaje al usuario).</summary>
        public string CandidatoNombre { get; set; } = string.Empty;
    }

    /// <summary>Datos que necesita el servicio para armar los correos de la decisión final.</summary>
    public class FinalistaDecisionContextoDto
    {
        public FinalistaDecisionResultDto Resultado { get; set; } = new();

        public string Codigo { get; set; } = string.Empty;
        public string Puesto { get; set; } = string.Empty;
        public string? Area { get; set; }
        public string? ProyectoObra { get; set; }

        /// <summary>Nombre de quien tomó la decisión (para el correo a GTH).</summary>
        public string? SolicitanteNombre { get; set; }

        /// <summary>
        /// Correo del finalista rechazado, al que se le envía el agradecimiento. Vacío cuando la
        /// decisión fue aprobarlo (no se le escribe al candidato desde aquí).
        /// </summary>
        public string CandidatoCorreo { get; set; } = string.Empty;
    }

    /// <summary>Un finalista con el informe que GTH registró tras su entrevista.</summary>
    public class FinalistaDto
    {
        public int CandidatoId { get; set; }
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Puesto del requerimiento (snapshot), no un dato capturado por candidato.</summary>
        public string? Puesto { get; set; }

        /// <summary>Nombre y link del CV en SharePoint (para "Ver CV completo").</summary>
        public string? CvNombre { get; set; }
        public string? CvUrl { get; set; }

        /// <summary>Evaluación registrada por GTH (puntajes + comentarios del informe).</summary>
        public EvaluacionResumenDto Evaluacion { get; set; } = new();
    }
}
