using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Infrastructure.Interfaces;
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
                join ur in ctx.UserRole on u.UserId equals ur.UserId
                join r in ctx.Role on ur.RoleId equals r.RoleId
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
                    Roles = g.Select(x => new RoleSimpleDTO
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
    }
}
