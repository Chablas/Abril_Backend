using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Infrastructure.Interfaces;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftProfile.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Infrastructure.Repositories
{
    public class MicrosoftLoginRepository : IMicrosoftLoginRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public MicrosoftLoginRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<UserDTO?> GetUserByEmailAsync(string email)
        {
            using var ctx = _factory.CreateDbContext();

            var query =
                from u in ctx.User
                join p in ctx.Person on u.UserId equals p.UserId
                join ur in ctx.UserRole on u.UserId equals ur.UserId into urGroup
                from ur in urGroup.DefaultIfEmpty()
                join r in ctx.Role on ur.RoleId equals r.RoleId into rGroup
                from r in rGroup.DefaultIfEmpty()
                where u.Email == email && u.Active && u.State
                group new { u, p, r } by new
                {
                    u.UserId,
                    u.Active,
                    u.Email,
                    p.PersonId,
                    p.DocumentIdentityCode,
                    p.FullName
                }
                into g
                select new
                {
                    g.Key,
                    Roles = g.Where(x => x.r != null)
                              .Select(x => new RoleSimpleDTO
                              {
                                  RoleId = x.r.RoleId,
                                  RoleDescription = x.r.RoleDescription
                              }).ToList()
                };

            var result = await query.FirstOrDefaultAsync();
            if (result == null)
                return null;

            return new UserDTO
            {
                UserId = result.Key.UserId,
                Active = result.Key.Active,
                Person = new PersonDTO
                {
                    PersonId = result.Key.PersonId,
                    DocumentIdentityCode = result.Key.DocumentIdentityCode,
                    FullName = result.Key.FullName,
                    Email = result.Key.Email ?? string.Empty
                },
                Roles = result.Roles
            };
        }

        public async Task<UserDTO> CreateUserFromGraphAsync(MicrosoftProfileDto profile)
        {
            using var ctx = _factory.CreateDbContext();
            using var transaction = await ctx.Database.BeginTransactionAsync();

            try
            {
                var email = profile.Mail ?? profile.UserPrincipalName;

                // 1. Crear User primero (tiene Email)
                var user = new User
                {
                    Email = email,
                    Password = null,
                    EmailConfirmed = true,
                    Active = true,
                    State = true,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = null
                };

                ctx.User.Add(user);
                await ctx.SaveChangesAsync();

                // 2. Crear Person apuntando al User recién creado
                var person = new Person
                {
                    UserId = user.UserId,
                    DocumentIdentityTypeId = null,
                    DocumentIdentityCode = null,
                    FirstNames = profile.GivenName?.ToUpper(),
                    FirstLastName = profile.Surname?.ToUpper(),
                    FullName = profile.DisplayName.ToUpper(),
                    Active = true,
                    State = true,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = null
                };

                ctx.Person.Add(person);
                await ctx.SaveChangesAsync();

                await transaction.CommitAsync();

                return new UserDTO
                {
                    UserId = user.UserId,
                    Active = user.Active,
                    Person = new PersonDTO
                    {
                        PersonId = person.PersonId,
                        DocumentIdentityCode = null,
                        FullName = person.FullName,
                        Email = email
                    },
                    Roles = new()
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
