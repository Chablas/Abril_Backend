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

        /// <summary>true si el formulario ya fue APROBADO por GTH → solo lectura.</summary>
        public bool SoloLectura { get; set; }

        /// <summary>
        /// Observaciones de GTH cuando el formulario fue RECHAZADO: el postulante vuelve a abrir el
        /// mismo enlace con sus respuestas precargadas, corrige lo observado y lo reenvía. Es null en
        /// cualquier otro estado (una vez corregido y reenviado ya no se le muestran).
        /// </summary>
        public string? Observaciones { get; set; }

        // Catálogos de los desplegables del formulario.
        public List<OpcionDto> EstadosCiviles { get; set; } = new();
        public List<TipoDocumentoOpcionDto> TiposDocumento { get; set; } = new();
        public List<DistritoOpcionDto> Distritos { get; set; } = new();
        public List<OpcionDto> Universidades { get; set; } = new();
        public List<OpcionDto> GradosAcademicos { get; set; } = new();
        public List<OpcionDto> Disponibilidades { get; set; } = new();
        public List<OpcionDto> MotivosCese { get; set; } = new();

        /// <summary>Respuestas ya guardadas (ids de catálogo + valores) para precargar el formulario.</summary>
        public PostulanteFormularioRespuestasDto Respuestas { get; set; } = new();
    }

    /// <summary>
    /// Tipo de documento para el desplegable. Además del nombre lleva el <c>Codigo</c> estable
    /// (DNI / CE) para que el formulario pueda aplicar reglas por tipo (el DNI son 8 dígitos)
    /// sin depender del texto que se muestra.
    /// </summary>
    public class TipoDocumentoOpcionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        /// <summary>DNI o CE (espejo de gth_tipo_documento.codigo).</summary>
        public string Codigo { get; set; } = string.Empty;
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

    /// <summary>
    /// Contexto que devuelve el repositorio cuando el postulante envía su formulario: lo necesario
    /// para avisarle a GTH que ya lo completó, sin volver a consultar la base de datos.
    /// </summary>
    public class FormularioCompletadoContextoDto
    {
        /// <summary>Código del requerimiento (REQ-AAAA-NNNN).</summary>
        public string Codigo { get; set; } = string.Empty;
        public string Puesto { get; set; } = string.Empty;
        public string? Area { get; set; }
        public string? ProyectoObra { get; set; }

        /// <summary>Nombre del postulante (el que declaró en el formulario; si no, el que registró GTH).</summary>
        public string CandidatoNombre { get; set; } = string.Empty;

        /// <summary>Correo y celular declarados, para que GTH pueda contactarlo sin abrir el sistema.</summary>
        public string? CorreoPostulante { get; set; }
        public string? NumeroCelular { get; set; }

        /// <summary>Momento del envío, ya en hora de Perú.</summary>
        public DateTime CompletadoEn { get; set; }

        /// <summary>true si es un reenvío tras un rechazo: el postulante corrigió lo observado.</summary>
        public bool EsCorreccion { get; set; }
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
    /// Contexto que devuelve el repositorio al registrar la decisión de GTH: el resumen para
    /// refrescar el modal más lo necesario para armar el correo de rechazo (token del enlace, puesto,
    /// nombre y correo del postulante), sin volver a consultar la base de datos. Los datos del correo
    /// solo se llenan cuando la decisión es un rechazo (al aprobar no se envía nada al postulante).
    /// </summary>
    public class DecisionFormularioContextoDto
    {
        public CandidatoFormularioResumenDto Resumen { get; set; } = new();

        /// <summary>
        /// true = hay que avisarle al postulante del rechazo. Solo cuando había completado el
        /// formulario: si nunca lo llenó, el rechazo es una decisión interna para destrabar el
        /// proceso y no tiene sentido pedirle que corrija algo que nunca escribió.
        /// </summary>
        public bool AvisarAlPostulante { get; set; }

        /// <summary>Token del enlace público (el mismo del envío original: conserva las respuestas ya guardadas).</summary>
        public string Token { get; set; } = string.Empty;
        public string Puesto { get; set; } = string.Empty;

        /// <summary>Nombre del postulante para el saludo del correo (el declarado en el formulario si lo hay).</summary>
        public string CandidatoNombre { get; set; } = string.Empty;

        /// <summary>Correo al que GTH envió el formulario (mismo destinatario del correo de rechazo).</summary>
        public string Correo { get; set; } = string.Empty;

        /// <summary>Observaciones del rechazo tal como se registraron (null si se aprobó o no se indicó motivo).</summary>
        public string? Motivo { get; set; }
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

        /// <summary>
        /// true si lo que se está reenviando es un formulario RECHAZADO: en ese caso el correo que
        /// corresponde es el de correcciones (con las observaciones), no el de invitación inicial.
        /// </summary>
        public bool EsRechazo { get; set; }

        /// <summary>Observaciones del rechazo, para el correo de correcciones. null si no es un rechazo.</summary>
        public string? Motivo { get; set; }

        public CandidatoFormularioResumenDto Resumen { get; set; } = new();
    }
}
