using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Application.Services
{
    public class ProjectResidentService : IProjectResidentService
    {
        private readonly IProjectResidentRepository _repository;
        public ProjectResidentService(IProjectResidentRepository repository)
        {
            _repository = repository;
        }
        /// <summary>Las obras donde sube el residente (IVTs y cuaderno de obra). Sin el rol RESIDENTE,
        /// ninguna: el Residente de Configuración → Proyectos también es destinatario de correos en
        /// proyectos que no son obra, y esa gente no sube.</summary>
        public async Task<List<ProjectSimpleDTO>> GetProjectByResidentUserId(int userId, bool esResidente)
        {
            if (!esResidente)
                return new List<ProjectSimpleDTO>();

            return await _repository.GetProjectByResidentUserId(userId);
        }
        public async Task<List<ProjectSimpleDTO>> GetProjectsDescription()
        {
            return await _repository.GetProjectsDescription();
        }
    }
}