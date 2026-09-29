using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos;

namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Interfaces
{
    public interface IProjectService
    {
        Task<ProjectInitDto> GetInit(int page, int pageSize, string? ruc = null, string? razonSocial = null, string? projectDescription = null, bool? active = null, int? projectTipoId = null, int? projectCicloVidaId = null);
        Task<PagedResult<ProjectDto>> GetPaged(int page, int pageSize, string? ruc = null, string? razonSocial = null, string? projectDescription = null, bool? active = null, int? projectTipoId = null, int? projectCicloVidaId = null);
        Task Create(ProjectCreateDto dto, int userId, bool puedeAsignarResidente);
        Task Update(ProjectEditDto dto, int userId, bool puedeAsignarResidente);
        Task<bool> DeleteSoftAsync(int projectId, int userId);
        Task<ContributorLookupDto?> GetOrCreateCompanyByRuc(string ruc, int userId);
        Task<bool?> ToggleArquitecturaComercial(int projectId);
        Task<bool?> SetTieneUnidadDeProyectos(int projectId, bool value);
        Task<ProjectLookupsDto> GetLookups();
        Task<List<int>> GetMyProjectIds(int userId);
        Task<MyWorkerDto?> GetMyWorker(int userId);
    }
}
