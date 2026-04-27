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

        public async Task Create(ContributorCreateDto dto, int? userId, string? brochureUrl, string? fichaRucUrl, string? referencesUrl)
        {
            using var ctx = _factory.CreateDbContext();

            var contributor = await ctx.Contributor.FirstOrDefaultAsync(c => c.ContributorRuc == dto.ContributorRuc && c.State);
            if (contributor == null)
            {
                contributor = new Contributor
                {
                    ContributorRuc = dto.ContributorRuc,
                    ContributorName = dto.ContributorName,
                    ContributorAddress = dto.ContributorAddress,
                    ContributorEconomicActivityDescription = dto.ContributorEconomicActivityDescription,
                    ContributorDistrict   = dto.ContributorDistrict,
                    ContributorProvince   = dto.ContributorProvince,
                    ContributorDepartment = dto.ContributorDepartment,
                    CreatedDateTime = DateTimeOffset.UtcNow,
                    CreatedUserId = userId,
                    Active = true,
                    State = true
                };
                ctx.Contributor.Add(contributor);
                await ctx.SaveChangesAsync();
            }

            var contractor = new Contractor
            {
                ContributorId = contributor.ContributorId,
                ContractorStateId = PendingContractorStateId,
                BrochureFileUrl = brochureUrl,
                FichaRucFileUrl = fichaRucUrl,
                ReferencesListFileUrl = referencesUrl,
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            };

            foreach (var email in dto.ContributorEmails)
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
