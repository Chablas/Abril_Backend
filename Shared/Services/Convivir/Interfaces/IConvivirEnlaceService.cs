namespace Abril_Backend.Shared.Services.Convivir.Interfaces
{
    public enum ConvivirEnlaceTipo
    {
        /// <summary>Cuenta recién creada (o «Reenviar» desde Seguridad → Usuarios o Propietarios).</summary>
        Invitacion,
        /// <summary>«¿La olvidaste?» desde la app.</summary>
        Recuperacion
    }

    /// <summary>
    /// Correo con el enlace que abre la app Convivir Abril en la pantalla de crear contraseña.
    /// Lo usan Seguridad → Usuarios (crear y reenviar, que de otro modo mandarían el enlace de la
    /// intranet), el módulo Propietarios y el login de la app (recuperación).
    /// </summary>
    public interface IConvivirEnlaceService
    {
        /// <summary>¿Tiene el usuario el rol PROPIETARIO vigente? No mira <c>app_user.active</c>.</summary>
        Task<bool> EsPropietarioAsync(int userId);

        /// <summary>
        /// Si el usuario es propietario, anula sus enlaces anteriores, crea uno nuevo en
        /// <c>user_password_token</c> y se lo manda por correo. Devuelve el correo al que se mandó,
        /// o <c>null</c> (sin hacer nada) si no es propietario, para que quien llama siga con su
        /// correo de siempre.
        /// </summary>
        Task<string?> EnviarEnlaceSiEsPropietarioAsync(int userId, ConvivirEnlaceTipo tipo);
    }
}
