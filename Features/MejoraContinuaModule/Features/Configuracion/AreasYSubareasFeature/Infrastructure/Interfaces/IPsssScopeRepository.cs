namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.AreasYSubareasFeature.Infrastructure.Interfaces
{
    public interface IPsssScopeRepository
    {
        Task<List<int>> GetByAreaAsync(int areaId);
        Task<List<int>> GetBySubAreaAsync(int subAreaId);
        Task UpdateByAreaAsync(int areaId, List<int> psssIds);
        Task UpdateBySubAreaAsync(int subAreaId, List<int> psssIds);
    }
}
