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
            var rows = await _context.Projects
                .Where(item => item.Activo)
                .OrderBy(item => item.Nombre)
                .Select(item => new
                {
                    item.Id,
                    item.Nombre,
                    item.CreatedAt,
                    item.UpdatedAt,
                    item.Activo
                })
                .ToListAsync();

            return rows.Select(item => new ProjectDTO
            {
                ProjectId = item.Id,
                ProjectDescription = item.Nombre ?? string.Empty,
                CreatedDateTime = item.CreatedAt ?? DateTime.MinValue,
                CreatedUserId = 0,
                UpdatedDateTime = item.UpdatedAt,
                UpdatedUserId = null,
                Active = item.Activo
            }).ToList();
        }
        public async Task<List<ProjectSimpleDTO>> GetAllFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var rows = await ctx.Projects
                .Where(item => item.Activo)
                .OrderBy(item => item.Nombre)
                .Select(item => new
                {
                    item.Id,
                    item.Nombre
                })
                .ToListAsync();

            return rows.Select(item => new ProjectSimpleDTO
            {
                ProjectId = item.Id,
                ProjectDescription = item.Nombre ?? string.Empty,
            }).ToList();
        }

        public async Task<PagedResult<ProjectDTO>> GetPaged(int page, bool? activo = null)
        {
            const int pageSize = 10;

            var baseQuery = _context.Projects.AsQueryable();
            if (activo.HasValue)
                baseQuery = baseQuery.Where(p => p.Activo == activo.Value);

            var projectQuery = baseQuery.OrderByDescending(p => p.Id);

            var totalRecords = await projectQuery.CountAsync();

            var projects = await projectQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.CreatedAt,
                    p.UpdatedAt,
                    p.Activo
                })
                .ToListAsync();

            var projectIds = projects.Select(p => p.Id).ToList();

            var residents = await (
                from pr in _context.ProjectResident
                join u in _context.User on pr.UserId equals u.UserId
                join pe in _context.Person on u.UserId equals pe.UserId
                where projectIds.Contains(pr.ProjectId) && pr.Active && pr.State
                select new { pr.ProjectId, pe.FullName }
            ).ToListAsync();

            var residentsByProject = residents
                .GroupBy(r => r.ProjectId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.FullName).ToList());

            var data = projects.Select(p => new ProjectDTO
            {
                ProjectId = p.Id,
                ProjectDescription = p.Nombre ?? string.Empty,
                LevelDescription = null,
                ResidentFullNames = residentsByProject.GetValueOrDefault(p.Id, new()),
                CreatedDateTime = p.CreatedAt ?? DateTime.MinValue,
                CreatedUserId = 0,
                UpdatedDateTime = p.UpdatedAt,
                UpdatedUserId = null,
                Active = p.Activo
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

            var projectQuery = _context.Projects
                .Where(p => p.Activo && _context.ProjectResident.Any(pr => pr.ProjectId == p.Id && pr.Active && pr.State))
                .OrderByDescending(p => p.Id);

            var totalRecords = await projectQuery.CountAsync();

            var projects = await projectQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.CreatedAt,
                    p.UpdatedAt,
                    p.Activo
                })
                .ToListAsync();

            var projectIds = projects.Select(p => p.Id).ToList();

            var residents = await (
                from pr in _context.ProjectResident
                join u in _context.User on pr.UserId equals u.UserId
                join pe in _context.Person on u.UserId equals pe.UserId
                where projectIds.Contains(pr.ProjectId) && pr.Active && pr.State
                select new { pr.ProjectId, pe.FullName }
            ).ToListAsync();

            var residentsByProject = residents
                .GroupBy(r => r.ProjectId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.FullName).ToList());

            var data = projects.Select(p => new ProjectDTO
            {
                ProjectId = p.Id,
                ProjectDescription = p.Nombre ?? string.Empty,
                LevelDescription = null,
                ResidentFullNames = residentsByProject.GetValueOrDefault(p.Id, new()),
                CreatedDateTime = p.CreatedAt ?? DateTime.MinValue,
                CreatedUserId = 0,
                UpdatedDateTime = p.UpdatedAt,
                UpdatedUserId = null,
                Active = p.Activo
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

        public async Task<Projects> Create(ProjectCreateDTO dto, int userId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(a => a.Nombre == dto.ProjectDescription.Trim());

            if (project != null && project.Activo)
                throw new AbrilException("El proyecto ya existe");

            if (project != null && !project.Activo)
            {
<<<<<<< HEAD
                project.State = true;
                project.Active = dto.Active;
                project.LevelDescription = dto.LevelDescription?.Trim();
                project.UpdatedDateTime = DateTime.UtcNow;
                project.UpdatedUserId = userId;
=======
                project.Activo = dto.Active;
                project.UpdatedAt = DateTime.UtcNow;
>>>>>>> origin/feature/arquitectura-comercial

                await _context.SaveChangesAsync();
                return project;
            }

            project = new Projects
            {
<<<<<<< HEAD
                ProjectDescription = dto.ProjectDescription.Trim(),
                LevelDescription = dto.LevelDescription?.Trim(),
                Active = dto.Active,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
=======
                Nombre = dto.ProjectDescription.Trim(),
                Activo = dto.Active,
                CreatedAt = DateTime.UtcNow
>>>>>>> origin/feature/arquitectura-comercial
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            return project;
        }

        public async Task<Projects> Update(ProjectEditDTO dto, int userId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == dto.ProjectId);

            if (project == null)
                throw new AbrilException("El proyecto no existe");

            var duplicate = await _context.Projects.FirstOrDefaultAsync(p =>
                p.Nombre == dto.ProjectDescription.Trim() &&
                p.Id != dto.ProjectId &&
                p.Activo
            );

            if (duplicate != null)
                throw new AbrilException("Ya existe otro proyecto con la misma descripción");

<<<<<<< HEAD
            project.ProjectDescription = dto.ProjectDescription.Trim();
            project.LevelDescription = dto.LevelDescription?.Trim();
            project.Active = dto.Active;
            project.UpdatedDateTime = DateTime.UtcNow;
            project.UpdatedUserId = userId;
=======
            project.Nombre = dto.ProjectDescription.Trim();
            project.Activo = dto.Active;
            project.UpdatedAt = DateTime.UtcNow;
>>>>>>> origin/feature/arquitectura-comercial

            await _context.SaveChangesAsync();

            return project;
        }

        public async Task<bool> DeleteSoftAsync(int projectId, int userId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(u => u.Id == projectId && u.Activo == true);

            if (project == null)
                return false;

            project.Activo = false;
            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<string> GetProjectNameByProjectId(int projectId)
        {
            var projectName = await (
                from project in _context.Projects
                where project.Id == projectId
                      && project.Activo
                select project.Nombre
            ).FirstOrDefaultAsync();

            return projectName ?? string.Empty;
        }

        public async Task UpdateEmails(int id, ProjectEmailsUpdateDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                throw new AbrilException("El proyecto no existe");

            if (dto.EmailResidente != null) project.EmailResidente = dto.EmailResidente;
            if (dto.EmailResponsable != null) project.EmailResponsable = dto.EmailResponsable;
            if (dto.EmailRrhh != null) project.EmailRrhh = dto.EmailRrhh;
            if (dto.EmailCoordSsoma != null) project.EmailCoordSsoma = dto.EmailCoordSsoma;
            if (dto.EmailCoordAdmin != null) project.EmailCoordAdmin = dto.EmailCoordAdmin;

            await _context.SaveChangesAsync();
        }
    }
}
