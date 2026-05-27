using Abril_Backend.Shared.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos;

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
            var rows = await _context.Project
                .Where(item => item.Active && item.State)
                .OrderBy(item => item.ProjectDescription)
                .Select(item => new
                {
                    item.ProjectId,
                    item.ProjectDescription,
                    item.LevelDescription,
                    item.CreatedDateTime,
                    item.CreatedUserId,
                    item.UpdatedDateTime,
                    item.UpdatedUserId,
                    item.Active
                })
                .ToListAsync();

            return rows.Select(item => new ProjectDTO
            {
                ProjectId          = item.ProjectId,
                ProjectDescription = item.ProjectDescription,
                LevelDescription   = item.LevelDescription,
                CreatedDateTime    = item.CreatedDateTime,
                CreatedUserId      = item.CreatedUserId,
                UpdatedDateTime    = item.UpdatedDateTime,
                UpdatedUserId      = item.UpdatedUserId,
                Active             = item.Active
            }).ToList();
        }

        public async Task<List<ProjectSimpleDTO>> GetAllFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var rows = await ctx.Project
                .Where(item => item.Active && item.State)
                .OrderBy(item => item.ProjectDescription)
                .Select(item => new
                {
                    item.ProjectId,
                    item.ProjectDescription
                })
                .ToListAsync();

            return rows.Select(item => new ProjectSimpleDTO
            {
                ProjectId          = item.ProjectId,
                ProjectDescription = item.ProjectDescription,
            }).ToList();
        }

        public async Task<PagedResult<ProjectDTO>> GetPaged(int page, bool? activo = null)
        {
            const int pageSize = 10;

            var baseQuery = _context.Project.Where(p => p.State).AsQueryable();
            if (activo.HasValue)
                baseQuery = baseQuery.Where(p => p.Active == activo.Value);

            var projectQuery = baseQuery.OrderByDescending(p => p.ProjectId);
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
                join u  in _context.User    on pr.UserId equals u.UserId
                join pe in _context.Person  on u.UserId  equals pe.UserId
                where projectIds.Contains(pr.ProjectId) && pr.Active && pr.State
                select new { pr.ProjectId, pe.FullName }
            ).ToListAsync();

            var residentsByProject = residents
                .GroupBy(r => r.ProjectId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.FullName).ToList());

            var data = projects.Select(p => new ProjectDTO
            {
                ProjectId          = p.ProjectId,
                ProjectDescription = p.ProjectDescription,
                LevelDescription   = p.LevelDescription,
                ResidentFullNames  = residentsByProject.GetValueOrDefault(p.ProjectId, new()),
                CreatedDateTime    = p.CreatedDateTime,
                CreatedUserId      = p.CreatedUserId,
                UpdatedDateTime    = p.UpdatedDateTime,
                UpdatedUserId      = p.UpdatedUserId,
                Active             = p.Active
            }).ToList();

            return new PagedResult<ProjectDTO>
            {
                Page         = page,
                PageSize     = pageSize,
                TotalRecords = totalRecords,
                TotalPages   = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data         = data
            };
        }

        public async Task<PagedResult<ProjectDTO>> GetPagedWithResidents(int page)
        {
            const int pageSize = 10;

            var projectQuery = _context.Project
                .Where(p => p.Active && p.State
                    && _context.ProjectResident.Any(pr => pr.ProjectId == p.ProjectId && pr.Active && pr.State))
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
                    p.FotoUrl,
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
                join u  in _context.User    on pr.UserId equals u.UserId
                join pe in _context.Person  on u.UserId  equals pe.UserId
                where projectIds.Contains(pr.ProjectId) && pr.Active && pr.State
                select new { pr.ProjectId, pe.FullName }
            ).ToListAsync();

            var residentsByProject = residents
                .GroupBy(r => r.ProjectId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.FullName).ToList());

            var data = projects.Select(p => new ProjectDTO
            {
                ProjectId          = p.ProjectId,
                ProjectDescription = p.ProjectDescription,
                LevelDescription   = p.LevelDescription,
                FotoUrl            = p.FotoUrl,
                ResidentFullNames  = residentsByProject.GetValueOrDefault(p.ProjectId, new()),
                CreatedDateTime    = p.CreatedDateTime,
                CreatedUserId      = p.CreatedUserId,
                UpdatedDateTime    = p.UpdatedDateTime,
                UpdatedUserId      = p.UpdatedUserId,
                Active             = p.Active
            }).ToList();

            return new PagedResult<ProjectDTO>
            {
                Page         = page,
                PageSize     = pageSize,
                TotalRecords = totalRecords,
                TotalPages   = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data         = data
            };
        }

        public async Task UpdateFotoUrlAsync(int projectId, string? fotoUrl)
        {
            var project = await _context.Project
                .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.State);
            if (project == null)
                throw new AbrilException("Proyecto no encontrado.", 404);
            project.FotoUrl = fotoUrl;
            project.UpdatedDateTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<string> GetProjectNameByProjectId(int projectId)
        {
            var projectName = await (
                from project in _context.Project
                where project.ProjectId == projectId
                      && project.Active && project.State
                select project.ProjectDescription
            ).FirstOrDefaultAsync();

            return projectName ?? string.Empty;
        }
    }
}
