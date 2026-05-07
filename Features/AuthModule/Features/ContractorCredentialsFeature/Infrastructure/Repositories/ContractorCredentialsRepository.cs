using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Features.AuthModule.ContractorCredentials.Application.Dtos;
using Abril_Backend.Features.AuthModule.ContractorCredentials.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.AuthModule.ContractorCredentials.Infrastructure.Repositories
{
    public class ContractorCredentialsRepository : IContractorCredentialsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ContractorCredentialsRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<ContractorForCredentialsDto?> GetByToken(string token)
        {
            using var ctx = _factory.CreateDbContext();

            var result = await (
                from ct in ctx.Contractor
                join c in ctx.Contributor on ct.ContributorId equals c.ContributorId
                where ct.ActivationToken == token
                      && ct.ActivationTokenExpiry > DateTime.UtcNow
                      && ct.Active
                select new ContractorForCredentialsDto
                {
                    ContractorId = ct.ContractorId,
                    ContributorName = c.ContributorName
                }
            ).FirstOrDefaultAsync();

            if (result == null) return null;

            result.Emails = await ctx.ContractorEmail
                .Where(e => e.ContractorId == result.ContractorId && e.Active)
                .Select(e => e.Email)
                .ToListAsync();

            return result;
        }

        public async Task Create(int contractorId, string email, string password)
        {
            using var ctx = _factory.CreateDbContext();

            var existingUser = await ctx.User.FirstOrDefaultAsync(u => u.Email == email);
            if (existingUser != null)
                throw new AbrilException("Ya existe un usuario con este correo electrónico.", 400);

            var user = new User
            {
                Email = email,
                EmailConfirmed = true,
                Active = true,
                State = true,
                CreatedDateTime = DateTime.UtcNow
            };

            var hasher = new PasswordHasher<User>();
            user.Password = hasher.HashPassword(user, password);

            ctx.User.Add(user);
            await ctx.SaveChangesAsync();

            ctx.ContractorUser.Add(new ContractorUser
            {
                ContractorId = contractorId,
                UserId = user.UserId,
                CreatedDateTime = DateTimeOffset.UtcNow,
                Active = true,
                State = true
            });

            ctx.UserRole.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = 11,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = user.UserId,
                Active = true,
                State = true
            });

            var contractor = await ctx.Contractor.FirstOrDefaultAsync(c => c.ContractorId == contractorId);
            if (contractor != null)
            {
                contractor.ActivationToken = null;
                contractor.ActivationTokenExpiry = null;
                contractor.UpdatedDateTime = DateTimeOffset.UtcNow;
            }

            await ctx.SaveChangesAsync();
        }
    }
}
