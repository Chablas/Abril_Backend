using Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.RolesPorFuncion.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Application.Services
{
    public class RevisoresAreasService : IRevisoresAreasService
    {
        private readonly IRevisoresAreasRepository _repo;
        private readonly IRolesPorFuncionService _rolesPorFuncion;

        public RevisoresAreasService(IRevisoresAreasRepository repo, IRolesPorFuncionService rolesPorFuncion)
        {
            _repo = repo;
            _rolesPorFuncion = rolesPorFuncion;
        }

        public Task<RevisoresAreasInicialDto> GetInitialDataAsync(int userId, bool verTodas)
            => _repo.GetInitialDataAsync(userId, verTodas);

        public Task<RevisoresAreaDetalleDto> GetDetalleAsync(int userId, bool verTodas, int areaScopeId, int? projectId)
            => _repo.GetDetalleAsync(userId, verTodas, areaScopeId, projectId);

        public async Task<RevisoresAreaDetalleDto> GuardarAsync(
            int userId, bool verTodas, int areaScopeId, RevisoresAreaGuardarDto dto)
        {
            await _repo.GuardarAsync(userId, verTodas, areaScopeId, dto);

            // Quien quedó como consolidador (o aprobador) tiene que poder entrar a su bandeja, y quien
            // dejó de serlo pierde CONSOLIDADOR.
            await _rolesPorFuncion.SincronizarAsync(userId);

            return await _repo.GetDetalleAsync(userId, verTodas, areaScopeId, dto?.ProjectId);
        }
    }
}
