using Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos;
using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Interfaces
{
    public interface IProjectSubContractorService
    {
        Task Create(ProjectSubContractorCreateDTO dto, int page);
        Task<ProjectSubContractorFormDataDTO> GetFormData();
        Task<PagedResult<ProjectSubContractorDTO>> GetPaged(ProjectSubContractorFilterDTO filter);
    }
}