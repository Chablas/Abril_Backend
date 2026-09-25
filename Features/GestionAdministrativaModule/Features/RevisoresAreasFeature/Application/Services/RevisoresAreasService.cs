using Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Application.Services
{
    public class RevisoresAreasService : IRevisoresAreasService
    {
        private readonly IRevisoresAreasRepository _repo;

        public RevisoresAreasService(IRevisoresAreasRepository repo)
        {
            _repo = repo;
        }

        public Task<RevisoresAreasInicialDto> GetInitialDataAsync(int userId, bool verTodas)
            => _repo.GetInitialDataAsync(userId, verTodas);

        public Task<RevisoresAreaDetalleDto> GetDetalleAsync(int userId, bool verTodas, int areaScopeId, int? projectId)
            => _repo.GetDetalleAsync(userId, verTodas, areaScopeId, projectId);

        public async Task<RevisoresAreaDetalleDto> GuardarAsync(int userId, int areaScopeId, RevisoresAreaGuardarDto dto)
        {
            await _repo.GuardarAsync(areaScopeId, dto);
            // Guardar solo lo pueden quienes ven todas las áreas.
            return await _repo.GetDetalleAsync(userId, verTodas: true, areaScopeId, dto?.ProjectId);
        }
    }
}
