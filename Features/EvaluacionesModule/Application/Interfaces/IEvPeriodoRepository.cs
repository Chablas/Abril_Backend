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
    }
}
