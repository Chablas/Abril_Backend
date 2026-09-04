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
        /// Adjunta (o reemplaza) el PDF Consolidado del S10 de una planilla propia. El archivo
        /// cubre la planilla entera; si había salidas con el reembolso rechazado, vuelven a
        /// Pendiente porque volver a adjuntarlo es justamente la subsanación.
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
