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

        /// <summary>Opciones de los filtros (periodos con planillas propias).</summary>
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
        /// cubre la planilla entera; si había salidas con el reembolso rechazado, vuelven a
        /// Pendiente porque volver a adjuntarlo es justamente la subsanación.
        ///
        /// Solo se habilita con la primera revisión APROBADA (RG-35): lo valida el servicio
        /// compartido que hace la subida.
        /// </summary>
        Task<ConsolidadoS10Dto> UploadConsolidadoS10(int rendicionId, IFormFile file, int userId);

        /// <summary>
        /// Avisa al jefe/revisor que la planilla ya tiene su Consolidado del S10 y el reembolso
        /// espera revisión. Se puede repetir a propósito (un correo se pierde, el jefe lo archiva
        /// sin leer): la fecha del último aviso queda a la vista para que no sea a ciegas.
        /// </summary>
        /// <returns>Mensaje para mostrar en la pantalla.</returns>
        Task<string> NotificarRevisor(int rendicionId, int userId);
    }
}
