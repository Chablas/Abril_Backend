using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Application.DTOs;

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
    }
}