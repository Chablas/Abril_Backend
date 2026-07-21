namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Resuelve la lista final de destinatarios (CC) de un correo del flujo de salidas a partir
    /// de la configuración editable (ga_correo_evento / ga_correo_regla), combinando los
    /// destinatarios base calculados en código con las inclusiones configuradas y quitando las
    /// exclusiones. Reemplaza los correos que antes estaban hardcodeados (GTH, recepción).
    /// </summary>
    public interface ICorreoSalidaRecipientResolver
    {
        /// <summary>
        /// Devuelve <c>(baseCc ∪ inclusiones configuradas) − exclusiones configuradas</c> para el
        /// correo <paramref name="eventoCodigo"/> (REVISOR, CONFIRMACION, APROBADA, RECHAZADA),
        /// sin duplicados (case-insensitive) ni vacíos. Es best-effort: ante cualquier error
        /// devuelve <paramref name="baseCc"/> tal cual (el correo debe enviarse igual).
        /// </summary>
        Task<List<string>> ResolveCcAsync(string eventoCodigo, IEnumerable<string>? baseCc = null);
    }
}
