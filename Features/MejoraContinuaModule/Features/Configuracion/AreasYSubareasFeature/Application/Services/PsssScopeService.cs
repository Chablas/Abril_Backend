using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.AreasYSubareasFeature.Application.Interfaces;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.AreasYSubareasFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.AreasYSubareasFeature.Application.Services
{
    public class PsssScopeService : IPsssScopeService
    {
        private readonly IPsssScopeRepository _repo;

        public PsssScopeService(IPsssScopeRepository repo)
        {
            _repo = repo;
        }

        public Task<List<int>> GetByAreaAsync(int areaId) => _repo.GetByAreaAsync(areaId);
        public Task<List<int>> GetBySubAreaAsync(int subAreaId) => _repo.GetBySubAreaAsync(subAreaId);
        public Task UpdateByAreaAsync(int areaId, List<int> psssIds) => _repo.UpdateByAreaAsync(areaId, psssIds);
        public Task UpdateBySubAreaAsync(int subAreaId, List<int> psssIds) => _repo.UpdateBySubAreaAsync(subAreaId, psssIds);
    }
}
