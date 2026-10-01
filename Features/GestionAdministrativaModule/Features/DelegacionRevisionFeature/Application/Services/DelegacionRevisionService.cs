using Abril_Backend.Features.GestionAdministrativa.DelegacionRevision.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.DelegacionRevision.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.DelegacionRevision.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.RolesPorFuncion.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.DelegacionRevision.Application.Services
{
    public class DelegacionRevisionService : IDelegacionRevisionService
    {
        private readonly IDelegacionRevisionRepository _repo;
        private readonly IRolesPorFuncionService _rolesPorFuncion;

        public DelegacionRevisionService(IDelegacionRevisionRepository repo, IRolesPorFuncionService rolesPorFuncion)
        {
            _repo = repo;
            _rolesPorFuncion = rolesPorFuncion;
        }

        public Task<DelegacionInicialDto> GetInitialDataAsync(int userId)
            => _repo.GetInitialDataAsync(userId);

        public async Task UpdateAsync(int userId, int areaScopeId, int? projectId, int casoId, List<DelegacionAsignacionDto> revisores)
        {
            await _repo.UpdateAsync(userId, areaScopeId, projectId, casoId, revisores);

            // El suplente designado tiene que poder entrar a Gestión de Salidas para aprobar.
            await _rolesPorFuncion.SincronizarAsync(userId);
        }
    }
}
