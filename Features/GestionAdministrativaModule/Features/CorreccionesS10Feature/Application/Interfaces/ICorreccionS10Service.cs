using Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Application.Interfaces
{
    /// <summary>
    /// "Correcciones S10": la bandeja del Coordinador ERP, el paso del medio de la subsanación
    /// (§10.5 del requerimiento de salidas).
    ///
    /// Existe porque la corrección que pide la jefatura (o Tesorería) suele tener que hacerse DENTRO
    /// del S10, donde el consolidador no tiene permiso. El consolidador la pide desde Consolidados, el
    /// ERP la ejecuta afuera de Abril One y acá solo marca el check que le devuelve la pelota al
    /// consolidador (RG-22).
    ///
    /// El acceso lo decide el rol COORDINADOR ERP y nada más (lo exige el controller contra el
    /// token). Sin recorte por área: el responsable ERP es uno para toda la organización.
    /// </summary>
    public interface ICorreccionS10Service
    {
        /// <summary>Las correcciones vivas del filtro, con las tarjetas de ese mismo conjunto.</summary>
        Task<CorreccionS10ListResultDto> GetAll(CorreccionS10FiltersDto filters);

        /// <summary>Opciones de los filtros. 404 nunca: si no hay nada, vienen vacías.</summary>
        Task<CorreccionS10FilterDataDto> GetFilterData();

        /// <summary>
        /// Una corrección por id, con las rendiciones de su consolidado y sus salidas. 404 si no
        /// existe o si ya se cerró.
        /// </summary>
        Task<CorreccionS10DetalleDto> GetDetalle(int correccionId);

        /// <summary>
        /// El detalle de una salida de la bandeja (el ojo de la tabla de salidas): trayectos,
        /// capturas y adjuntos. Solo consulta: 404 si no está en la bandeja.
        /// </summary>
        Task<SolicitudSalidaDetalleDto> GetSalidaDetalle(int solicitudId);

        /// <summary>
        /// A quién le llegaría el aviso de atención si se confirmara la selección indicada. Se
        /// resuelve con la MISMA llamada que hace el envío, así que la confirmación no puede
        /// prometer un correo que Configuración dejó apagado.
        ///
        /// Best-effort: ante un error devuelve una lista vacía en vez de romper la confirmación.
        /// </summary>
        Task<List<CorreoAvisoPreviewDto>> GetCorreoPreview(List<int> correccionIds);

        /// <summary>
        /// El check de confirmación del Coordinador ERP (RG-22 / RF-OBS-07): registra que la
        /// corrección ya se hizo en el S10 —también para las demás planillas del mismo consolidado—
        /// y le avisa al consolidador que la pidió que puede recargar el Consolidado (RF-OBS-08).
        ///
        /// El aviso es best-effort: la confirmación queda guardada aunque el correo falle — el
        /// consolidador la ve igual en Consolidados. Al revés (correo enviado sin confirmar) lo
        /// mandaría a recargar un consolidado que sigue mal en el S10.
        /// </summary>
        Task<CorreccionS10BulkResultDto> Atender(AtenderCorreccionS10BulkDto accion, int erpUserId);
    }
}
