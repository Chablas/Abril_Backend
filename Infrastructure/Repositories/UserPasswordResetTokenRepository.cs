using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Infrastructure.Repositories
{
    public class UserPasswordResetTokenRepository : IUserPasswordResetTokenRepository
    {
        private readonly AppDbContext _context;

        public UserPasswordResetTokenRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(UserPasswordResetToken token)
        {
            await _context.UserPasswordResetTokens.AddAsync(token);
            await _context.SaveChangesAsync();
        }

        public async Task<UserPasswordResetToken?> GetValidTokenAsync(string token)
        {
            return await _context.UserPasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t =>
                    t.Token == token &&
                    !t.Used &&
                    t.ExpiresAt > DateTime.UtcNow
                );
        }

        public async Task InvalidatePreviousTokensAsync(int userId)
        {
            var tokens = await _context.UserPasswordResetTokens
                .Where(t => t.UserId == userId && !t.Used)
                .ToListAsync();

            foreach (var token in tokens)
                token.Used = true;
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
