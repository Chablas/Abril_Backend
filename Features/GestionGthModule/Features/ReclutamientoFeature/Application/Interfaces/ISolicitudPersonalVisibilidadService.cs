using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces
{
    /// <summary>Solicitud de Personal → Configuración → Visibilidad.</summary>
    public interface ISolicitudPersonalVisibilidadService
    {
        Task<SolicitudPersonalVisibilidadInicialDto> GetInitialData();

        /// <summary>Lo configurado a mano de un trabajador y lo que realmente ve hoy.</summary>
        Task<SolicitudPersonalVisibilidadDetalleDto> GetWorkerDetalle(int workerId);

        Task UpdateWorkerAsignaciones(
            int workerId, List<SolicitudPersonalVisibilidadAsignacionDto>? areas, int? userId);
    }
}
