using Abril_Backend.Application.DTOs.ArquitecturaComercial;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Application.Services
{
    public class ArquitecturaComercialService : IArquitecturaComercialService
    {
        private readonly IArquitecturaComercialRepository _repository;

        public ArquitecturaComercialService(IArquitecturaComercialRepository repository)
        {
            _repository = repository;
        }

        public async Task<ArqComercialDashboardDTO> GetDashboardData(string? semana, string? mes, int? proyectoId)
        {
            return await _repository.GetDashboardData(semana, mes, proyectoId);
        }

        public async Task<ArqComercialFiltersDTO> GetFilters()
        {
            return await _repository.GetFilters();
        }

        public async Task<List<ProyectoConActividadesDTO>> GetProyectosConActividades()
        {
            return await _repository.GetProyectosConActividades();
        }

        public async Task<List<SupervisorAcDTO>> GetSupervisoresAc()
        {
            return await _repository.GetSupervisoresAc();
        }
    }
}
