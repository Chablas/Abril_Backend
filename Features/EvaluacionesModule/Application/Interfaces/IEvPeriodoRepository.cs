using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;

namespace Abril_Backend.Features.Evaluaciones.Application.Interfaces
{
    public interface IEvPeriodoRepository
    {
        Task<EvPeriodo?> GetActivoAsync();
        Task<List<EvPeriodo>> GetAllAsync();
        Task<EvPeriodo?> GetByIdAsync(int id);
        Task<EvPeriodo> CreateAsync(EvPeriodo periodo);
        Task UpdateAsync(EvPeriodo periodo);

        /// <summary>
        /// Desactiva períodos vencidos y crea/activa automáticamente el período
        /// vigente (ventana día 25 del mes -> día 4 del mes siguiente) si corresponde.
        /// Debe llamarse al inicio de cualquier proceso que dependa del período activo.
        /// </summary>
        Task SincronizarVigenciaAsync();
    }
}
