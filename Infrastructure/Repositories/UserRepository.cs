using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using System.Linq;
using Microsoft.AspNetCore.Identity;

namespace Abril_Backend.Infrastructure.Repositories {
    public class UserRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IPasswordHasher<User> _passwordHasher;
        public UserRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory, IPasswordHasher<User> passwordHasher) {
            _context = contexto;
            _factory = factory;
            _passwordHasher = passwordHasher;
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

        public async Task<object> GetPagedFactory(int page, int pageSizeQuery)
        {
            int pageSize = pageSizeQuery;
            page = page < 1 ? 1 : page;

            using var ctx = _factory.CreateDbContext();

            var query =
                from u in ctx.User
                join p in ctx.Person
                    on u.PersonId equals p.PersonId
                join dit in ctx.DocumentIdentityType
                    on p.DocumentIdentityTypeId equals dit.DocumentIdentityTypeId
                where u.Active == true
                orderby u.UserId descending
                select new UserDTO
                {
                    UserId = u.UserId,
                    CreatedDateTime = u.CreatedDateTime,
                    CreatedUserId = u.CreatedUserId,
                    UpdatedDateTime = u.UpdatedDateTime,
                    UpdatedUserId = u.UpdatedUserId,
                    Active = u.Active,

                    Person = new PersonDTO
                    {
                        PersonId = p.PersonId,
                        DocumentIdentityCode = p.DocumentIdentityCode,
                        FirstNames = p.FirstNames,
                        FirstName = p.FirstName,
                        SecondName = p.SecondName,
                        FirstLastName = p.FirstLastName,
                        SecondLastName = p.SecondLastName,
                        FullName = p.FullName,
                        Email = p.Email,

                        CreatedDateTime = p.CreatedDateTime,
                        CreatedUserId = p.CreatedUserId,
                        UpdatedDateTime = p.UpdatedDateTime,
                        UpdatedUserId = p.UpdatedUserId,
                        Active = p.Active,

                        DocumentIdentityType = new DocumentIdentityTypeDTO
                        {
                            DocumentIdentityTypeId = dit.DocumentIdentityTypeId,
                            DocumentIdentityTypeDescription = dit.DocumentIdentityTypeDescription,
                            DocumentIdentityTypeAbbreviation = dit.DocumentIdentityTypeAbbreviation,

                            CreatedDateTime = dit.CreatedDateTime,
                            CreatedUserId = dit.CreatedUserId,
                            UpdatedDateTime = dit.UpdatedDateTime,
                            UpdatedUserId = dit.UpdatedUserId,
                            Active = dit.Active
                        }
                    }
                };

            var totalRecords = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new
            {
                page,
                pageSize,
                totalRecords,
                totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                data
            };
        }

        /*
        public async Task<List<UserDTO>> GetAllFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.User
                .OrderBy(item => item.UserDescription)
                .Select(item => new UserDTO
                {
                    UserId = item.UserId,
                    UserDescription = item.UserDescription,

                    CreatedDateTime = item.CreatedDateTime,
                    CreatedUserId = item.CreatedUserId,
                    UpdatedDateTime = item.UpdatedDateTime,
                    UpdatedUserId = item.UpdatedUserId,
                    Active = item.Active
                });
            return await registros.ToListAsync();
        }

        public async Task<object> GetPaged(int page)
        {
            const int pageSize = 10;

            var query = from user in _context.User
                        where user.State == true
                        orderby user.UserId descending
                        select new UserDTO
                        {
                            UserId = user.UserId,
                            UserDescription = user.UserDescription,
                            CreatedDateTime = user.CreatedDateTime,
                            CreatedUserId = user.CreatedUserId,
                            UpdatedDateTime = user.UpdatedDateTime,
                            UpdatedUserId = user.UpdatedUserId,
                            Active = user.Active
                        };

            var totalRecords = await query.CountAsync();

            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return new
            {
                page,
                pageSize,
                totalRecords,
                totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                data
            };
        }
        */
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
                    Active = dto.Active,
                    State = true,
                    EmailConfirmed = false,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = dto.CreatedUserId
                };

                _context.User.Add(user);
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
    }
}