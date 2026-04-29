using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Infrastructure.Repositories {
    public class ProjectResidentRepository : IProjectResidentRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public ProjectResidentRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task<List<ProjectSimpleDTO>> GetProjectsDescription()
        {
            using var ctx = _factory.CreateDbContext();

            var registros = from project_resident in ctx.ProjectResident
                join project in ctx.Projects on project_resident.ProjectId equals project.Id
                where (project_resident.State == true) && (project_resident.Active == true)
                orderby project.Nombre
                select new ProjectSimpleDTO
                {
                    ProjectId = project.Id,
                    ProjectDescription = project.Nombre ?? string.Empty
                };
            return await registros.ToListAsync();
        }

        public async Task<List<ProjectSimpleDTO>> GetProjectByResidentUserId(int userId)
        {
            var registros = from pj in _context.Projects
                join up in _context.ProjectResident on pj.Id equals up.ProjectId
                where (up.UserId == userId)
                && (pj.Activo == true)
                select new ProjectSimpleDTO
                {
                    ProjectId = pj.Id,
                    ProjectDescription = pj.Nombre ?? string.Empty,
                };
            return await registros.ToListAsync();
        }
    }
}