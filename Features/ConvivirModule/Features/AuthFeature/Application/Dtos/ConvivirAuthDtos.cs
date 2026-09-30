namespace Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Dtos
{
    /// <summary>El propietario entra con su DNI (<c>person.document_identity_code</c>), no con el correo.</summary>
    public class ConvivirLoginDto
    {
        public string Dni { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    /// <summary>
    /// Igual que el login de la intranet: un JWT de 2 minutos para llamar a la API y un session
    /// token opaco (fila de <c>user_session</c>) que es la sesión real. El JWT se renueva con
    /// <c>refresh</c>; la sesión vence cuando dice su <c>expires_at</c> en la base.
    /// </summary>
    public class ConvivirLoginResponseDto
    {
        public string AccessToken { get; set; } = null!;
        public string SessionToken { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public ConvivirUsuarioDto Usuario { get; set; } = null!;
    }

    public class ConvivirUsuarioDto
    {
        public int UserId { get; set; }
        public string Email { get; set; } = null!;
        public string? Nombres { get; set; }
    }

    public class ConvivirSesionDto
    {
        public string SessionToken { get; set; } = null!;
    }

    public class ConvivirRefreshResponseDto
    {
        public string AccessToken { get; set; } = null!;
    }

    public class ConvivirInvitacionDto
    {
        /// <summary>Con el que ingresará de ahí en adelante: la pantalla se lo muestra.</summary>
        public string? Dni { get; set; }
        public string Email { get; set; } = null!;
        public string? Nombres { get; set; }
    }

    public class ConvivirCrearContrasenaDto
    {
        public string Token { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string ConfirmPassword { get; set; } = null!;
    }

    public class ConvivirOlvideContrasenaDto
    {
        public string Dni { get; set; } = null!;
    }

    /// <summary>
    /// Cambio de contraseña desde la app, con sesión. Pide la actual (RF-24: acción sensible). El
    /// session token es el del teléfono: esa sesión se queda y las demás se cierran.
    /// </summary>
    public class ConvivirCambiarContrasenaDto
    {
        public string SessionToken { get; set; } = null!;
        public string PasswordActual { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string ConfirmPassword { get; set; } = null!;
    }

    /// <summary>Cuenta encontrada por DNI, antes de validar la contraseña.</summary>
    public class ConvivirCuentaDto
    {
        public int UserId { get; set; }
        /// <summary>Tal como está guardado: es con el que valida la contraseña el login de siempre.</summary>
        public string Email { get; set; } = null!;
        public bool TienePassword { get; set; }
        /// <summary><c>app_user.active</c>: false = desactivada desde Seguridad, o todavía sin contraseña.</summary>
        public bool Activa { get; set; }
    }
}
