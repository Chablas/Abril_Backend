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
                where ct.Active
                select new { ct, c, cs };

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
                    ContractorStateId = x.cs.ContractorStateId,
                    ContractorStateDescription = x.cs.ContractorStateDescription,
                    BrochureFileUrl = x.ct.BrochureFileUrl,
                    FichaRucFileUrl = x.ct.FichaRucFileUrl,
                    ReferencesListFileUrl = x.ct.ReferencesListFileUrl,
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
    }
}
