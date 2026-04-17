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
                    CompanyId = p.CompanyId,
                    CompanyRuc = p.Company != null ? p.Company.CompanyRuc : null,
                    CompanyName = p.Company != null ? p.Company.CompanyName : null,
                    CompanyAddress = p.Company != null ? p.Company.CompanyAddress : null,
                    District = p.District,
                    Location = p.Location,
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
                existing.CompanyId = dto.CompanyId;
                existing.District = dto.District?.Trim();
                existing.Location = dto.Location?.Trim();
                existing.UpdatedDateTime = DateTime.UtcNow;
                existing.UpdatedUserId = userId;
                await _context.SaveChangesAsync();
                return;
            }

            var project = new Project
            {
                ProjectDescription = dto.ProjectDescription.Trim(),
                LevelDescription = dto.LevelDescription?.Trim(),
                CompanyId = dto.CompanyId,
                District = dto.District?.Trim(),
                Location = dto.Location?.Trim(),
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
            project.CompanyId = dto.CompanyId;
            project.District = dto.District?.Trim();
            project.Location = dto.Location?.Trim();
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

        public async Task<Company?> FindCompanyByRuc(string ruc)
        {
            return await _context.Company
                .FirstOrDefaultAsync(c => c.CompanyRuc == ruc && c.State);
        }

        public async Task<Company> CreateCompany(string ruc, string name, string address, string economicActivity, int userId)
        {
            var company = new Company
            {
                CompanyRuc = ruc,
                CompanyName = name,
                CompanyAddress = address,
                CompanyEconomicActivityDescription = economicActivity,
                Active = true,
                State = true,
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId = userId
            };

            _context.Company.Add(company);
            await _context.SaveChangesAsync();
            return company;
        }
    }
}
