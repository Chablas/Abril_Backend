using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Infrastructure.Repositories {
    public class ProjectRepository : IProjectRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public ProjectRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }
        public async Task<List<ProjectDTO>> GetAll()
        {
            var registros = _context.Project
                .Where(item => item.State)
                .OrderBy(item => item.ProjectDescription)
                .Select(item => new ProjectDTO
                {
                    ProjectId = item.ProjectId,
                    ProjectDescription = item.ProjectDescription,
                    CreatedDateTime = item.CreatedDateTime,
                    CreatedUserId = item.CreatedUserId,
                    UpdatedDateTime = item.UpdatedDateTime,
                    UpdatedUserId = item.UpdatedUserId,
                    Active = item.Active
                });
            return await registros.ToListAsync();
        }
        public async Task<List<ProjectSimpleDTO>> GetAllFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.Project
                .Where(item => item.Active)
                .OrderBy(item => item.ProjectDescription)
                .Select(item => new ProjectSimpleDTO
                {
                    ProjectId = item.ProjectId,
                    ProjectDescription = item.ProjectDescription,
                });
            return await registros.ToListAsync();
        }

        public async Task<PagedResult<ProjectDTO>> GetPaged(int page)
        {
            const int pageSize = 10;

            var query = from project in _context.Project
                        where project.State == true
                        orderby project.ProjectId descending
                        select new ProjectDTO
                        {
                            ProjectId = project.ProjectId,
                            ProjectDescription = project.ProjectDescription,
                            CreatedDateTime = project.CreatedDateTime,
                            CreatedUserId = project.CreatedUserId,
                            UpdatedDateTime = project.UpdatedDateTime,
                            UpdatedUserId = project.UpdatedUserId,
                            Active = project.Active
                        };

            var totalRecords = await query.CountAsync();

            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return new PagedResult<ProjectDTO>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = data
            };
        }

        public async Task<List<ProjectScheduleSimpleDTO>> GetWithResidentByUserId(int userId)
        {
            var registros = from pj in _context.Project
                join sd in _context.Schedule on pj.ProjectId equals sd.ProjectId
                where (sd.CreatedUserId == userId)
                && (pj.Active == true)
                && (pj.State == true)
                select new ProjectScheduleSimpleDTO
                {
                    ProjectId = pj.ProjectId,
                    ProjectDescription = pj.ProjectDescription,
                    ScheduleId = sd.ScheduleId
                };
            return await registros.ToListAsync();
        }

        public async Task<List<ProjectSimpleDTO>> GetProjectWithResidents()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = from item in ctx.Project
                where (item.ResidentUserId != null)
                select new ProjectSimpleDTO
                {
                    ProjectId = item.ProjectId,
                    ProjectDescription = item.ProjectDescription
                };
            return await registros.ToListAsync();
        }

        public async Task<Project> Create(ProjectCreateDTO dto, int userId)
        {
            var project = await _context.Project.FirstOrDefaultAsync(a => a.ProjectDescription == dto.ProjectDescription.Trim());

            if (project != null && project.State)
                throw new AbrilException("El proyecto ya existe");

            if (project != null && !project.State)
            {
                project.State = true;
                project.Active = dto.Active;
                project.UpdatedDateTime = DateTime.UtcNow;
                project.UpdatedUserId = userId;

                await _context.SaveChangesAsync();
                return project;
            }

            project = new Project
            {
                ProjectDescription = dto.ProjectDescription.Trim(),
                Active = dto.Active,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            _context.Project.Add(project);
            await _context.SaveChangesAsync();

            return project;
        }

        public async Task<Project> Update(ProjectEditDTO dto, int userId)
        {
            var project = await _context.Project.FirstOrDefaultAsync(p => p.ProjectId == dto.ProjectId);

            if (project == null)
                throw new AbrilException("El proyecto no existe");

            var duplicate = await _context.Project.FirstOrDefaultAsync(p =>
                p.ProjectDescription == dto.ProjectDescription.Trim() &&
                p.ProjectId != dto.ProjectId &&
                p.State
            );

            if (duplicate != null)
                throw new AbrilException("Ya existe otro proyecto con la misma descripción");

            project.ProjectDescription = dto.ProjectDescription.Trim();
            project.Active = dto.Active;
            project.UpdatedDateTime = DateTime.UtcNow;
            project.UpdatedUserId = userId;

            await _context.SaveChangesAsync();

            return project;
        }

        public async Task<bool> DeleteSoftAsync(int projectId, int userId)
        {
            var project = await _context.Project.FirstOrDefaultAsync(u => u.ProjectId == projectId && u.State == true);

            if (project == null)
                return false;

            project.State = false;
            project.Active = false;
            project.UpdatedDateTime = DateTime.UtcNow;
            project.UpdatedUserId = userId;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<string> GetProjectNameByProjectId(int projectId)
        {
            var projectName = await (
                from project in _context.Project
                where project.ProjectId == projectId
                      && project.State
                select project.ProjectDescription
            ).FirstOrDefaultAsync();

            return projectName;
        }
    }
}