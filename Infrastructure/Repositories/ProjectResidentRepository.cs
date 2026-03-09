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
                join project in ctx.Project on project_resident.ProjectId equals project.ProjectId
                where (project_resident.State == true) && (project_resident.Active == true)
                select new ProjectSimpleDTO
                {
                    ProjectId = project.ProjectId,
                    ProjectDescription = project.ProjectDescription
                };
            return await registros.ToListAsync();
        }

        public async Task<List<ProjectSimpleDTO>> GetProjectByResidentUserId(int userId)
        {
            var registros = from pj in _context.Project
                join up in _context.ProjectResident on pj.ProjectId equals up.ProjectId
                where (up.UserId == userId)
                && (pj.Active == true)
                && (pj.State == true)
                select new ProjectSimpleDTO
                {
                    ProjectId = pj.ProjectId,
                    ProjectDescription = pj.ProjectDescription,
                };
            return await registros.ToListAsync();
        }
    }
}