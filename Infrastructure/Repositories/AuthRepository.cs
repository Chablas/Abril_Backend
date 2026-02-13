using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

namespace Abril_Backend.Infrastructure.Repositories
{
    public class AuthRepository
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthRepository(AppDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        public async Task<User?> ValidateUserAsync(string email, string password)
        {
            var user = await _context.User
                .Include(u => u.Person)
                .FirstOrDefaultAsync(u =>
                    u.Person.Email == email &&
                    u.Active &&
                    u.State
                );

            if (user == null)
                return null;

            var result = _passwordHasher.VerifyHashedPassword(
                user,
                user.Password!,
                password
            );

            return result == PasswordVerificationResult.Success
                ? user
                : null;
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