namespace Abril_Backend.Infrastructure.Models
{
    public class FrontendSettings
    {
        public string SetPasswordUrl { get; set; }
        public string ContractorCredentialsUrl { get; set; }

        /// <summary>Página que realmente consume los tokens de app_user (SetPasswordDTO). SetPasswordUrl
        /// apunta a /auth/set-password, que hoy es la pantalla de activación de contratistas — para
        /// el flujo de cuentas con contraseña (ej. Capataz/Maestro de obra) se usa esta.</summary>
        public string CompleteRegistrationUrl =>
            (SetPasswordUrl ?? string.Empty).Replace("/auth/set-password", "/auth/complete-registration");
    }
}
