using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.Contractors.ContractorManagement.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorManagement.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Contractors.ContractorManagement.Infrastructure.Repositories
{
    public class ContractorManagementRepository : IContractorManagementRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ContractorManagementRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<PagedResult<CompanyPagedDto>> GetPaged(CompanyFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();

            const int pageSize = 10;

            var query =
                from c in ctx.Company
                join cs in ctx.CompanyState on c.CompanyStateId equals cs.CompanyStateId
                where c.Active
                select new { c, cs };

            if (!string.IsNullOrWhiteSpace(filter.CompanyName))
                query = query.Where(x => x.c.CompanyName.ToLower().Contains(filter.CompanyName.ToLower()));

            if (!string.IsNullOrWhiteSpace(filter.CompanyRuc))
                query = query.Where(x => x.c.CompanyRuc.Contains(filter.CompanyRuc));

            var totalRecords = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.c.CompanyId)
                .Skip((filter.Page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new CompanyPagedDto
                {
                    CompanyId = x.c.CompanyId,
                    CompanyRuc = x.c.CompanyRuc,
                    CompanyName = x.c.CompanyName,
                    CompanyAddress = x.c.CompanyAddress,
                    CompanyEconomicActivityDescription = x.c.CompanyEconomicActivityDescription,
                    CompanyStateId = x.cs.CompanyStateId,
                    CompanyStateDescription = x.cs.CompanyStateDescription,
                    CreatedDateTime = x.c.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime
                })
                .ToListAsync();

            var ids = items.Select(c => c.CompanyId).ToList();

            var emails = await ctx.CompanyEmail
                .Where(e => ids.Contains(e.CompanyId) && e.Active)
                .Select(e => new { e.CompanyId, e.Email })
                .ToListAsync();

            var emailsByCompany = emails
                .GroupBy(e => e.CompanyId)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Email).ToList());

            foreach (var item in items)
                item.Emails = emailsByCompany.GetValueOrDefault(item.CompanyId, new());

            return new PagedResult<CompanyPagedDto>
            {
                Page = filter.Page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = items
            };
        }

        public async Task Approve(int companyId, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var company = await ctx.Company.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.CompanyStateId == 1);
            if (company is null) return;
            company.CompanyStateId = 2;
            company.UpdatedDateTime = DateTimeOffset.UtcNow;
            company.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task Reject(int companyId, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var company = await ctx.Company.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.CompanyStateId == 1);
            if (company is null) return;
            company.CompanyStateId = 3;
            company.UpdatedDateTime = DateTimeOffset.UtcNow;
            company.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }
    }
}
