namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models
{
    /// <summary>
    /// Onboarding de un nuevo colaborador (tabla <c>gth_onboarding</c>): la continuación del proceso
    /// de Reclutamiento. Solo se puede abrir para un candidato SELECCIONADO cuyo requerimiento ya
    /// quedó CERRADO — es decir, después de que firmó su carta oferta y GTH la aprobó.
    ///
    /// La carta oferta ya NO vive acá: es el último paso de Reclutamiento y su expediente está en
    /// <c>gth_carta_oferta</c>. Lo que este onboarding hereda de ella es el file digital del
    /// colaborador (la carpeta de SharePoint que esa carta creó) y la fecha de ingreso pactada, para
    /// seguir llenando el mismo expediente sin volver a resolver nada.
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
        /// FK a <c>person</c>: la ficha de la data maestra del colaborador. Se hereda de su carta
        /// oferta, que no se puede enviar sin ficha, así que en la práctica siempre viene llena.
        /// </summary>
        public int? PersonId { get; set; }

        /// <summary>FK a <c>gth_onboarding_fase</c>: en qué paso del checklist está.</summary>
        public int GthOnboardingFaseId { get; set; }

        /// <summary>FK a <c>gth_onboarding_estado</c>: el badge de la tabla.</summary>
        public int GthOnboardingEstadoId { get; set; }

        /// <summary>
        /// Fecha de ingreso pactada. Se hereda de la carta oferta (es una de sus condiciones) y GTH
        /// la puede ajustar al abrir el onboarding.
        /// </summary>
        public DateOnly? FechaIngreso { get; set; }

        // ── File digital del colaborador (RF-ONB-04) ──────────────────────────
        // La carpeta de SharePoint donde viven TODOS los documentos de este colaborador:
        // «{DNI} - {NOMBRE}» dentro de la biblioteca configurada, con una subcarpeta por tipo de
        // documento. La abre la carta oferta al final de Reclutamiento y el onboarding la hereda:
        // se guarda acá para no volver a derivarla del nombre, que puede cambiar en la base maestra.
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
