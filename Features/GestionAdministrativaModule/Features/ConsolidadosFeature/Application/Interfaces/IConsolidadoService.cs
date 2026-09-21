using Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Interfaces
{
    /// <summary>
    /// "Consolidados": los Consolidados del S10 del alcance del usuario. Dos roles trabajan acá y
    /// cada uno tiene lo suyo:
    ///
    ///   • <b>La jefatura</b> de los trabajadores decide el reembolso (aprobar —que ES firmar— u
    ///     observar). Nadie más: ni el consolidador, ni quien solo tiene visibilidad amplia.
    ///   • <b>El consolidador</b> que lo adjuntó le avisa a la jefatura que lo está esperando y, si se
    ///     lo observan, le pide la corrección al Coordinador ERP. Todos los correos de vuelta
    ///     (aprobado, observado por la jefatura o por Tesorería) le llegan a él.
    ///
    /// Se separó de Gestión de Rendiciones porque lo que se decide acá es el DOCUMENTO del S10, que
    /// puede cubrir varias planillas a la vez: decidirlo planilla por planilla dejaba un reembolso
    /// partido y a la jefatura firmando el mismo PDF varias veces.
    /// </summary>
    public interface IConsolidadoService
    {
        Task<ConsolidadoListResultDto> GetAll(ConsolidadoFiltersDto filters);

        Task<ConsolidadoFilterDataDto> GetFilterData(ConsolidadoFiltersDto scope);

        Task<ConsolidadoDetalleDto> GetDetalle(int consolidadoId, ConsolidadoFiltersDto scope);

        /// <summary>
        /// El detalle de UNA salida de los consolidados del alcance (el ojo de la tabla de salidas
        /// del detalle), para ver sus capturas y montos antes de decidir. Solo consulta: 404 si la
        /// salida no está rendida o no está en su alcance.
        /// </summary>
        Task<SolicitudSalidaDetalleDto> GetSalidaDetalle(int solicitudId, ConsolidadoFiltersDto scope);

        /// <summary>
        /// Qué correos saldrían con la acción indicada sobre esa selección y a quién. Lo resuelve con
        /// las mismas llamadas que hace el envío, así que la confirmación no puede prometer un
        /// correo que Configuración → Correos dejó fuera.
        /// </summary>
        Task<List<CorreoAvisoPreviewDto>> GetCorreoPreview(
            ConsolidadoCorreoPreviewRequestDto request, ConsolidadoFiltersDto scope);

        /// <summary>
        /// La jefatura decide el reembolso de los consolidados seleccionados. Aprobar ES firmar:
        /// estampa la firma en la planilla y en el consolidado, deja las salidas en "Firmado" y avisa
        /// a Tesorería. Solo toca las salidas de los trabajadores de los que el usuario es la
        /// jefatura (403 si no hay ninguna) y le avisa al consolidador.
        /// </summary>
        Task<ReembolsoBulkResultDto> DecidirReembolso(
            ConsolidadoAccionDto accion, bool aprobar, ConsolidadoFiltersDto scope, int reviewerUserId);

        /// <summary>
        /// El consolidador le avisa a la jefatura que el consolidado tiene reembolsos esperando su
        /// visto bueno. Se puede repetir a propósito (un correo se pierde); la fecha del último aviso
        /// queda a la vista. Este aviso ES el correo: si no le llega a nadie, responde 409.
        /// </summary>
        /// <returns>Mensaje para mostrar en la pantalla.</returns>
        Task<string> NotificarJefatura(int consolidadoId, ConsolidadoFiltersDto scope, int userId);

        /// <summary>
        /// El consolidador le pide al Coordinador ERP que corrija el Consolidado del S10 observado
        /// (§10.5 / RG-21). Es el camino ALTERNATIVO a volver a adjuntarlo corregido, para cuando el
        /// arreglo tiene que hacerse dentro del S10. Tampoco es best-effort: sin un ERP a quien
        /// escribirle, la solicitud no se registra (409).
        /// </summary>
        /// <param name="motivo">El «MOTIVO *» del requerimiento. Obligatorio (CA-17).</param>
        /// <returns>Mensaje para mostrar en la pantalla.</returns>
        Task<string> SolicitarCorreccionS10(int consolidadoId, string motivo, ConsolidadoFiltersDto scope, int userId);

        /// <summary>
        /// El consolidador reemplaza el Consolidado del S10 —normalmente porque se lo observaron—.
        /// Es el ÚNICO lugar donde se reemplaza: Gestión de Rendiciones solo adjunta el primero. El
        /// documento nuevo cubre las planillas que siguen con el reembolso abierto (las decididas se
        /// quedan con el que se firmó), hereda el código CON y deja el reembolso Pendiente; en el
        /// mismo paso se le avisa a la jefatura (best-effort, igual que al adjuntar).
        /// </summary>
        Task<ConsolidadoS10UploadResultDto> ReemplazarConsolidado(
            int consolidadoId, IFormFile file, decimal montoTotal, string numeroReembolso,
            ConsolidadoFiltersDto scope, int userId);
    }
}
