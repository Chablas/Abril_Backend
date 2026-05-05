using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.Contractors.ContractorManagement.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorManagement.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Contractors.ContractorManagement.Infrastructure.Repositories
{
    public class ContractorManagementRepository : IContractorManagementRepository
    {
        private const int PendingContractorStateId = 1;
        private const int ApprovedContractorStateId = 2;
        private const int RejectedContractorStateId = 3;

        private readonly IDbContextFactory<AppDbContext> _factory;

        public ContractorManagementRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<PagedResult<ContributorPagedDto>> GetPaged(ContributorFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();

            const int pageSize = 10;

            var query =
                from ct in ctx.Contractor
                join c in ctx.Contributor on ct.ContributorId equals c.ContributorId
                join cs in ctx.ContractorState on ct.ContractorStateId equals cs.ContractorStateId
                join p in ctx.Person on c.LegalRepresentativePersonId equals p.PersonId into personJoin
                from p in personJoin.DefaultIfEmpty()
                where ct.Active
                select new { ct, c, cs, p };

            if (!string.IsNullOrWhiteSpace(filter.ContributorName))
                query = query.Where(x => x.c.ContributorName.ToLower().Contains(filter.ContributorName.ToLower()));

            if (!string.IsNullOrWhiteSpace(filter.ContributorRuc))
                query = query.Where(x => x.c.ContributorRuc.Contains(filter.ContributorRuc));

            var totalRecords = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.ct.ContractorId)
                .Skip((filter.Page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ContributorPagedDto
                {
                    ContractorId = x.ct.ContractorId,
                    ContributorId = x.c.ContributorId,
                    ContributorRuc = x.c.ContributorRuc,
                    ContributorName = x.c.ContributorName,
                    ContributorAddress = x.c.ContributorAddress,
                    ContributorEconomicActivityDescription = x.c.ContributorEconomicActivityDescription,
                    ContributorDistrict   = x.c.ContributorDistrict,
                    ContributorProvince   = x.c.ContributorProvince,
                    ContributorDepartment = x.c.ContributorDepartment,
                    LegalRepresentativeDni      = x.p != null ? x.p.DocumentIdentityCode : null,
                    LegalRepresentativeFullName = x.p != null ? x.p.FullName : null,
                    LegalEntityRegistryNumber   = x.c.LegalEntityRegistryNumber,
                    ContractorStateId = x.cs.ContractorStateId,
                    ContractorStateDescription = x.cs.ContractorStateDescription,
                    BrochureFileUrl = x.ct.BrochureFileUrl,
                    FichaRucFileUrl = x.ct.FichaRucFileUrl,
                    ReferencesListFileUrl = x.ct.ReferencesListFileUrl,
                    HasUser = ctx.ContractorUser.Any(cu => cu.ContractorId == x.ct.ContractorId && cu.Active),
                    CreatedDateTime = x.ct.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime
                })
                .ToListAsync();

            var ids = items.Select(c => c.ContractorId).ToList();

            var emails = await ctx.ContractorEmail
                .Where(e => ids.Contains(e.ContractorId) && e.Active)
                .Select(e => new { e.ContractorId, e.Email })
                .ToListAsync();

            var emailsByContractor = emails
                .GroupBy(e => e.ContractorId)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Email).ToList());

            foreach (var item in items)
                item.Emails = emailsByContractor.GetValueOrDefault(item.ContractorId, new());

            var users = await (
                from cu in ctx.ContractorUser
                join u in ctx.User on cu.UserId equals u.UserId
                where ids.Contains(cu.ContractorId) && cu.Active
                select new { cu.ContractorId, cu.UserId, u.Email, cu.CreatedDateTime }
            ).ToListAsync();

            var usersByContractor = users
                .GroupBy(u => u.ContractorId)
                .ToDictionary(g => g.Key, g => g.Select(u => new ContractorUserItemDto
                {
                    UserId = u.UserId,
                    Email = u.Email,
                    CreatedDateTime = u.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime
                }).ToList());

            foreach (var item in items)
                item.Users = usersByContractor.GetValueOrDefault(item.ContractorId, new());

            return new PagedResult<ContributorPagedDto>
            {
                Page = filter.Page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = items
            };
        }

        public async Task Approve(int contractorId, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var contractor = await ctx.Contractor.FirstOrDefaultAsync(c => c.ContractorId == contractorId && c.ContractorStateId == PendingContractorStateId);
            if (contractor is null) return;
            contractor.ContractorStateId = ApprovedContractorStateId;
            contractor.UpdatedDateTime = DateTimeOffset.UtcNow;
            contractor.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task Reject(int contractorId, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var contractor = await ctx.Contractor.FirstOrDefaultAsync(c => c.ContractorId == contractorId && c.ContractorStateId == PendingContractorStateId);
            if (contractor is null) return;
            contractor.ContractorStateId = RejectedContractorStateId;
            contractor.UpdatedDateTime = DateTimeOffset.UtcNow;
            contractor.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task<ContractorWithEmailsDto?> GetWithEmails(int contractorId)
        {
            using var ctx = _factory.CreateDbContext();

            var result = await (
                from ct in ctx.Contractor
                join c in ctx.Contributor on ct.ContributorId equals c.ContributorId
                where ct.ContractorId == contractorId && ct.Active
                select new ContractorWithEmailsDto
                {
                    ContractorId = ct.ContractorId,
                    ContributorName = c.ContributorName,
                    ContractorStateId = ct.ContractorStateId
                }
            ).FirstOrDefaultAsync();

            if (result == null) return null;

            result.Emails = await ctx.ContractorEmail
                .Where(e => e.ContractorId == contractorId && e.Active)
                .Select(e => e.Email)
                .ToListAsync();

            return result;
        }

        public async Task SetActivationToken(int contractorId, string token, DateTime expiry)
        {
            using var ctx = _factory.CreateDbContext();
            var contractor = await ctx.Contractor.FirstOrDefaultAsync(c => c.ContractorId == contractorId);
            if (contractor is null) return;
            contractor.ActivationToken = token;
            contractor.ActivationTokenExpiry = expiry;
            contractor.UpdatedDateTime = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }
    }
}
