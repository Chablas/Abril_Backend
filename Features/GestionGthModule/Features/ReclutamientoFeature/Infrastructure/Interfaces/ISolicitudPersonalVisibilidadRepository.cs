using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces
{
    /// <summary>
    /// Configuración propia de «qué áreas ve este trabajador» en Solicitud de Personal
    /// (<c>gth_solicitud_personal_visibilidad_area</c>). Acá solo se administra lo cargado a mano:
    /// lo que el trabajador ve de verdad lo decide <c>ISolicitudPersonalScopeResolver</c>.
    /// </summary>
    public interface ISolicitudPersonalVisibilidadRepository
    {
        /// <summary>Trabajadores (tabla) + árbol de áreas (filtro y modales), en una sola conexión.</summary>
        Task<SolicitudPersonalVisibilidadInicialDto> GetInitialData();

        /// <summary>
        /// Reemplaza la configuración propia de un trabajador por <paramref name="areaScopeIds"/>.
        /// Vacío = la quita entera y vuelve a mandar el algoritmo.
        /// </summary>
        Task UpdateWorkerAsignaciones(int workerId, List<int> areaScopeIds, int? userId);
    }
}
