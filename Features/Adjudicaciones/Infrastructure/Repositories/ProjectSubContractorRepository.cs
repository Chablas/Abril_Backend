using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Features.Adjudicaciones.Infrastructure.Interfaces;
using Abril_Backend.Features.Adjudicaciones.Infrastructure.Models;
using Abril_Backend.Features.Adjudicaciones.Application.Dtos;
using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.Adjudicaciones.Infrastructure.Repositories {
    public class ProjectSubContractorRepository : IProjectSubContractorRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public ProjectSubContractorRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task Create(ProjectSubContractorCreateDTO dto, List<string> quotationFileUrls, List<string> comparativeFileUrls, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var subContractor = new ProjectSubContractor
            {
                ProjectId = dto.ProjectId,
                CompanyId = dto.CompanyId,
                ContractId = dto.ContractId,
                ContractTypeId = dto.ContractTypeId,
                ContractOriginId = dto.ContractOriginId,
                PaymentMethodId = dto.PaymentMethodId,
                AdvancePercentage = dto.AdvancePercentage,
                Amount = dto.Amount,
                CurrencyId  = dto.CurrencyId,
                HasIgv = dto.HasIgv,
                ContractorEmail = dto.ContractorEmail,
                WorkItemId = dto.WorkItemId,
                ProjectSubContractorStatusId = 1,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            };

            foreach (var url in quotationFileUrls)
            {
                subContractor.QuotationFiles.Add(new ProjectSubContractorQuotationFile
                {
                    FileUrl = url,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = userId,
                    Active = true,
                    State = true
                });
            }

            foreach (var url in comparativeFileUrls)
            {
                subContractor.ComparativeFiles.Add(new ProjectSubContractorComparativeFile
                {
                    FileUrl = url,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = userId,
                    Active = true,
                    State = true
                });
            }

            ctx.ProjectSubContractor.Add(subContractor);
            await ctx.SaveChangesAsync();
        }

        public async Task<List<ContractSimpleDTO>> GetContractsFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.Contract
                .Where(item => item.Active)
                .OrderBy(item => item.ContractDescription)
                .Select(item => new ContractSimpleDTO
                {
                    ContractId = item.ContractId,
                    ContractDescription = item.ContractDescription,
                });
            return await registros.ToListAsync();
        }

        public async Task<List<ContractTypeSimpleDTO>> GetContractTypeFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.ContractType
                .Where(item => item.Active)
                .OrderBy(item => item.ContractTypeDescription)
                .Select(item => new ContractTypeSimpleDTO
                {
                    ContractTypeId = item.ContractTypeId,
                    ContractTypeDescription = item.ContractTypeDescription,
                });
            return await registros.ToListAsync();
        }

        public async Task<List<ContractOriginSimpleDTO>> GetContractOriginFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.ContractOrigin
                .Where(item => item.Active)
                .OrderBy(item => item.ContractOriginDescription)
                .Select(item => new ContractOriginSimpleDTO
                {
                    ContractOriginId = item.ContractOriginId,
                    ContractOriginDescription = item.ContractOriginDescription,
                });
            return await registros.ToListAsync();
        }

        public async Task<List<PaymentMethodSimpleDTO>> GetPaymentMethodFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.PaymentMethod
                .Where(item => item.Active)
                .OrderBy(item => item.PaymentMethodDescription)
                .Select(item => new PaymentMethodSimpleDTO
                {
                    PaymentMethodId = item.PaymentMethodId,
                    PaymentMethodDescription = item.PaymentMethodDescription,
                });
            return await registros.ToListAsync();
        }

        public async Task<List<CurrencySimpleDTO>> GetCurrencyFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.Currency
                .Where(item => item.Active)
                .OrderBy(item => item.CurrencyCode)
                .Select(item => new CurrencySimpleDTO
                {
                    CurrencyId = item.CurrencyId,
                    CurrencyDescription = item.CurrencyDescription,
                    CurrencyCode = item.CurrencyCode,
                    CurrencySymbol = item.CurrencySymbol,
                });
            return await registros.ToListAsync();
        }

        public async Task<List<WorkItemSimpleDTO>> GetWorkItemFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.WorkItem
                .Where(item => item.Active)
                .OrderBy(item => item.WorkItemDescription)
                .Select(item => new WorkItemSimpleDTO
                {
                    WorkItemId = item.WorkItemId,
                    WorkItemDescription = item.WorkItemDescription,
                });
            return await registros.ToListAsync();
        }

        public async Task<List<CompanySimpleDTO>> GetCompanyFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.Company
                .Where(item => item.Active)
                .OrderBy(item => item.CompanyName)
                .Select(item => new CompanySimpleDTO
                {
                    CompanyId = item.CompanyId,
                    CompanyName = item.CompanyName,
                    CompanyRuc = item.CompanyRuc,
                });
            return await registros.ToListAsync();
        }

        public async Task<PagedResult<ProjectSubContractorDTO>> GetPaged(ProjectSubContractorFilterDTO filter)
        {
            using var ctx = _factory.CreateDbContext();

            const int pageSize = 10;

            var query =
                from psc in ctx.ProjectSubContractor
                join p in ctx.Project on psc.ProjectId equals p.ProjectId
                join c in ctx.Company on psc.CompanyId equals c.CompanyId
                join ct in ctx.ContractType on psc.ContractTypeId equals ct.ContractTypeId
                join co in ctx.ContractOrigin on psc.ContractOriginId equals co.ContractOriginId
                join pm in ctx.PaymentMethod on psc.PaymentMethodId equals pm.PaymentMethodId
                join cur in ctx.Currency on psc.CurrencyId equals cur.CurrencyId
                join wi in ctx.WorkItem on psc.WorkItemId equals wi.WorkItemId
                join contract in ctx.Contract on psc.ContractId equals contract.ContractId
                join pscs in ctx.ProjectSubContractorStatus on psc.ProjectSubContractorStatusId equals pscs.ProjectSubContractorStatusId
                where psc.State
                select new { psc, p, c, ct, co, pm, cur, wi, contract, pscs };

            if (filter.ProjectId.HasValue)
                query = query.Where(x => x.psc.ProjectId == filter.ProjectId.Value);

            if (!string.IsNullOrWhiteSpace(filter.CompanyName))
                query = query.Where(x => x.c.CompanyName.Contains(filter.CompanyName));

            if (!string.IsNullOrWhiteSpace(filter.CompanyRuc))
                query = query.Where(x => x.c.CompanyRuc.Contains(filter.CompanyRuc));

            if (filter.CreatedUserId.HasValue)
                query = query.Where(x => x.psc.CreatedUserId == filter.CreatedUserId.Value);

            query = query.OrderByDescending(x => x.psc.ProjectSubContractorId);

            var totalRecords = await query.CountAsync();

            var items = await query
                .Skip((filter.Page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ProjectSubContractorDTO
                {
                    ProjectSubContractorId = x.psc.ProjectSubContractorId,
                    ProjectId = x.psc.ProjectId,
                    ProjectDescription = x.p.ProjectDescription,
                    CompanyId = x.psc.CompanyId,
                    CompanyName = x.c.CompanyName,
                    ContractId = x.psc.ContractId,
                    ContractDescription = x.contract.ContractDescription,
                    ContractTypeId = x.psc.ContractTypeId,
                    ContractTypeDescription = x.ct.ContractTypeDescription,
                    ContractOriginId = x.psc.ContractOriginId,
                    ContractOriginDescription = x.co.ContractOriginDescription,
                    PaymentMethodId = x.psc.PaymentMethodId,
                    PaymentMethodDescription = x.pm.PaymentMethodDescription,
                    AdvancePercentage = x.psc.AdvancePercentage,
                    Amount = x.psc.Amount,
                    CurrencyId = x.psc.CurrencyId,
                    CurrencyCode = x.cur.CurrencyCode,
                    AmountHasIgv = x.psc.HasIgv,
                    ContractorEmail = x.psc.ContractorEmail,
                    WorkItemId = x.psc.WorkItemId,
                    WorkItemDescription = x.wi.WorkItemDescription,
                    ProjectSubContractorStatusId = x.pscs.ProjectSubContractorStatusId,
                    ProjectSubContractorStatusDescription = x.pscs.ProjectSubContractorStatusDescription,
                    CreatedDateTime = x.psc.CreatedDateTime
                })
                .ToListAsync();

            var ids = items.Select(x => x.ProjectSubContractorId).ToList();

            var quotationFiles = await ctx.ProjectSubContractorQuotationFile
                .Where(f => ids.Contains(f.ProjectSubContractorId) && f.State)
                .Select(f => new { f.ProjectSubContractorId, f.FileUrl })
                .ToListAsync();

            var comparativeFiles = await ctx.ProjectSubContractorComparativeFile
                .Where(f => ids.Contains(f.ProjectSubContractorId) && f.State)
                .Select(f => new { f.ProjectSubContractorId, f.FileUrl })
                .ToListAsync();

            var quotationByPsc = quotationFiles
                .GroupBy(f => f.ProjectSubContractorId)
                .ToDictionary(g => g.Key, g => g.Select(f => f.FileUrl).ToList());

            var comparativeByPsc = comparativeFiles
                .GroupBy(f => f.ProjectSubContractorId)
                .ToDictionary(g => g.Key, g => g.Select(f => f.FileUrl).ToList());

            foreach (var item in items)
            {
                item.QuotationFileUrls = quotationByPsc.GetValueOrDefault(item.ProjectSubContractorId, new());
                item.ComparativeFileUrls = comparativeByPsc.GetValueOrDefault(item.ProjectSubContractorId, new());
            }

            return new PagedResult<ProjectSubContractorDTO>
            {
                Page = filter.Page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = items
            };
        }
    }
}