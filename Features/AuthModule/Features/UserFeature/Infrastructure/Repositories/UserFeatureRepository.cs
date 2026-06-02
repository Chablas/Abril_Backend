using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AuthModule.UserFeature.Application.Dtos;
using Abril_Backend.Features.AuthModule.UserFeature.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using UserModel = Abril_Backend.Infrastructure.Models.User;

namespace Abril_Backend.Features.AuthModule.UserFeature.Infrastructure.Repositories
{
    public class UserFeatureRepository : IUserFeatureRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public UserFeatureRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<PagedResult<UserListItemDto>> GetPaged(int page, int pageSize, string? search = null)
        {
            page = page < 1 ? 1 : page;
            using var ctx = _factory.CreateDbContext();

            var hasSearch = !string.IsNullOrWhiteSpace(search);
            var likePattern = hasSearch ? $"%{search!.Trim().ToLower()}%" : null;

            int totalRecords;
            List<UserBaseRow> baseRows;

            if (hasSearch)
            {
                var counts = await ctx.Database
                    .SqlQuery<int>($"""
                        SELECT COUNT(DISTINCT u.user_id)::int AS "Value"
                        FROM app_user u
                        LEFT JOIN person p ON p.user_id = u.user_id AND p.state = true
                        WHERE u.state = true
                          AND (
                            LOWER(COALESCE(p.full_name, '')) LIKE {likePattern}
                            OR LOWER(COALESCE(p.document_identity_code, '')) LIKE {likePattern}
                            OR LOWER(u.email) LIKE {likePattern}
                          )
                        """)
                    .ToListAsync();
                totalRecords = counts.FirstOrDefault();

                baseRows = await ctx.Database.SqlQuery<UserBaseRow>($"""
                    SELECT DISTINCT ON (u.user_id)
                        u.user_id,
                        u.email,
                        u.active,
                        CASE WHEN cu.contractor_user_id IS NOT NULL THEN 'CONTRATISTA'
                             WHEN p.person_id IS NOT NULL THEN 'PERSONA'
                             ELSE 'COLABORADOR' END AS user_type,
                        p.full_name AS display_name,
                        p.document_identity_code,
                        p.first_names,
                        p.first_last_name,
                        p.second_last_name,
                        p.phone_number
                    FROM app_user u
                    LEFT JOIN person p ON p.user_id = u.user_id AND p.state = true
                    LEFT JOIN contractor_user cu ON cu.user_id = u.user_id AND cu.state = true
                    WHERE u.state = true
                      AND (
                        LOWER(COALESCE(p.full_name, '')) LIKE {likePattern}
                        OR LOWER(COALESCE(p.document_identity_code, '')) LIKE {likePattern}
                        OR LOWER(u.email) LIKE {likePattern}
                      )
                    ORDER BY u.user_id DESC
                    LIMIT {pageSize} OFFSET {(page - 1) * pageSize}
                    """).ToListAsync();
            }
            else
            {
                totalRecords = await ctx.User.CountAsync(u => u.State);

                baseRows = await ctx.Database.SqlQuery<UserBaseRow>($"""
                    SELECT DISTINCT ON (u.user_id)
                        u.user_id,
                        u.email,
                        u.active,
                        CASE WHEN cu.contractor_user_id IS NOT NULL THEN 'CONTRATISTA'
                             WHEN p.person_id IS NOT NULL THEN 'PERSONA'
                             ELSE 'COLABORADOR' END AS user_type,
                        p.full_name AS display_name,
                        p.document_identity_code,
                        p.first_names,
                        p.first_last_name,
                        p.second_last_name,
                        p.phone_number
                    FROM app_user u
                    LEFT JOIN person p ON p.user_id = u.user_id AND p.state = true
                    LEFT JOIN contractor_user cu ON cu.user_id = u.user_id AND cu.state = true
                    WHERE u.state = true
                    ORDER BY u.user_id DESC
                    LIMIT {pageSize} OFFSET {(page - 1) * pageSize}
                    """).ToListAsync();
            }

            var userIds = baseRows.Select(r => r.UserId).ToList();

            var rolesData = await ctx.UserRole
                .Where(ur => userIds.Contains(ur.UserId) && ur.State)
                .Join(ctx.Role.Where(r => r.State),
                    ur => ur.RoleId,
                    r => r.RoleId,
                    (ur, r) => new { ur.UserId, r.RoleId, r.RoleDescription })
                .ToListAsync();

            var rolesMap = rolesData
                .GroupBy(r => r.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(r => new RoleItemDto { RoleId = r.RoleId, RoleDescription = r.RoleDescription }).ToList());

            var data = baseRows.Select(r => new UserListItemDto
            {
                UserId = r.UserId,
                Email = r.Email,
                Active = r.Active,
                UserType = r.UserType,
                DisplayName = r.DisplayName,
                DocumentIdentityCode = r.DocumentIdentityCode,
                FirstNames = r.FirstNames,
                FirstLastName = r.FirstLastName,
                SecondLastName = r.SecondLastName,
                PhoneNumber = r.PhoneNumber,
                Roles = rolesMap.TryGetValue(r.UserId, out var roles) ? roles : new()
            }).ToList();

