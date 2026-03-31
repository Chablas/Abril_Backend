using Abril_Backend.Features.Adjudicaciones.Application.Dtos;

namespace Abril_Backend.Features.Adjudicaciones.Application.Interfaces
{
    public interface IProjectSubContractorService
    {
        Task Create(ProjectSubContractorCreateDTO dto, int page);
        Task<ProjectSubContractorFormDataDTO> GetFormData();
    }
}