using Abril_Backend.Features.Habilitacion.Infrastructure.Models;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces
{
    public interface IEmpresaContratistaRepository
    {
        Task<SsEmpresaContratista?> GetByIdAsync(int id);
        Task<(List<SsEmpresaContratista> Items, int Total)> GetPagedAsync(
            string? search, string? tipo, bool? activo, int page, int pageSize);
        Task<bool> ExisteRucEnEmpresaContratistaAsync(string ruc);
        Task<bool> ExisteRucEnContributorAsync(string ruc);
        Task<int?> GetContributorIdByRucAsync(string ruc);
        Task<SsEmpresaContratista> CreateAsync(SsEmpresaContratista empresa);
        Task<SsEmpresaContratista> UpdateAsync(SsEmpresaContratista empresa);
        Task<List<SsEmpresaProyecto>> GetProyectosAsync(int empresaId);
        Task AddProyectoAsync(SsEmpresaProyecto ep);
        Task RemoveProyectoAsync(int empresaId, int proyectoId);
    }
}
