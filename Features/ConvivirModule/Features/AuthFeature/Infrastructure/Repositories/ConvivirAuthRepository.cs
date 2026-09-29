using System.Security.Cryptography;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Dtos;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Infrastructure.Repositories
{
    public class ConvivirAuthRepository : IConvivirAuthRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ConvivirAuthRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<ConvivirCuentaDto?> GetCuentaPorEmail(string email)
        {
            using var ctx = _factory.CreateDbContext();
            var normalizado = email.Trim().ToLower();

            // En el celular el teclado suele poner la primera letra en mayúscula: se compara sin
            // distinguir mayúsculas y se devuelve el correo tal como está guardado, que es con el
            // que valida la contraseña el login de siempre (AuthRepository.ValidateUserAsync).
            return await ctx.User
                .Where(u => u.State && u.Active && u.Email != null && u.Email.ToLower() == normalizado)
                .OrderBy(u => u.UserId)
                .Select(u => new ConvivirCuentaDto
                {
                    UserId = u.UserId,
                    Email = u.Email!,
                    TienePassword = u.Password != null && u.Password != ""
                })
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetNombres(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Person
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.PersonId)
                .Select(p => p.FirstNames ?? p.FullName)
                .FirstOrDefaultAsync();
        }

        public async Task<(string Token, DateTime ExpiresAt)> CrearSesion(int userId, DateTime expiresAt)
        {
            using var ctx = _factory.CreateDbContext();

            // Misma tabla y mismo formato de token que la intranet (AuthRepository.CreateSessionAsync):
            // lo único propio es el vencimiento, que en la app es largo para no pedir la contraseña
            // cada día.
            var session = new UserSession
            {
                UserId = userId,
                Token = GenerarToken(),
                ExpiresAt = expiresAt,
                Revoked = false,
                CreatedDateTime = DateTime.UtcNow
            };

            ctx.UserSession.Add(session);
            await ctx.SaveChangesAsync();

            return (session.Token, session.ExpiresAt);
        }

        public async Task RevocarSesion(string sessionToken)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.UserSession
                .Where(s => s.Token == sessionToken && !s.Revoked)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true));
        }

        private static string GenerarToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}
