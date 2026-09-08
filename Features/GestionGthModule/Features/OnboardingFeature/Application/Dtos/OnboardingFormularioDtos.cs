namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos
{
    /// <summary>Una opción de catálogo del formulario (id + texto).</summary>
    public class OpcionFormularioDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    /// <summary>
    /// Una razón social del grupo, con el banco que le corresponde. El formulario lo necesita para
    /// poder decirle al colaborador «en esta razón social se trabaja con X» sin volver a preguntar.
    /// </summary>
    public class RazonSocialOpcionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? BancoNombre { get; set; }
    }

    // ── Cara pública (el colaborador, por token) ──────────────────────────────

    /// <summary>
    /// Todo lo que la página pública del formulario necesita al abrirse: quién es, qué se le pactó,
    /// los catálogos de los desplegables y lo que ya haya respondido.
    /// </summary>
    public class OnboardingFormularioPublicoDto
    {
        /// <summary>Nombre del colaborador, para saludarlo y para que se reconozca en el formulario.</summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Código del requerimiento que originó su contratación (referencia interna).</summary>
        public string Codigo { get; set; } = string.Empty;

        public string? Puesto { get; set; }
        public string? Area { get; set; }
        public string? ProyectoObra { get; set; }

        /// <summary>Fecha límite para completarlo (la del correo de bienvenida).</summary>
        public DateOnly? FechaLimite { get; set; }

        /// <summary>
        /// true cuando ya lo envió: la página pasa a solo lectura. Se puede corregir hasta que se
        /// manda, no después.
        /// </summary>
        public bool SoloLectura { get; set; }

        /// <summary>
        /// Lo que el proceso ya sabe de él y por eso NO se le vuelve a preguntar. Va en pantalla
        /// como un panel de solo lectura para que pueda verificarlo (si algo está mal, lo corrige
        /// GTH en su ficha, no acá).
        /// </summary>
        public DatosRegistradosDto DatosRegistrados { get; set; } = new();

        // Catálogos
        public List<OpcionFormularioDto> Puestos { get; set; } = new();
        public List<OpcionFormularioDto> Ubicaciones { get; set; } = new();
        public List<RazonSocialOpcionDto> RazonesSociales { get; set; } = new();
        public List<OpcionFormularioDto> Sexos { get; set; } = new();
        public List<OpcionFormularioDto> TallasCalzado { get; set; } = new();
        public List<OpcionFormularioDto> Tallas { get; set; } = new();
        public List<OpcionFormularioDto> RentaQuinta { get; set; } = new();

        /// <summary>
        /// Respuestas guardadas. En el primer ingreso vienen precargadas con lo que la carta oferta
        /// ya pactó (puesto, fecha de ingreso, sueldo, razón social): el colaborador confirma en vez
        /// de transcribir el correo.
        /// </summary>
        public OnboardingFormularioRespuestasDto Respuestas { get; set; } = new();
    }

    /// <summary>Lo que el proceso ya capturó del colaborador y el formulario solo muestra.</summary>
    public class DatosRegistradosDto
    {
        public string? NombresCompletos { get; set; }
        public string? TipoDocumento { get; set; }
        public string? NumeroDocumento { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        public string? NumeroCelular { get; set; }
        public string? Distrito { get; set; }
        public string? EstadoCivil { get; set; }
        public string? CorreoElectronico { get; set; }
    }

    /// <summary>Las respuestas del formulario. Es el mismo shape que viaja de ida y de vuelta.</summary>
    public class OnboardingFormularioRespuestasDto
    {
        // Datos personales
        public string? Direccion { get; set; }

        // Información laboral
        public int? PuestoId { get; set; }
        public DateOnly? FechaIngreso { get; set; }
        public decimal? RemuneracionMensual { get; set; }
        public int? UbicacionId { get; set; }
        public int? ContributorId { get; set; }

        // Pago de haberes
        public bool? CuentaSueldo { get; set; }

        // Información personal complementaria
        public int? SexoId { get; set; }
        public string? ContactoEmergencia { get; set; }
        public string? CelularEmergencia { get; set; }
        public int? NumeroHijos { get; set; }
        public int? TallaCalzadoId { get; set; }
        public int? TallaId { get; set; }
        public bool? UsaLentes { get; set; }
        public string? Hobbies { get; set; }

        // Renta de 5ta y EMO
        public int? RentaQuintaId { get; set; }
        public DateOnly? FechaEmo { get; set; }

        // Consentimiento
        public bool? DeclaracionVeracidad { get; set; }
    }

    // ── Cara de GTH (envío del correo de bienvenida) ──────────────────────────

    /// <summary>
    /// Todo lo que el correo de bienvenida necesita, resuelto en una sola consulta. Lo devuelve el
    /// repositorio junto con el token del formulario ya creado (o el que ya existía, si se está
    /// reenviando).
    /// </summary>
    public class BienvenidaContextoDto
    {
        public int OnboardingId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Puesto { get; set; }
        public string? Area { get; set; }
        public string? Empresa { get; set; }
        public string? ProyectoObra { get; set; }
        public DateOnly? FechaIngreso { get; set; }

        /// <summary>Correo personal del colaborador: el destinatario principal.</summary>
        public string Correo { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;
        public DateOnly? FechaLimite { get; set; }

        /// <summary>Cuándo salió el correo por última vez (UTC). null = nunca.</summary>
        public DateTimeOffset? EnviadoEn { get; set; }
    }

    /// <summary>Lo que la pantalla manda al pulsar «Enviar correo».</summary>
    public class EnviarBienvenidaDto
    {
        /// <summary>
        /// Hasta cuándo tiene el colaborador para completar el formulario y mandar sus documentos.
        /// Si no viene, se usa el valor por defecto del servicio.
        /// </summary>
        public DateOnly? FechaLimite { get; set; }
    }
}
