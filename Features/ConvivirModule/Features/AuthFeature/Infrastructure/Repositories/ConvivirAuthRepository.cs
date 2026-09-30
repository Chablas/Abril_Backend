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

        public async Task<ConvivirCuentaDto?> GetCuentaPorDni(string dni)
        {
            using var ctx = _factory.CreateDbContext();
            var normalizado = dni.Trim();

            // person.document_identity_code es único en toda la tabla (sin filtro de state), así
            // que el DNI lleva a una sola persona y de ahí a su usuario. Se devuelve el correo tal
            // como está guardado: es con el que valida la contraseña el login de siempre
            // (AuthRepository.ValidateUserAsync). No filtra active: el servicio distingue «todavía
            // sin contraseña» de «desactivada».
            return await ctx.User
                .Where(u => u.State
                         && u.Email != null
                         && ctx.Person.Any(p => p.UserId == u.UserId
                                             && p.State
                                             && p.DocumentIdentityCode == normalizado))
                .OrderBy(u => u.UserId)
                .Select(u => new ConvivirCuentaDto
                {
                    UserId = u.UserId,
                    Email = u.Email!,
                    TienePassword = u.Password != null && u.Password != "",
                    Activa = u.Active
                })
                .FirstOrDefaultAsync();
        }

        public async Task<(string? Nombres, string? Dni)> GetPersona(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var persona = await ctx.Person
                .Where(p => p.UserId == userId && p.State)
                .OrderByDescending(p => p.PersonId)
                .Select(p => new { Nombres = p.FirstNames ?? p.FullName, Dni = p.DocumentIdentityCode })
                .FirstOrDefaultAsync();

            return (persona?.Nombres, persona?.Dni);
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
