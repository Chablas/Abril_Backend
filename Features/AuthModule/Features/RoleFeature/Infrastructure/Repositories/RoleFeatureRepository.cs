using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.AuthModule.Role.Application.Dtos;
using Abril_Backend.Features.AuthModule.Role.Infrastructure.Interfaces;
using RoleEntity = Abril_Backend.Infrastructure.Models.Role;

namespace Abril_Backend.Features.AuthModule.Role.Infrastructure.Repositories
{
    public class RoleFeatureRepository : IRoleFeatureRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public RoleFeatureRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<PagedResult<RoleDto>> GetPaged(int page, int pageSize)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.Role
                .Where(r => r.State)
                .OrderByDescending(r => r.RoleId);

            var totalRecords = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new RoleDto
                {
                    RoleId          = r.RoleId,
                    RoleDescription = r.RoleDescription,
                    CreatedDateTime = r.CreatedDateTime,
                    Active          = r.Active
                })
                .ToListAsync();

            return new PagedResult<RoleDto>
            {
                Page         = page,
                PageSize     = pageSize,
                TotalRecords = totalRecords,
                TotalPages   = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data         = data
            };
        }

        public async Task Create(RoleCreateDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var description = dto.RoleDescription.Trim().ToUpper();

            var existing = await ctx.Role
                .FirstOrDefaultAsync(r => r.RoleDescription.ToUpper() == description);

            if (existing != null && existing.State)
                throw new AbrilException("Ya existe un rol con esa descripción.");

            if (existing != null && !existing.State)
            {
                existing.RoleDescription = description;
                existing.State           = true;
                existing.Active          = true;
                existing.UpdatedDateTime = DateTime.UtcNow;
                existing.UpdatedUserId   = userId;
                await ctx.SaveChangesAsync();
                return;
            }

            var role = new RoleEntity
            {
                RoleDescription = description,
                Active          = true,
                State           = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId   = userId
            };

            ctx.Role.Add(role);
            await ctx.SaveChangesAsync();
        }

        public async Task<List<FeatureDto>> GetAllFeatures()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Database
                .SqlQuery<FeatureDto>($"""
                    SELECT f.feature_id, f.feature_key, f.module_id, m.module_name
                    FROM feature f
                    LEFT JOIN module m ON m.module_id = f.module_id
                    ORDER BY m.module_name, f.feature_key
                    """)
                .ToListAsync();
        }

        public async Task<List<int>> GetRoleFeatureIds(int roleId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Database
                .SqlQuery<int>($"SELECT feature_id FROM role_feature WHERE role_id = {roleId}")
                .ToListAsync();
        }

        public async Task UpdateRoleFeatures(int roleId, List<int> featureIds)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM role_feature WHERE role_id = {roleId}");
            foreach (var featureId in featureIds)
                await ctx.Database.ExecuteSqlAsync($"INSERT INTO role_feature (role_id, feature_id) VALUES ({roleId}, {featureId})");
        }

        public async Task UpdateRoleDescription(int roleId, string description, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var role = await ctx.Role.FirstOrDefaultAsync(r => r.RoleId == roleId && r.State)
                ?? throw new AbrilException("El rol no existe.");

            var normalized = description.Trim().ToUpper();

            var duplicate = await ctx.Role
                .FirstOrDefaultAsync(r => r.RoleId != roleId && r.State && r.RoleDescription.ToUpper() == normalized);

            if (duplicate != null)
                throw new AbrilException("Ya existe otro rol con esa descripción.");

            role.RoleDescription = normalized;
            role.UpdatedDateTime = DateTime.UtcNow;
            role.UpdatedUserId   = userId;
            await ctx.SaveChangesAsync();
        }
    }
}