            return new PagedResult<UserListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = data
            };
        }

        public async Task<UserModel> Create(UserFeatureCreateDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            UserModel? result = null;

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await ctx.Database.BeginTransactionAsync();
                try
                {
                    var person = await ctx.Person
                        .FirstOrDefaultAsync(p => p.DocumentIdentityCode == dto.DocumentIdentityCode && p.State);

                    if (person != null)
                    {
                        var userExists = await ctx.User.AnyAsync(u => u.UserId == person.UserId && u.State);
                        if (userExists)
                            throw new AbrilException("La persona ya tiene un usuario registrado.");
                    }
                    else
                    {
                        person = new Person
                        {
                            DocumentIdentityCode = dto.DocumentIdentityCode,
                            DocumentIdentityTypeId = 1,
                            FirstNames = dto.FirstNames,
                            FirstLastName = dto.FirstLastName,
                            SecondLastName = dto.SecondLastName,
                            FullName = $"{dto.FirstNames} {dto.FirstLastName} {dto.SecondLastName}",
                            PhoneNumber = dto.PhoneNumber,
                            Active = true,
                            State = true,
                            CreatedDateTime = DateTime.UtcNow,
                            CreatedUserId = dto.CreatedUserId
                        };
                        ctx.Person.Add(person);
                        await ctx.SaveChangesAsync();
                    }

                    var user = new UserModel
                    {
                        Email = dto.Email,
                        Active = false,
                        State = true,
                        EmailConfirmed = false,
                        CreatedDateTime = DateTime.UtcNow,
                        CreatedUserId = dto.CreatedUserId
                    };
                    ctx.User.Add(user);
                    await ctx.SaveChangesAsync();

                    person.UserId = user.UserId;
                    await ctx.SaveChangesAsync();

                    foreach (var roleId in dto.RoleIds)
                    {
                        ctx.UserRole.Add(new UserRole
                        {
                            UserId = user.UserId,
                            RoleId = roleId,
                            Active = true,
                            State = true,
                            CreatedDateTime = DateTime.UtcNow,
                            CreatedUserId = dto.CreatedUserId
                        });
                    }
                    await ctx.SaveChangesAsync();
                    await transaction.CommitAsync();
                    result = user;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            return result!;
        }

        public async Task Update(int userId, UserFeatureUpdateDto dto, int updatedUserId)
        {
            using var ctx = _factory.CreateDbContext();

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await ctx.Database.BeginTransactionAsync();
                try
                {
                    var user = await ctx.User.FirstOrDefaultAsync(u => u.UserId == userId && u.State)
                        ?? throw new AbrilException("Usuario no encontrado.", 404);

                    user.Email = dto.Email;
                    user.UpdatedDateTime = DateTime.UtcNow;
                    user.UpdatedUserId = updatedUserId;

                    var person = await ctx.Person.FirstOrDefaultAsync(p => p.UserId == userId && p.State);
                    if (person != null)
                    {
                        if (!string.IsNullOrWhiteSpace(dto.FirstNames))
                            person.FirstNames = dto.FirstNames;
                        if (!string.IsNullOrWhiteSpace(dto.FirstLastName))
                            person.FirstLastName = dto.FirstLastName;
                        if (!string.IsNullOrWhiteSpace(dto.SecondLastName))
                            person.SecondLastName = dto.SecondLastName;

                        var fullName = string.Join(" ", new[] { person.FirstNames, person.FirstLastName, person.SecondLastName }
                            .Where(n => !string.IsNullOrWhiteSpace(n)));
                        if (!string.IsNullOrWhiteSpace(fullName))
                            person.FullName = fullName;

                        person.PhoneNumber = dto.PhoneNumber;
                        person.UpdatedDateTime = DateTime.UtcNow;
                        person.UpdatedUserId = updatedUserId;
                    }

                    await ctx.SaveChangesAsync();

                    await ctx.Database.ExecuteSqlAsync($"DELETE FROM user_role WHERE user_id = {userId}");

                    foreach (var roleId in dto.RoleIds)
                    {
                        ctx.UserRole.Add(new UserRole
                        {
                            UserId = userId,
                            RoleId = roleId,
                            Active = true,
                            State = true,
                            CreatedDateTime = DateTime.UtcNow,
                            CreatedUserId = updatedUserId
                        });
                    }

                    await ctx.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (AbrilException)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task ToggleActive(int userId, int updatedUserId)
        {
            using var ctx = _factory.CreateDbContext();
            var user = await ctx.User.FirstOrDefaultAsync(u => u.UserId == userId && u.State)
                ?? throw new AbrilException("Usuario no encontrado.", 404);

            user.Active = !user.Active;
            user.UpdatedDateTime = DateTime.UtcNow;
            user.UpdatedUserId = updatedUserId;
            await ctx.SaveChangesAsync();
        }

        public async Task Delete(int userId, int updatedUserId)
        {
            using var ctx = _factory.CreateDbContext();
            var user = await ctx.User.FirstOrDefaultAsync(u => u.UserId == userId && u.State)
                ?? throw new AbrilException("Usuario no encontrado.", 404);

            user.State = false;
            user.UpdatedDateTime = DateTime.UtcNow;
            user.UpdatedUserId = updatedUserId;
            await ctx.SaveChangesAsync();
        }
    }

    internal class UserBaseRow
    {
        public int UserId { get; set; }
        public string Email { get; set; } = null!;
        public bool Active { get; set; }
        public string UserType { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? DocumentIdentityCode { get; set; }
        public string? FirstNames { get; set; }
        public string? FirstLastName { get; set; }
        public string? SecondLastName { get; set; }
        public int? PhoneNumber { get; set; }
    }
}
