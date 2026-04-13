using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Interfaces;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Repositories
{
    public class ContractorRegistrationRepository : IContractorRegistrationRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ContractorRegistrationRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task Create(CompanyCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var company = new Company
            {
                CompanyRuc = dto.CompanyRuc,
                CompanyName = dto.CompanyName,
                CompanyAddress = dto.CompanyAddress,
                CompanyEconomicActivityDescription = dto.CompanyEconomicActivityDescription,
                CompanyStateId = 1,
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            };

            foreach (var email in dto.CompanyEmails)
            {
                company.Emails.Add(new CompanyEmail
                {
                    Email = email,
                    CreatedDateTime = DateTimeOffset.UtcNow,
                    CreatedUserId = userId,
                    Active = true,
                    State = true
                });
            }

            ctx.Company.Add(company);
            await ctx.SaveChangesAsync();
        }
    }
}
