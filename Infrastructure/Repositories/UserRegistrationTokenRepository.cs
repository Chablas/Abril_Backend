using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using System.Linq;

namespace Abril_Backend.Infrastructure.Repositories {
    public class UserRegistrationTokenRepository
    {
        private readonly AppDbContext _context;

        public UserRegistrationTokenRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(UserRegistrationToken token)
        {
            await _context.UserRegistrationTokens.AddAsync(token);
            await _context.SaveChangesAsync();
        }

        public async Task<UserRegistrationToken?> GetValidTokenAsync(string token)
        {
            var tokenEntity = await _context.UserRegistrationTokens.Include(t => t.User)
                .FirstOrDefaultAsync(t =>
                t.Token == token &&
                !t.Used &&
                t.ExpiresAt > DateTime.UtcNow
            );
            return tokenEntity;
        }

        public async Task InvalidateTokensByUserAsync(int userId)
        {
            var tokens = await _context.UserRegistrationTokens
                .Where(t => t.UserId == userId && !t.Used)
                .ToListAsync();

            foreach (var token in tokens)
            {
                token.Used = true;
            }
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}