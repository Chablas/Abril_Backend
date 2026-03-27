using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

namespace Abril_Backend.Infrastructure.Repositories
{
    public class AuthRepository : IAuthRepository
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthRepository(AppDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        public async Task<UserDTO?> ValidateUserAsync(string email, string password)
        {
            var query =
                from u in _context.User
                join p in _context.Person on u.PersonId equals p.PersonId
                join ur in _context.UserRole on u.UserId equals ur.UserId
                join r in _context.Role on ur.RoleId equals r.RoleId
                where p.Email == email &&
                      u.Active &&
                      u.State
                group new { u, p, r } by new
                {
                    u.UserId,
                    u.Password,
                    u.Active,
                    p.PersonId,
                    p.DocumentIdentityCode,
                    p.FullName,
                    p.Email
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

            var passwordHasher = new PasswordHasher<object>();

            var verify = passwordHasher.VerifyHashedPassword(
                new object(),
                result.Key.Password!,
                password
            );

            if (verify != PasswordVerificationResult.Success)
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
                    Email = result.Key.Email
                },
                Roles = result.Roles
            };
        }

        public async Task<UserSession> CreateSessionAsync(int userId)
        {
            var token = GenerateToken();

            var session = new UserSession
            {
                UserId = userId,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                Revoked = false,
                CreatedDateTime = DateTime.UtcNow
            };

            await _context.UserSession.AddAsync(session);
            await _context.SaveChangesAsync();

            return session;
        }

        public async Task<(int UserId, string Email)?> GetUserByEmailAsync(string email)
        {
            var result = await (
                from u in _context.User
                join p in _context.Person on u.PersonId equals p.PersonId
                where p.Email == email && u.Active && u.State
                select new { u.UserId, p.Email }
            ).FirstOrDefaultAsync();

            if (result == null)
                return null;

            return (result.UserId, result.Email);
        }

        public async Task<(int UserId, string Email)?> GetUserByIdAsync(int userId)
        {
            var result = await (
                from u in _context.User
                join p in _context.Person on u.PersonId equals p.PersonId
                where u.UserId == userId && u.State
                select new { u.UserId, p.Email }
            ).FirstOrDefaultAsync();

            if (result == null)
                return null;

            return (result.UserId, result.Email);
        }

        private string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}