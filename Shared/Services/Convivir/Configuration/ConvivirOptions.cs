namespace Abril_Backend.Shared.Services.Convivir.Configuration
{
    /// <summary>
    /// Sección <c>Convivir</c> del appsettings: la app móvil de propietarios (Convivir Abril).
    /// Los plazos son solo el valor con el que NACE cada fila: lo que manda después es la columna
    /// <c>expires_at</c> de <c>user_session</c> / <c>user_password_token</c>, así que una sesión o
    /// un enlace se alarga (o se corta) con un UPDATE, sin tocar código ni el token del teléfono.
    /// </summary>
    public class ConvivirOptions
    {
        /// <summary>
        /// Base pública del backend a la que apunta el enlace del correo
        /// (<c>{UrlPublica}/api/v1/convivir/auth/abrir?token=...</c>). Si falta, se usa
        /// <c>BackendSettings:PublicUrl</c>. En desarrollo tiene que ser la IP de la PC en la red
        /// (no localhost): el enlace se abre desde el teléfono.
        /// </summary>
        public string? UrlPublica { get; set; }

        /// <summary>
        /// Prefijo del deep link de la app. En la app instalada es el <c>scheme</c> del app.json
        /// (<c>abrilconvivir://</c>); probando con Expo Go es <c>exp://IP:8081/--/</c>.
        /// </summary>
        public string EnlaceApp { get; set; } = "abrilconvivir://";

        /// <summary>Duración con la que nace una sesión de la app.</summary>
        public int SesionDias { get; set; } = 90;

        /// <summary>Vigencia del enlace de invitación (crear la contraseña por primera vez).</summary>
        public int InvitacionHoras { get; set; } = 168;

        /// <summary>Vigencia del enlace de «¿La olvidaste?».</summary>
        public int RecuperacionHoras { get; set; } = 1;
    }
}
