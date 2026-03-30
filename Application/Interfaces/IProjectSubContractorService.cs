using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Application.Interfaces
{
    public interface IProjectSubContractorService
    {
        Task Create(ProjectSubContractorCreateDTO dto, int page);
        Task<ProjectSubContractorFormDataDTO> GetFormData();
    }
}