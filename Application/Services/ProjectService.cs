using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Application.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IProjectRepository _repository;
        public ProjectService(IProjectRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ProjectScheduleSimpleDTO>> GetWithResidentByUserId(int userId)
        {
            var registros = await _repository.GetWithResidentByUserId(userId);
            return registros;
        }

        public async Task<PagedResult<ProjectDTO>> GetPaged(int page)
        {
            return await _repository.GetPaged(page);
        }

        public async Task Create(ProjectCreateDTO dto, int userId)
        {
            await _repository.Create(dto, userId);
        }

        public async Task Update(ProjectEditDTO dto, int userId)
        {
            await _repository.Update(dto, userId);
        }

        public async Task<bool> DeleteSoftAsync(int projecdId, int userId)
        {
            await _repository.DeleteSoftAsync(projecdId, userId);
            return true;
        }
    }
}