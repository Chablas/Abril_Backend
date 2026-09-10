using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Interfaces
{
    /// <summary>
    /// "Mis Rendiciones": el autoservicio del trabajador sobre sus planillas ya rendidas. Acá vive
    /// todo lo que va DESPUÉS de rendir —adjuntar el Consolidado del S10, avisarle al revisor y
    /// seguir el reembolso hasta la firma—, porque esos pasos son de la planilla y no de cada
    /// salida suelta.
    /// </summary>
    public interface IRendicionService
    {
        /// <summary>Planillas propias ya filtradas, con los números de las tarjetas de ese conjunto.</summary>
        Task<RendicionListResultDto> GetByUserId(int userId, RendicionFiltersDto? filters = null);

        /// <summary>
        /// Datos de arranque de la pantalla: las opciones del filtro de periodo y los correos a los
        /// que de verdad va a salir el aviso de la primera revisión (para mostrarlos al confirmar
        /// el envío). Van juntos porque ninguno de los dos cambia al mover los filtros.
        /// </summary>
        Task<RendicionFilterDataDto> GetFilterData(int userId);

        /// <summary>Detalle de una planilla propia con el desglose de sus salidas.</summary>
        Task<RendicionDetalleDto> GetDetalle(int rendicionId, int userId);

        /// <summary>
        /// Envía la planilla a la PRIMERA revisión de la jefatura. Es el "Enviar" del requerimiento
        /// funcional (RG-30): la rendición se genera al rendir y queda "Lista para enviar" hasta
        /// que el trabajador la manda, para que pueda revisar el PDF antes.
        ///
        /// Dispara los dos correos del paso: la confirmación al propio solicitante y el aviso al
        /// jefe con los botones de aprobar y observar. Se puede repetir después de subsanar: al
        /// volver a generar la planilla, esta regresa a "Lista para enviar".
        /// </summary>
        /// <returns>Mensaje para mostrar en la pantalla.</returns>
        Task<string> EnviarAPrimeraRevision(int rendicionId, int userId);

        /// <summary>
        /// Vuelve a generar el PDF de una planilla OBSERVADA en primera revisión, con las capturas
        /// y los montos como quedaron después de corregirlos. La rendición es la misma —conserva su
        /// código REN-AAAA-NNNN y su número de planilla— y queda "Lista para enviar" para que el
        /// trabajador la reenvíe.
        /// </summary>
        /// <returns>Los bytes del PDF nuevo, para descargarlo desde la pantalla.</returns>
        Task<byte[]> RegenerarPlanilla(int rendicionId, int userId);

        /// <summary>
        /// Adjunta (o reemplaza) el PDF Consolidado del S10 de una planilla propia. El archivo
        /// cubre la planilla entera; si había salidas con el reembolso observado, vuelven a
        /// Pendiente porque volver a adjuntarlo es justamente la subsanación, y la corrección con
        /// el ERP que estuviera viva se cierra.
        ///
        /// Solo se habilita con la primera revisión APROBADA (RG-35): lo valida el servicio
        /// compartido que hace la subida.
        /// </summary>
        /// <param name="montoTotal">
        /// Importe total del consolidado. Tiene que coincidir con el monto de la planilla completa
        /// (todas sus salidas, que es lo que el consolidado cubre) o se rechaza con 400.
        /// </param>
        /// <param name="numeroGuia">Número de guía del S10 (texto, obligatorio).</param>
        Task<ConsolidadoS10Dto> UploadConsolidadoS10(
            int rendicionId, IFormFile file, decimal montoTotal, string numeroGuia, int userId);

        /// <summary>
        /// Avisa al jefe/revisor que la planilla ya tiene su Consolidado del S10 y el reembolso
        /// espera revisión. Se puede repetir a propósito (un correo se pierde, el jefe lo archiva
        /// sin leer): la fecha del último aviso queda a la vista para que no sea a ciegas.
        /// </summary>
        /// <returns>Mensaje para mostrar en la pantalla.</returns>
        Task<string> NotificarRevisor(int rendicionId, int userId);

        /// <summary>
        /// Le pide al Coordinador ERP que corrija el Consolidado del S10 (§10.5 / RG-21). Es el
        /// camino para cuando la jefatura observó el reembolso y el arreglo tiene que hacerse
        /// DENTRO del S10, donde el trabajador no tiene permiso.
        ///
        /// Es ALTERNATIVO a recargar el consolidado, no un paso obligatorio: si el trabajador puede
        /// arreglarlo él mismo, vuelve a adjuntarlo y el reembolso regresa a Pendiente sin pasar
        /// por acá.
        ///
        /// Manda al ERP la rendición, la guía, la observación de la jefatura y el motivo del
        /// trabajador, todo en un correo. A diferencia de los otros avisos de la pantalla, este NO
        /// es best-effort: si el correo no le puede llegar a nadie, la solicitud no se registra —
        /// una corrección que el ERP no ve es una espera infinita.
        /// </summary>
        /// <param name="motivo">El «MOTIVO *» del requerimiento. Obligatorio (CA-17).</param>
        /// <returns>La corrección creada, para que la pantalla la muestre sin recargar.</returns>
        Task<CorreccionS10Dto> SolicitarCorreccionS10(int rendicionId, string motivo, int userId);
    }
}
