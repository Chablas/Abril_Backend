namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.AreasYSubareasFeature.Application.Interfaces
{
    public interface IPsssScopeService
    {
        Task<List<int>> GetByAreaAsync(int areaId);
        Task<List<int>> GetBySubAreaAsync(int subAreaId);
        Task UpdateByAreaAsync(int areaId, List<int> psssIds);
        Task UpdateBySubAreaAsync(int subAreaId, List<int> psssIds);
    }
}
