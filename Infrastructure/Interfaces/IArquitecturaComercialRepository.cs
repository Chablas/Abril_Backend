using Abril_Backend.Application.DTOs.ArquitecturaComercial;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IArquitecturaComercialRepository
    {
        Task<ArqComercialDashboardDTO> GetDashboardData(string? semana, string? mes, int? proyectoId);
        Task<ArqComercialFiltersDTO> GetFilters();
        Task<List<ProyectoConActividadesDTO>> GetProyectosConActividades();
        Task<List<SupervisorAcDTO>> GetSupervisoresAc();
        Task<ActividadListResponseDTO> GetActividades(int? proyectoId, string? tipo, int? etapaId, string? search, bool? soloActivas, int pagina, int porPagina);
    }
}
