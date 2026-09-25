namespace Abril_Backend.Shared.Services.Firma.Interfaces
{
    /// <summary>
    /// Verificación de Microsoft que acompaña a una firma: prueba que quien firma acaba de iniciar
    /// sesión en Microsoft con su contraseña y el segundo factor (Authenticator), no solo que tenía
    /// la sesión abierta. La exigen los endpoints que estampan la firma de la persona (Consolidados
    /// y Facturas).
    /// </summary>
    public interface IVerificacionMfaFirma
    {
        /// <summary>Header con el que el frontend manda el token (no va en Authorization: ahí viaja el JWT interno).</summary>
        const string Header = "X-Firma-Mfa";

        /// <summary>
        /// Valida el access token de Entra para el ámbito <c>Firmar</c> de la app. Lanza
        /// <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 403 si falta, no es
        /// válido, es de otra cuenta, no incluye MFA o el inicio de sesión ya no es reciente, y 503
        /// si Graph no responde al comprobar la cuenta. Nunca 401 (el frontend lo toma como sesión
        /// vencida y desloguea) ni 409 (Consolidados lo toma como «falta registrar la firma»).
        /// </summary>
        /// <param name="accion">Qué se firma, para el log (p. ej. <c>Consolidados.Aprobar [12,13]</c>).</param>
        Task VerificarAsync(string? token, int userId, string accion, CancellationToken ct = default);
    }
}
