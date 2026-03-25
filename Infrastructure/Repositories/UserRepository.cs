using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Microsoft.AspNetCore.Identity;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Infrastructure.Repositories {
    public class UserRepository : IUserRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IPasswordHasher<User> _passwordHasher;
        public UserRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory, IPasswordHasher<User> passwordHasher) {
            _context = contexto;
            _factory = factory;
            _passwordHasher = passwordHasher;
        }

        public async Task<List<UserFilterDTO>> GetAllUsersFactory()
        {
            using var ctx = _factory.CreateDbContext();

            var data = await (
                from u in ctx.User
                join p in ctx.Person on u.PersonId equals p.PersonId
                where u.Active == true
                orderby p.FullName
                select new UserFilterDTO
                {
                    UserId = u.UserId,
                    FullName = p.FullName
                }
            ).ToListAsync();

            return data;
        }

        public async Task<List<UserPersonFilterDTO>> GetAllFilterFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var query = from u in ctx.User
                join p in ctx.Person
                on u.PersonId equals p.PersonId
                where (u.Active == true) && (u.State == true) && (p.Active == true) && (p.State == true) && (u.EmailConfirmed == true)
                select new UserPersonFilterDTO
                {
                    UserId = u.UserId,
                    PersonId = p.PersonId,
                    PersonFullName = p.FullName,
                };
                return await query.ToListAsync();
        }

        public async Task<PagedResult<UserDTO>> GetPagedFactory(int page, int pageSizeQuery)
        {
            int pageSize = pageSizeQuery;
            page = page < 1 ? 1 : page;

            using var ctx = _factory.CreateDbContext();

            var query =
                from u in ctx.User
                join p in ctx.Person on u.PersonId equals p.PersonId
                join dit in ctx.DocumentIdentityType on p.DocumentIdentityTypeId equals dit.DocumentIdentityTypeId
                join ur in ctx.UserRole on u.UserId equals ur.UserId
                join r in ctx.Role on ur.RoleId equals r.RoleId
                where u.State == true
                group new { u, p, dit, r } by new
                {
                    u.UserId,
                    u.Active,
                    p.PersonId,
                    p.DocumentIdentityCode,
                    p.FullName,
                    p.Email,
                    dit.DocumentIdentityTypeId,
                    dit.DocumentIdentityTypeDescription,
                    dit.DocumentIdentityTypeAbbreviation
                }
                into g
                orderby g.Key.UserId descending
                select new UserDTO
                {
                    UserId = g.Key.UserId,
                    Active = g.Key.Active,

                    Person = new PersonDTO
                    {
                        PersonId = g.Key.PersonId,
                        DocumentIdentityCode = g.Key.DocumentIdentityCode,
                        FullName = g.Key.FullName,
                        Email = g.Key.Email,

                        DocumentIdentityType = new DocumentIdentityTypeDTO
                        {
                            DocumentIdentityTypeId = g.Key.DocumentIdentityTypeId,
                            DocumentIdentityTypeDescription = g.Key.DocumentIdentityTypeDescription,
                            DocumentIdentityTypeAbbreviation = g.Key.DocumentIdentityTypeAbbreviation
                        }
                    },

                    Roles = g.Select(x => new RoleSimpleDTO
                    {
                        RoleId = x.r.RoleId,
                        RoleDescription = x.r.RoleDescription
                    }).ToList()
                };

            var totalRecords = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<UserDTO>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = data
            };
        }

        public async Task<User?> Create(UserCreateDTO dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var person = await _context.Person
                    .FirstOrDefaultAsync(p =>
                        p.DocumentIdentityCode == dto.DocumentIdentityCode &&
                        p.State == true
                    );

                if (person != null)
                {
                    var userExists = await _context.User
                        .AnyAsync(u =>
                            u.PersonId == person.PersonId &&
                            u.State == true
                        );

                    if (userExists)
                        return null;
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
                        Email = dto.Email,
                        PhoneNumber = dto.PhoneNumber,
                        Active = true,
                        State = true,
                        CreatedDateTime = DateTime.UtcNow,
                        CreatedUserId = dto.CreatedUserId
                    };

                    _context.Person.Add(person);
                    await _context.SaveChangesAsync();
                }

                var user = new User
                {
                    PersonId = person.PersonId,
                    Active = false,
                    State = true,
                    EmailConfirmed = false,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = dto.CreatedUserId
                };

                _context.User.Add(user);
                await _context.SaveChangesAsync();

                var userRole = new UserRole
                {
                    UserId = user.UserId,
                    RoleId = dto.RoleId,
                    Active = true,
                    State = true,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = dto.CreatedUserId
                };

                _context.UserRole.Add(userRole);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return user;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task SetPassword(int userId, string plainPassword)
        {
            var user = await _context.User.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                throw new Exception("Usuario no encontrado.");

            user.Password = _passwordHasher.HashPassword(user, plainPassword);

            user.Active = true;
            user.EmailConfirmed = true;
            user.UpdatedDateTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task<List<UserFilterDTO>> GetResidentsFullName()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = from user in ctx.User
                join user_role in ctx.UserRole on user.UserId equals user_role.UserId
                join role in ctx.Role on user_role.RoleId equals role.RoleId
                join person in ctx.Person on user.PersonId equals person.PersonId
                where (role.RoleDescription == "RESIDENTE") && (person.State == true) && (person.Active == true)
                && (user.State == true) && (user.Active == true)
                select new UserFilterDTO
                {
                    UserId = user.UserId,
                    FullName = person.FullName
                };

            return await registros.Distinct().ToListAsync();
        }
    }
}