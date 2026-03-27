using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IUserPasswordResetTokenRepository
    {
        Task CreateAsync(UserPasswordResetToken token);
        Task<UserPasswordResetToken?> GetValidTokenAsync(string token);
        Task InvalidatePreviousTokensAsync(int userId);
        Task SaveAsync();
    }
}
