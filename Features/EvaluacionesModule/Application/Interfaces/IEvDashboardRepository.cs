using Abril_Backend.Features.Evaluaciones.Application.Dtos;

namespace Abril_Backend.Features.Evaluaciones.Application.Interfaces
{
    public interface IEvDashboardRepository
    {
        Task<EvDashboardGerenciaDto> GetDashboardGerenciaAsync(int periodoId);
        Task<List<EvResidenteResumenDto>> GetResidentesResumenAsync(int periodoId);
        Task<List<EvAreaPromedioDto>> GetPromediosPorAreaAsync(int periodoId);
        Task<List<EvTendenciaDto>> GetTendenciaAsync(int anio);
        Task<List<EvPendienteDto>> GetPendientesAsync(int periodoId);
    }
}
