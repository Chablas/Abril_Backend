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

            var projectQuery = _context.Project
                .Where(p => p.State)
                .OrderByDescending(p => p.ProjectId);

            var totalRecords = await projectQuery.CountAsync();

            var projects = await projectQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.ProjectId,
                    p.ProjectDescription,
                    p.LevelDescription,
                    p.CreatedDateTime,
                    p.CreatedUserId,
                    p.UpdatedDateTime,
                    p.UpdatedUserId,
                    p.Active
                })
                .ToListAsync();

            var projectIds = projects.Select(p => p.ProjectId).ToList();

            var residents = await (
                from pr in _context.ProjectResident
                join u in _context.User on pr.UserId equals u.UserId
                join pe in _context.Person on u.PersonId equals pe.PersonId
                where projectIds.Contains(pr.ProjectId) && pr.Active && pr.State
                select new { pr.ProjectId, pe.FullName }
            ).ToListAsync();

            var residentsByProject = residents
                .GroupBy(r => r.ProjectId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.FullName).ToList());

            var data = projects.Select(p => new ProjectDTO
            {
                ProjectId = p.ProjectId,
                ProjectDescription = p.ProjectDescription,
                LevelDescription = p.LevelDescription,
                ResidentFullNames = residentsByProject.GetValueOrDefault(p.ProjectId, new()),
                CreatedDateTime = p.CreatedDateTime,
                CreatedUserId = p.CreatedUserId,
                UpdatedDateTime = p.UpdatedDateTime,
                UpdatedUserId = p.UpdatedUserId,
                Active = p.Active
            }).ToList();

            return new PagedResult<ProjectDTO>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = data
            };
        }

        public async Task<PagedResult<ProjectDTO>> GetPagedWithResidents(int page)
        {
            const int pageSize = 10;

            var projectQuery = _context.Project
                .Where(p => p.State && _context.ProjectResident.Any(pr => pr.ProjectId == p.ProjectId && pr.Active && pr.State))
                .OrderByDescending(p => p.ProjectId);

            var totalRecords = await projectQuery.CountAsync();

            var projects = await projectQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.ProjectId,
                    p.ProjectDescription,
                    p.LevelDescription,
                    p.CreatedDateTime,
                    p.CreatedUserId,
                    p.UpdatedDateTime,
                    p.UpdatedUserId,
                    p.Active
                })
                .ToListAsync();

            var projectIds = projects.Select(p => p.ProjectId).ToList();

            var residents = await (
                from pr in _context.ProjectResident
                join u in _context.User on pr.UserId equals u.UserId
                join pe in _context.Person on u.PersonId equals pe.PersonId
                where projectIds.Contains(pr.ProjectId) && pr.Active && pr.State
                select new { pr.ProjectId, pe.FullName }
            ).ToListAsync();

            var residentsByProject = residents
                .GroupBy(r => r.ProjectId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.FullName).ToList());

            var data = projects.Select(p => new ProjectDTO
            {
                ProjectId = p.ProjectId,
                ProjectDescription = p.ProjectDescription,
                LevelDescription = p.LevelDescription,
                ResidentFullNames = residentsByProject.GetValueOrDefault(p.ProjectId, new()),
                CreatedDateTime = p.CreatedDateTime,
                CreatedUserId = p.CreatedUserId,
                UpdatedDateTime = p.UpdatedDateTime,
                UpdatedUserId = p.UpdatedUserId,
                Active = p.Active
            }).ToList();

            return new PagedResult<ProjectDTO>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = data
            };
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