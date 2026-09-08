namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models
{
    /// <summary>
    /// Formulario «Nuevos Talentos» del colaborador que entra (tabla <c>gth_onboarding_formulario</c>).
    /// Reemplaza al Microsoft Forms del mismo nombre: lo abre GTH al mandar el correo de bienvenida
    /// y lo llena el colaborador desde una página pública, sin login, con el token de este registro.
    ///
    /// NO vuelve a preguntar lo que el formulario del postulante ya capturó (nombre, documento,
    /// fecha de nacimiento, celular, distrito, estado civil, estudios): eso ya está en su ficha
    /// maestra y repetirlo solo abre la puerta a que las dos versiones no coincidan. Lo que se pide
    /// acá es lo que recién existe ahora que la persona entra.
    /// </summary>
    public class GthOnboardingFormulario
    {
        public int GthOnboardingFormularioId { get; set; }

        /// <summary>Onboarding al que pertenece. Uno solo vivo por colaborador.</summary>
        public int GthOnboardingId { get; set; }

        /// <summary>Credencial del enlace público. Es lo único que autentica al colaborador.</summary>
        public string Token { get; set; } = null!;

        public int GthOnboardingFormularioEstadoId { get; set; }

        // ── Envío del correo de bienvenida ────────────────────────────────────

        /// <summary>Buzón al que salió el correo (el personal de su ficha maestra).</summary>
        public string CorreoEnvio { get; set; } = null!;

        /// <summary>Hasta cuándo tiene para completarlo. Es lo que el correo le dice en grande.</summary>
        public DateOnly? FechaLimite { get; set; }

        public DateTimeOffset? EnviadoDateTime { get; set; }
        public int? EnviadoUserId { get; set; }

        /// <summary>Cuándo lo envió el colaborador. null = todavía no lo completó.</summary>
        public DateTimeOffset? CompletadoDateTime { get; set; }

        // ── Datos personales ──────────────────────────────────────────────────

        /// <summary>Domicilio actual. Es el único dato personal que el proceso todavía no tenía.</summary>
        public string? Direccion { get; set; }

        // ── Información laboral (confirma lo que dice el correo de bienvenida) ─

        public int? PuestoId { get; set; }
        public DateOnly? FechaIngreso { get; set; }

        /// <summary>Remuneración mensual BRUTA, sin bonos ni comisiones.</summary>
        public decimal? RemuneracionMensual { get; set; }

        public int? GthOnboardingUbicacionId { get; set; }

        /// <summary>Razón social con la que se le contrata: es la que define su banco.</summary>
        public int? ContributorId { get; set; }

        // ── Pago de haberes ───────────────────────────────────────────────────

        /// <summary>
        /// ¿Quiere que se le abra la cuenta sueldo en el banco de su razón social? El banco no se
        /// guarda acá: sale de <c>contributor.banco_id</c>, que es donde vive y donde se corrige.
        /// </summary>
        public bool? CuentaSueldo { get; set; }

        // ── Información personal complementaria ───────────────────────────────

        public int? SexoId { get; set; }

        /// <summary>Nombre completo y parentesco, tal como lo escribe el colaborador.</summary>
        public string? ContactoEmergencia { get; set; }
        public string? CelularEmergencia { get; set; }

        /// <summary>Cantidad de hijos. El desplegable llega hasta «más de 3», que se guarda como 4.</summary>
        public int? NumeroHijos { get; set; }

        public int? TallaCalzadoId { get; set; }

        /// <summary>Talla de blusa/camisa (catálogo <c>talla</c>, XS…XXL).</summary>
        public int? TallaId { get; set; }

        public bool? UsaLentes { get; set; }
        public string? Hobbies { get; set; }

        // ── Renta de 5ta categoría y EMO de entrada ───────────────────────────

        public int? GthRentaQuintaId { get; set; }

        /// <summary>Día que eligió para su EMO de entrada, anterior a su fecha de ingreso.</summary>
        public DateOnly? FechaEmo { get; set; }

        // ── Consentimiento ────────────────────────────────────────────────────

        public bool? DeclaracionVeracidad { get; set; }

        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
