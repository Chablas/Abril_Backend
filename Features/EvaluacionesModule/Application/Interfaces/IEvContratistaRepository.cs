using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;

namespace Abril_Backend.Features.Evaluaciones.Application.Interfaces
{
    public interface IEvContratistaRepository
    {
        Task<EvContratistaInicioDto> GetInicioAsync(int userId);
        Task<EvContratistaVerInicioDto> GetVerInicioAsync(int? periodoId, int? proyectoId);
        Task<EvContratistaDashboardDto> GetDashboardAsync(int? periodoId, int? proyectoId);
        Task<EvEvaluacionContratista> CreateAsync(EvEvaluacionContratista eval, List<EvEvaluacionContratistaDetalle> detalles);
        Task<bool> ExisteAsync(int periodoId, int proyectoId, int contributorId, string areaNombre, int evaluadorUserId);
    }
}
