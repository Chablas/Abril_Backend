namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    // ── Público (página del postulante, acceso por token) ────────────────────

    /// <summary>
    /// Respuesta del GET público del formulario (por token): contexto del proceso + catálogos de
    /// los desplegables + respuestas ya guardadas (para reanudar) + estado. Todo en una sola petición.
    /// </summary>
    public class PostulanteFormularioPublicoDto
    {
        /// <summary>Puesto/convocatoria del proceso al que postula (para el encabezado del formulario).</summary>
        public string Puesto { get; set; } = string.Empty;

        /// <summary>Nombre con el que GTH registró al candidato (referencial, para saludarlo).</summary>
        public string CandidatoNombre { get; set; } = string.Empty;

        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>true si el formulario ya fue revisado por GTH (aprobado/rechazado) → solo lectura.</summary>
        public bool SoloLectura { get; set; }

        // Catálogos de los desplegables del formulario.
        public List<OpcionDto> EstadosCiviles { get; set; } = new();
        public List<OpcionDto> TiposDocumento { get; set; } = new();
        public List<DistritoOpcionDto> Distritos { get; set; } = new();
        public List<OpcionDto> Universidades { get; set; } = new();
        public List<OpcionDto> GradosAcademicos { get; set; } = new();
        public List<OpcionDto> Disponibilidades { get; set; } = new();
        public List<OpcionDto> MotivosCese { get; set; } = new();
        /// <summary>Convocatorias de interés (se reutiliza el catálogo de puestos gth_puesto).</summary>
        public List<OpcionDto> Convocatorias { get; set; } = new();

        /// <summary>Respuestas ya guardadas (ids de catálogo + valores) para precargar el formulario.</summary>
        public PostulanteFormularioRespuestasDto Respuestas { get; set; } = new();
    }

    /// <summary>Distrito para el desplegable (incluye la provincia para agrupar/mostrar).</summary>
    public class DistritoOpcionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        /// <summary>LIMA o CALLAO.</summary>
        public string Provincia { get; set; } = string.Empty;
    }

    /// <summary>
    /// Respuestas del formulario del postulante (ids de catálogo + valores libres). Se usa tanto para
    /// precargar (GET público) como para recibir el envío (POST público) del postulante.
    /// </summary>
    public class PostulanteFormularioRespuestasDto
    {
        // Página 1 · Datos personales
        public string? NombresCompletos { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        public int? EstadoCivilId { get; set; }
        public int? TipoDocumentoId { get; set; }
        public string? NumeroDocumento { get; set; }
        public int? DistritoId { get; set; }
        public string? CorreoElectronico { get; set; }
        public string? NumeroCelular { get; set; }
        public int? ConvocatoriaId { get; set; }
        public string? PretensionesSalariales { get; set; }
        public int? DisponibilidadId { get; set; }
        public string? Linkedin { get; set; }
        public string? PortafolioLink { get; set; }

        // Página 2 · Estudios realizados
        public string? Profesion { get; set; }
        public int? UniversidadId { get; set; }
        public int? GradoAcademicoId { get; set; }
        public string? NumeroColegiatura { get; set; }

        // Página 3 · Experiencia laboral
        public string? Empresa { get; set; }
        public string? AreaTrabajo { get; set; }
        public string? Cargo { get; set; }
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaTermino { get; set; }
        public int? MotivoCeseId { get; set; }
        public string? FuncionesPrincipales { get; set; }
        public string? Logros { get; set; }
        public string? IngresoBrutoMensual { get; set; }
        public int? PersonasACargo { get; set; }
        public string? JefeInmediato { get; set; }
        public bool? AutorizaVerificacionReferencias { get; set; }

        // Página 4 · Consentimiento y veracidad
        public bool? DeclaracionVeracidad { get; set; }
        public bool? ConfirmacionDocumentos { get; set; }
    }

    // ── GTH (bandeja de reclutamiento: enviar, revisar, aprobar/rechazar) ─────

    /// <summary>Body del POST que envía el formulario al correo del postulante.</summary>
    public class EnviarFormularioDto
    {
        public string Correo { get; set; } = string.Empty;
    }

    /// <summary>Body del POST que registra la decisión de GTH sobre el formulario (aprobar/rechazar).</summary>
    public class FormularioDecisionDto
    {
        public bool Aprobado { get; set; }
        /// <summary>Motivo del rechazo (opcional, solo cuando se rechaza).</summary>
        public string? Motivo { get; set; }
    }

    /// <summary>
    /// Vista de GTH del formulario de un candidato (modal "Ver formulario"): estado + trazabilidad +
    /// datos ya listos para mostrar (los catálogos resueltos a su nombre). Si el postulante aún no lo
    /// completó, <see cref="Datos"/> viene null y solo se muestra la estructura/estado.
    /// </summary>
    public class FormularioRevisionDto
    {
        /// <summary>true si el formulario existe (GTH ya envió el enlace al postulante).</summary>
        public bool Existe { get; set; }

        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        public string CandidatoNombre { get; set; } = string.Empty;
        public string? CorreoEnvio { get; set; }
        public DateTime? EnviadoEn { get; set; }
        public DateTime? CompletadoEn { get; set; }
        public string? RevisadoNombre { get; set; }
        public DateTime? RevisadoEn { get; set; }
        public string? MotivoRechazo { get; set; }

        /// <summary>Datos declarados por el postulante (null si aún no completó el formulario).</summary>
        public FormularioDatosDto? Datos { get; set; }
    }

    /// <summary>
    /// Datos del formulario ya listos para mostrar en el modal de GTH: los campos de catálogo vienen
    /// resueltos a su nombre (no id) y el resto tal cual los declaró el postulante.
    /// </summary>
    public class FormularioDatosDto
    {
        // Página 1
        public string? NombresCompletos { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        public string? EstadoCivil { get; set; }
        public string? TipoDocumento { get; set; }
        public string? NumeroDocumento { get; set; }
        public string? Distrito { get; set; }
        public string? CorreoElectronico { get; set; }
        public string? NumeroCelular { get; set; }
        public string? Convocatoria { get; set; }
        public string? PretensionesSalariales { get; set; }
        public string? Disponibilidad { get; set; }
        public string? Linkedin { get; set; }
        public string? PortafolioLink { get; set; }

        // Página 2
        public string? Profesion { get; set; }
        public string? Universidad { get; set; }
        public string? GradoAcademico { get; set; }
        public string? NumeroColegiatura { get; set; }

        // Página 3
        public string? Empresa { get; set; }
        public string? AreaTrabajo { get; set; }
        public string? Cargo { get; set; }
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaTermino { get; set; }
        public string? MotivoCese { get; set; }
        public string? FuncionesPrincipales { get; set; }
        public string? Logros { get; set; }
        public string? IngresoBrutoMensual { get; set; }
        public int? PersonasACargo { get; set; }
        public string? JefeInmediato { get; set; }
        public bool? AutorizaVerificacionReferencias { get; set; }

        // Página 4
        public bool? DeclaracionVeracidad { get; set; }
        public bool? ConfirmacionDocumentos { get; set; }
    }

    /// <summary>Estado del formulario del postulante como se muestra en la bandeja de GTH por candidato.</summary>
    public class CandidatoFormularioResumenDto
    {
        /// <summary>Estado del formulario: null si GTH aún no envió el enlace.</summary>
        public string? EstadoCodigo { get; set; }
        public string? EstadoNombre { get; set; }
        public string? CorreoEnvio { get; set; }
        public DateTime? EnviadoEn { get; set; }
        public DateTime? CompletadoEn { get; set; }
        public string? RevisadoNombre { get; set; }
        public DateTime? RevisadoEn { get; set; }
    }

    /// <summary>Resultado de enviar el formulario o registrar la decisión (para refrescar el modal).</summary>
    public class FormularioAccionResultDto
    {
        public string Message { get; set; } = string.Empty;
        public CandidatoFormularioResumenDto Formulario { get; set; } = new();
    }

    /// <summary>
    /// Contexto que devuelve el repositorio al preparar el envío del formulario: el token de acceso
    /// (nuevo o reutilizado), datos para el correo y el estado resultante para refrescar el modal.
    /// </summary>
    public class EnviarFormularioContextoDto
    {
        public string Token { get; set; } = string.Empty;
        public string Puesto { get; set; } = string.Empty;
        public string CandidatoNombre { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public CandidatoFormularioResumenDto Resumen { get; set; } = new();
    }
}
