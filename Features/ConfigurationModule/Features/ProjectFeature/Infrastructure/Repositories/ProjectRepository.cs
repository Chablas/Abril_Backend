using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.CostsModule.Shared.Models;

namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Infrastructure.Repositories
{
    public class ProjectRepository : IProjectRepository
    {
        private readonly AppDbContext _context;

        public ProjectRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<ProjectDto>> GetPaged(int page)
        {
            const int pageSize = 10;

            var query = _context.Project
                .Where(p => p.State)
                .OrderByDescending(p => p.ProjectId);

            var totalRecords = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProjectDto
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                    LevelDescription = p.LevelDescription,
                    ContributorId = p.ContributorId,
                    ContributorRuc = p.Contributor != null ? p.Contributor.ContributorRuc : null,
                    ContributorName = p.Contributor != null ? p.Contributor.ContributorName : null,
                    ContributorAddress = p.Contributor != null ? p.Contributor.ContributorAddress : null,
                    ContributorDistrict = p.Contributor != null ? p.Contributor.ContributorDistrict : null,
                    ContributorProvince = p.Contributor != null ? p.Contributor.ContributorProvince : null,
                    ContributorDepartment = p.Contributor != null ? p.Contributor.ContributorDepartment : null,
                    ProjectDistrict = p.ProjectDistrict,
                    ProjectProvince = p.ProjectProvince,
                    ProjectDepartment = p.ProjectDepartment,
                    ProjectLocation = p.ProjectLocation,
                    Active = p.Active
                })
                .ToListAsync();

            return new PagedResult<ProjectDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = data
            };
        }

        public async Task Create(ProjectCreateDto dto, int userId)
        {
            var existing = await _context.Project
                .FirstOrDefaultAsync(p => p.ProjectDescription.ToLower() == dto.ProjectDescription.Trim().ToLower());

            if (existing != null && existing.State)
                throw new AbrilException("Ya existe un proyecto con esa descripción.");

            if (existing != null && !existing.State)
            {
                existing.State = true;
                existing.Active = dto.Active;
                existing.LevelDescription = dto.LevelDescription?.Trim();
                existing.ContributorId = dto.ContributorId;
                existing.ProjectDistrict = dto.ProjectDistrict?.Trim();
                existing.ProjectProvince = dto.ProjectProvince?.Trim();
                existing.ProjectDepartment = dto.ProjectDepartment?.Trim();
                existing.ProjectLocation = dto.ProjectLocation?.Trim();
                existing.UpdatedDateTime = DateTime.UtcNow;
                existing.UpdatedUserId = userId;
                await _context.SaveChangesAsync();
                return;
            }

            var project = new Project
            {
                ProjectDescription = dto.ProjectDescription.Trim(),
                LevelDescription = dto.LevelDescription?.Trim(),
                ContributorId = dto.ContributorId,
                ProjectDistrict = dto.ProjectDistrict?.Trim(),
                ProjectProvince = dto.ProjectProvince?.Trim(),
                ProjectDepartment = dto.ProjectDepartment?.Trim(),
                ProjectLocation = dto.ProjectLocation?.Trim(),
                Active = dto.Active,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            _context.Project.Add(project);
            await _context.SaveChangesAsync();
        }

        public async Task Update(ProjectEditDto dto, int userId)
        {
            var project = await _context.Project
                .FirstOrDefaultAsync(p => p.ProjectId == dto.ProjectId);

            if (project == null)
                throw new AbrilException("El proyecto no existe.");

            var duplicate = await _context.Project
                .FirstOrDefaultAsync(p =>
                    p.ProjectDescription.ToLower() == dto.ProjectDescription.Trim().ToLower() &&
                    p.ProjectId != dto.ProjectId &&
                    p.State);

            if (duplicate != null)
                throw new AbrilException("Ya existe otro proyecto con la misma descripción.");

            project.ProjectDescription = dto.ProjectDescription.Trim();
            project.LevelDescription = dto.LevelDescription?.Trim();
            project.ContributorId = dto.ContributorId;
            project.ProjectDistrict = dto.ProjectDistrict?.Trim();
            project.ProjectProvince = dto.ProjectProvince?.Trim();
            project.ProjectDepartment = dto.ProjectDepartment?.Trim();
            project.ProjectLocation = dto.ProjectLocation?.Trim();
            project.Active = dto.Active;
            project.UpdatedDateTime = DateTime.UtcNow;
            project.UpdatedUserId = userId;

            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteSoftAsync(int projectId, int userId)
        {
            var project = await _context.Project
                .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.State);

            if (project == null)
                return false;

            project.State = false;
            project.Active = false;
            project.UpdatedDateTime = DateTime.UtcNow;
            project.UpdatedUserId = userId;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Contributor?> FindContributorByRuc(string ruc)
        {
            return await _context.Contributor
                .FirstOrDefaultAsync(c => c.ContributorRuc == ruc && c.State);
        }

        public async Task<Contributor> CreateContributor(string ruc, string name, string address, string economicActivity, string? district, string? province, string? department, int userId)
        {
            var contributor = new Contributor
            {
                ContributorRuc = ruc,
                ContributorName = name,
                ContributorAddress = address,
                ContributorEconomicActivityDescription = economicActivity,
                ContributorDistrict = district,
                ContributorProvince = province,
                ContributorDepartment = department,
                Active = true,
                State = true,
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId = userId
            };

            _context.Contributor.Add(contributor);
            await _context.SaveChangesAsync();
            return contributor;
        }

        public async Task UpdateContributorLocationAsync(int contributorId, string? district, string? province, string? department)
        {
            var contributor = await _context.Contributor.FindAsync(contributorId);
            if (contributor == null) return;

            contributor.ContributorDistrict = district;
            contributor.ContributorProvince = province;
            contributor.ContributorDepartment = department;
            contributor.UpdatedDateTime = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();
        }
    }
}
