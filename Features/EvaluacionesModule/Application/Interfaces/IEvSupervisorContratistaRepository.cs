using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;

namespace Abril_Backend.Features.Evaluaciones.Application.Interfaces
{
    public interface IEvSupervisorContratistaRepository
    {
        Task<EvSupervisorContratistaInicioDto> GetInicioAsync(int evaluadorUserId);
        Task<EvEvaluacionSupervisorContratista> CreateAsync(
            EvEvaluacionSupervisorContratista eval, List<EvEvaluacionSupervisorContratistaDetalle> detalles);
        Task<bool> ExisteAsync(int periodoId, int supervisorWorkerId, int evaluadorUserId);
        Task<bool> ExisteNoAplicaAsync(int periodoId, int evaluadorUserId);
        Task RegistrarNoAplicaAsync(
            int periodoId, int evaluadorUserId, string motivo,
            int? proyectoId = null, int? supervisorWorkerId = null);
        Task<EvSupervisorContratistaVerInicioDto> GetVerInicioAsync(int? periodoId, int? proyectoId);
        Task<EvSupervisorContratistaDashboardDto> GetDashboardAsync(int? periodoId, int? proyectoId);
    }
}
