using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Infrastructure.Repositories {
    public class UserRegistrationTokenRepository : IUserRegistrationTokenRepository
    {
        private readonly AppDbContext _context;

        public UserRegistrationTokenRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(UserRegistrationTokenDTO tokenDto)
        {
            var tokenModel = new UserRegistrationToken
            {
                UserId = tokenDto.UserId,
                Token = tokenDto.Token,
                CreatedDateTime = tokenDto.CreatedDateTime,
                ExpiresAt = tokenDto.ExpiresAt,
                Used = tokenDto.Used
            };
            await _context.UserRegistrationTokens.AddAsync(tokenModel);
            await _context.SaveChangesAsync();
        }

        public async Task<UserRegistrationTokenDTO?> GetValidTokenAsync(string token)
        {
            var tokenEntity = await _context.UserRegistrationTokens.Include(t => t.User)
                .FirstOrDefaultAsync(t =>
                t.Token == token &&
                !t.Used &&
                t.ExpiresAt > DateTime.UtcNow
            );
            var dto = new UserRegistrationTokenDTO
            {
                UserId = tokenEntity.UserId,
                Token = tokenEntity.Token,
                CreatedDateTime = tokenEntity.CreatedDateTime,
                ExpiresAt = tokenEntity.ExpiresAt,
                Used = tokenEntity.Used
            };
            return dto;
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