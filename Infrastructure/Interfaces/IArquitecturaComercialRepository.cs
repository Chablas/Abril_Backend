using Abril_Backend.Application.DTOs.ArquitecturaComercial;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IArquitecturaComercialRepository
    {
        Task<ArqComercialDashboardDTO> GetDashboardData(string? semana, string? mes, int? proyectoId);
        Task<ArqComercialFiltersDTO> GetFilters();
    }
}
