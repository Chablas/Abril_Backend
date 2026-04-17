using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Interfaces;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Repositories
{
    public class ContractorRegistrationRepository : IContractorRegistrationRepository
    {
        private const int PendingContractorStateId = 1;

        private readonly IDbContextFactory<AppDbContext> _factory;

        public ContractorRegistrationRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task Create(CompanyCreateDto dto, int? userId, string? brochureUrl, string? fichaRucUrl, string? referencesUrl)
        {
            using var ctx = _factory.CreateDbContext();

            var company = await ctx.Company.FirstOrDefaultAsync(c => c.CompanyRuc == dto.CompanyRuc && c.State);
            if (company == null)
            {
                company = new Company
                {
                    CompanyRuc = dto.CompanyRuc,
                    CompanyName = dto.CompanyName,
                    CompanyAddress = dto.CompanyAddress,
                    CompanyEconomicActivityDescription = dto.CompanyEconomicActivityDescription,
                    CreatedDateTime = DateTimeOffset.UtcNow,
                    CreatedUserId = userId,
                    Active = true,
                    State = true
                };
                ctx.Company.Add(company);
                await ctx.SaveChangesAsync();
            }

            var contractor = new Contractor
            {
                CompanyId = company.CompanyId,
                ContractorStateId = PendingContractorStateId,
                BrochureFileUrl = brochureUrl,
                FichaRucFileUrl = fichaRucUrl,
                ReferencesListFileUrl = referencesUrl,
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            };

            foreach (var email in dto.CompanyEmails)
            {
                contractor.Emails.Add(new ContractorEmail
                {
                    Email = email,
                    CreatedDateTime = DateTimeOffset.UtcNow,
                    CreatedUserId = userId,
                    Active = true,
                    State = true
                });
            }

            ctx.Contractor.Add(contractor);
            await ctx.SaveChangesAsync();
        }
    }
}
