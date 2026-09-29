namespace Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Dtos
{
    public class ConvivirLoginDto
    {
        public string Email { get; set; } = null!;
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
        public string Email { get; set; } = null!;
    }

    /// <summary>Cuenta encontrada por correo, antes de validar la contraseña.</summary>
    public class ConvivirCuentaDto
    {
        public int UserId { get; set; }
        public string Email { get; set; } = null!;
        public bool TienePassword { get; set; }
    }
}
