namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Formulario de información del postulante (tabla <c>gth_postulante_formulario</c>): 1:1 con un
    /// candidato aprobado (<see cref="GthCandidato"/>). GTH envía el enlace al correo del postulante;
    /// el postulante lo llena desde una página pública (acceso por <c>token</c>, sin autenticación) y
    /// GTH luego lo revisa (aprueba/rechaza). Reemplaza al formulario externo de Microsoft Forms.
    ///
    /// Los campos del formulario (páginas 1-4) quedan en null hasta que el postulante lo completa;
    /// los que apuntan a un catálogo (estado civil, tipo de documento, distrito, universidad, grado,
    /// disponibilidad, motivo de cese) se guardan como FK, no como texto libre.
    ///
    /// La convocatoria no se pregunta ni se guarda acá: es la del proceso al que ya fue invitado el
    /// postulante y se lee por <c>gth_candidato → gth_requerimiento → puesto</c>.
    /// </summary>
    public class GthPostulanteFormulario
    {
        public int GthPostulanteFormularioId { get; set; }

        /// <summary>FK al candidato aprobado dueño del formulario (1:1 entre los vigentes).</summary>
        public int GthCandidatoId { get; set; }

        /// <summary>Token de acceso público al formulario (va en el enlace del correo). Único entre los vigentes.</summary>
        public string Token { get; set; } = null!;

        /// <summary>FK a <c>gth_postulante_formulario_estado</c>: ENVIADO / COMPLETADO / APROBADO / RECHAZADO.</summary>
        public int GthPostulanteFormularioEstadoId { get; set; }

        /// <summary>Correo al que GTH envió el enlace del formulario.</summary>
        public string CorreoEnvio { get; set; } = null!;

        // ── Trazabilidad del flujo ────────────────────────────────────────────
        public DateTimeOffset? EnviadoDateTime { get; set; }
        public int? EnviadoUserId { get; set; }

        /// <summary>Momento en que el postulante envió (completó) el formulario.</summary>
        public DateTimeOffset? CompletadoDateTime { get; set; }

        /// <summary>Usuario de GTH que revisó (aprobó/rechazó) el formulario.</summary>
        public int? RevisadoUserId { get; set; }

        /// <summary>Nombre del revisor (snapshot para mostrar "Aprobado por …", estable en el tiempo).</summary>
        public string? RevisadoNombre { get; set; }

        public DateTimeOffset? RevisadoDateTime { get; set; }

        /// <summary>Motivo del rechazo (opcional, solo cuando el formulario se rechaza).</summary>
        public string? MotivoRechazo { get; set; }

        // ── Página 1 · Datos personales ───────────────────────────────────────
        public string? NombresCompletos { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        public int? GthEstadoCivilId { get; set; }
        public int? GthTipoDocumentoId { get; set; }
        public string? NumeroDocumento { get; set; }
        /// <summary>FK a <c>gth_distrito</c>: distrito de residencia (Lima o Callao).</summary>
        public int? GthDistritoId { get; set; }
        public string? CorreoElectronico { get; set; }
        public string? NumeroCelular { get; set; }
        public string? PretensionesSalariales { get; set; }
        public int? GthDisponibilidadId { get; set; }
        public string? Linkedin { get; set; }
        public string? PortafolioLink { get; set; }

        // ── Página 2 · Estudios realizados ────────────────────────────────────
        public string? Profesion { get; set; }
        public int? GthUniversidadId { get; set; }
        public int? GthGradoAcademicoId { get; set; }
        public string? NumeroColegiatura { get; set; }

        // ── Página 3 · Experiencia laboral (más reciente) ─────────────────────
        public string? Empresa { get; set; }
        public string? AreaTrabajo { get; set; }
        public string? Cargo { get; set; }
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaTermino { get; set; }
        public int? GthMotivoCeseId { get; set; }
        public string? FuncionesPrincipales { get; set; }
        public string? Logros { get; set; }
        public string? IngresoBrutoMensual { get; set; }
        public int? PersonasACargo { get; set; }
        public string? JefeInmediato { get; set; }
        /// <summary>Autoriza la verificación de referencias de este trabajo (Sí/No).</summary>
        public bool? AutorizaVerificacionReferencias { get; set; }

        // ── Página 4 · Consentimiento y veracidad ─────────────────────────────
        /// <summary>Declara bajo juramento que la información es veraz (Sí/No).</summary>
        public bool? DeclaracionVeracidad { get; set; }
        /// <summary>Confirma haber completado todos los documentos requeridos (Sí/No).</summary>
        public bool? ConfirmacionDocumentos { get; set; }

        // ── Auditoría ─────────────────────────────────────────────────────────
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
