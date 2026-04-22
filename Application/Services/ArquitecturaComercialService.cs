using System.Text.Json;
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

        public async Task<ActividadListResponseDTO> GetActividades(int? proyectoId, string? tipo, int? etapaId, string? search, bool? soloActivas, int pagina, int porPagina)
        {
            return await _repository.GetActividades(proyectoId, tipo, etapaId, search, soloActivas, pagina, porPagina);
        }

        public async Task<ActividadListItemDTO?> PatchActividad(int id, Dictionary<string, JsonElement> body)
        {
            return await _repository.PatchActividad(id, body);
        }

        public async Task<ReasignarEncargadoResultDTO?> ReasignarEncargado(int proyectoId)
        {
            return await _repository.ReasignarEncargado(proyectoId);
        }

        public async Task<GenerarActividadesResultDTO?> GenerarActividades(int proyectoId)
        {
            return await _repository.GenerarActividades(proyectoId);
        }

        public async Task<ProyectoConActividadesDTO?> PatchProyecto(int id, PatchProyectoDTO body)
        {
            return await _repository.PatchProyecto(id, body);
        }

        public async Task<List<GanttActividadDTO>> GetGantt(int? proyectoId, string? tipo, string? etapa, bool? soloActivas)
        {
            return await _repository.GetGantt(proyectoId, tipo, etapa, soloActivas);
        }
    }
}
