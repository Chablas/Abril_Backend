namespace Abril_Backend.Shared.Services.Firma.Interfaces
{
    /// <summary>
    /// Verificación de Microsoft que acompaña a una firma: prueba que quien firma acaba de iniciar
    /// sesión en Microsoft con el segundo factor (contraseña + Authenticator, o un passkey), no solo
    /// que tenía la sesión abierta. La exigen los endpoints que estampan la firma de la persona
    /// (Consolidados y Facturas).
    ///
    /// Una verificación sirve para UNA firma (pedido del usuario: «siempre que se vaya a firmar,
    /// que pida el 2FA»): la siguiente firma tiene que volver a pasar por Microsoft.
    /// </summary>
    public interface IVerificacionMfaFirma
    {
        /// <summary>Header con el que el frontend manda el token (no va en Authorization: ahí viaja el JWT interno).</summary>
        const string Header = "X-Firma-Mfa";

        /// <summary>
        /// Estampa una firma con la verificación de Microsoft: valida el access token de Entra para
        /// el ámbito <c>Firmar</c> de la app, ejecuta <paramref name="firmar"/> y, si terminó bien,
        /// da el token por usado. Si <paramref name="firmar"/> falla (p. ej. el 409 de «todavía no
        /// registraste tu firma»), el token sigue sirviendo para reintentar ESA firma.
        ///
        /// Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 403 si el token
        /// falta, no es válido, es de otra cuenta, no incluye MFA, el inicio de sesión ya no es
        /// reciente o ya se usó en otra firma, y 503 si Graph no responde al comprobar la cuenta.
        /// Nunca 401 (el frontend lo toma como sesión vencida y desloguea) ni 409 (Consolidados lo
        /// toma como «falta registrar la firma»).
        /// </summary>
        /// <param name="accion">Qué se firma, para el log (p. ej. <c>Consolidados.Aprobar [12,13]</c>).</param>
        Task<T> FirmarAsync<T>(string? token, int userId, string accion, Func<Task<T>> firmar, CancellationToken ct = default);
    }
}
