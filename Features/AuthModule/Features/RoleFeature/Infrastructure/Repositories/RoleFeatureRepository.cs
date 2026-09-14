using Dapper;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.AuthModule.Role.Application.Dtos;
using Abril_Backend.Features.AuthModule.Role.Infrastructure.Interfaces;
using Abril_Backend.Features.AuthModule.Shared.Dtos;
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

        public async Task<PagedResult<RoleDto>> GetPaged(int page, int pageSize, string? search = null)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.Role.Where(r => r.State);

            // Búsqueda por palabras en cualquier orden: cada palabra debe aparecer en la
            // descripción (misma semántica que SearchInput.matches del frontend).
            // Así "obra admin" encuentra a "ADMINISTRADOR DE OBRA".
            if (!string.IsNullOrWhiteSpace(search))
            {
                var words = search.Trim().ToLower()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var word in words)
                {
                    var w = word;
                    query = query.Where(r => r.RoleDescription.ToLower().Contains(w));
                }
            }

            var totalRecords = await query.CountAsync();

            var data = await query
                .OrderByDescending(r => r.RoleId)
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

        /// <summary>
        /// Cabecera, usuarios y funcionalidades del rol en un solo viaje a la base. Se cuentan los
        /// mismos accesos que al iniciar sesión (user_role vivo), sin los usuarios eliminados.
        /// </summary>
        public async Task<RoleDetailDto?> GetDetail(int roleId)
        {
            using var ctx = _factory.CreateDbContext();

            const string sql = """
                SELECT role_id, role_description
                FROM role
                WHERE role_id = @roleId AND state;

                SELECT u.user_id,
                       u.email,
                       u.active,
                       p.full_name AS display_name
                FROM user_role ur
                JOIN app_user u ON u.user_id = ur.user_id AND u.state
                LEFT JOIN LATERAL (
                    SELECT pe.full_name
                    FROM person pe
                    WHERE pe.user_id = u.user_id AND pe.state
                    ORDER BY pe.person_id DESC
                    LIMIT 1
                ) p ON true
                WHERE ur.role_id = @roleId
                  AND ur.state
                ORDER BY COALESCE(p.full_name, u.email), u.user_id;

                SELECT f.feature_id, f.feature_key, f.module_id, m.module_name
                FROM role_feature rf
                JOIN feature f     ON f.feature_id = rf.feature_id
                LEFT JOIN module m ON m.module_id  = f.module_id
                WHERE rf.role_id = @roleId
                ORDER BY m.module_name NULLS LAST, f.feature_key;
                """;

            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();
            using var multi = await conn.QueryMultipleAsync(sql, new { roleId });

            var detail = await multi.ReadSingleOrDefaultAsync<RoleDetailDto>();
            if (detail == null) return null;

            detail.Users    = (await multi.ReadAsync<AccessUserDto>()).ToList();
            detail.Features = (await multi.ReadAsync<AccessFeatureDto>()).ToList();
            return detail;
        }

        public async Task UpdateRoleFeatures(int roleId, List<int> featureIds)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.ExecuteSqlAsync($"DELETE FROM role_feature WHERE role_id = {roleId}");
            foreach (var featureId in featureIds)
                await ctx.Database.ExecuteSqlAsync($"INSERT INTO role_feature (role_id, feature_id) VALUES ({roleId}, {featureId})");
        }

        public async Task<List<int>> GetUserIdsByRole(int roleId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Database
                .SqlQuery<int>($"SELECT DISTINCT user_id FROM user_role WHERE role_id = {roleId}")
                .ToListAsync();
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
