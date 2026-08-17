namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models
{
    /// <summary>
    /// Onboarding de un nuevo colaborador (tabla <c>gth_onboarding</c>): la continuación del proceso
    /// de Reclutamiento. Solo se puede abrir para un candidato SELECCIONADO cuyo requerimiento ya
    /// quedó CERRADO — es decir, después de que el área solicitante le dio el puesto.
    ///
    /// Arranca con el envío de la carta oferta al correo personal del colaborador
    /// (<c>person.email</c>, que es el que GTH validó al aprobar su formulario de postulante), así
    /// que la carta y su envío se guardan acá: el archivo va a SharePoint y la fila conserva el
    /// enlace, a quién se le envió y cuándo.
    ///
    /// Los datos de la vacante (código del requerimiento, puesto, área, razón social, jefe directo)
    /// no se copian: se leen por <c>gth_candidato → gth_requerimiento</c>, que es donde ya viven.
    /// </summary>
    public class GthOnboarding
    {
        public int GthOnboardingId { get; set; }

        /// <summary>
        /// FK al candidato seleccionado que da origen al onboarding. Único entre los vigentes: un
        /// seleccionado no puede tener dos onboardings abiertos.
        /// </summary>
        public int GthCandidatoId { get; set; }

        /// <summary>
        /// FK a <c>person</c>: la ficha de la data maestra del colaborador. Se resuelve del formulario
        /// aprobado del postulante (<c>gth_postulante_formulario.person_id</c>). Null solo si el
        /// formulario nunca se aprobó, caso en el que el correo se toma del formulario y la fase
        /// «Base maestra» quedará pendiente de crear la ficha.
        /// </summary>
        public int? PersonId { get; set; }

        /// <summary>FK a <c>gth_onboarding_fase</c>: en qué paso del checklist está.</summary>
        public int GthOnboardingFaseId { get; set; }

        /// <summary>FK a <c>gth_onboarding_estado</c>: el badge de la tabla.</summary>
        public int GthOnboardingEstadoId { get; set; }

        /// <summary>
        /// Fecha de ingreso pactada. Se propone con la <c>fecha_requerida_ingreso</c> del
        /// requerimiento y GTH la puede ajustar al abrir el onboarding.
        /// </summary>
        public DateOnly? FechaIngreso { get; set; }

        // ── Carta oferta (archivo en SharePoint + trazabilidad del envío) ─────
        public string? CartaOfertaNombre { get; set; }
        public string? CartaOfertaUrl { get; set; }
        public string? CartaOfertaItemId { get; set; }
        public string? CartaOfertaDriveId { get; set; }

        /// <summary>Correo personal al que se envió la carta oferta.</summary>
        public string? CartaOfertaCorreo { get; set; }

        public DateTimeOffset? CartaOfertaEnviadaDateTime { get; set; }
        public int? CartaOfertaEnviadaUserId { get; set; }

        // ── Carta oferta FIRMADA (la que el colaborador devuelve) ─────────────
        // Va aparte de la enviada porque son dos documentos distintos del mismo expediente: la
        // enviada es la propuesta y la firmada es la evidencia que abre el file digital. Se guarda
        // en la misma carpeta de SharePoint que la enviada.
        public string? CartaFirmadaNombre { get; set; }
        public string? CartaFirmadaUrl { get; set; }
        public string? CartaFirmadaItemId { get; set; }
        public string? CartaFirmadaDriveId { get; set; }

        public DateTimeOffset? CartaFirmadaSubidaDateTime { get; set; }
        public int? CartaFirmadaSubidaUserId { get; set; }

        /// <summary>
        /// Momento en que GTH aprobó la carta firmada (RF-ONB-02). Mientras esté en null el
        /// onboarding no puede avanzar de fase: es la primera actividad obligatoria del checklist.
        /// </summary>
        public DateTimeOffset? CartaFirmadaAprobadaDateTime { get; set; }
        public int? CartaFirmadaAprobadaUserId { get; set; }

        // ── File digital del colaborador (RF-ONB-04) ──────────────────────────
        // La carpeta de SharePoint donde se guardan TODOS los documentos de este onboarding. Se
        // resuelve al enviar la carta oferta y se persiste acá: los documentos siguientes (empezando
        // por la carta firmada) se suben a esta carpeta sin volver a derivarla del nombre, que puede
        // cambiar en la base maestra después del envío.
        public string? FileDigitalDriveId { get; set; }
        public string? FileDigitalItemId { get; set; }

        /// <summary>Ruta legible de esa carpeta, solo para mostrarla en pantalla.</summary>
        public string? FileDigitalRuta { get; set; }

        /// <summary>Observación interna de GTH al abrir el onboarding (opcional).</summary>
        public string? Observacion { get; set; }

        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
