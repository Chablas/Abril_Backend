using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Models;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Shared.Models;

namespace Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Repositories
{
    public class InvoiceRepository : IInvoiceRepository
    {
        private const int PageSize = 10;
        private readonly IDbContextFactory<AppDbContext> _factory;

        public InvoiceRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<InvoiceSupplierDto>> GetSuppliers()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Contributor
                .Where(c => c.State && c.Active)
                .OrderBy(c => c.ContributorName)
                .Select(c => new InvoiceSupplierDto
                {
                    ContributorId = c.ContributorId,
                    ContributorRuc = c.ContributorRuc,
                    ContributorName = c.ContributorName
                })
                .ToListAsync();
        }

        public async Task<List<InvoiceSupplierDto>> GetAbrilCompanies()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Contributor
                .Where(c => c.State && c.Active && c.EsAbril)
                .OrderBy(c => c.ContributorName)
                .Select(c => new InvoiceSupplierDto
                {
                    ContributorId = c.ContributorId,
                    ContributorRuc = c.ContributorRuc,
                    ContributorName = c.ContributorName
                })
                .ToListAsync();
        }

        public async Task<List<InvoiceFolderOptionDto>> GetFolderOptions()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.InvoiceFolder
                .Where(f => f.State && f.Active)
                .OrderBy(f => f.Name)
                .Select(f => new InvoiceFolderOptionDto
                {
                    InvoiceFolderId = f.InvoiceFolderId,
                    Name = f.Name
                })
                .ToListAsync();
        }

        public async Task<List<InvoiceCurrencyDto>> GetCurrencies()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Currency
                .Where(c => c.State && c.Active)
                .OrderBy(c => c.CurrencyId)
                .Select(c => new InvoiceCurrencyDto
                {
                    CurrencyId = c.CurrencyId,
                    CurrencyCode = c.CurrencyCode,
                    CurrencyDescription = c.CurrencyDescription,
                    CurrencySymbol = c.CurrencySymbol
                })
                .ToListAsync();
        }

        public async Task<InvoiceDetailDto?> GetDetail(int invoiceId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Invoice
                .Where(i => i.InvoiceId == invoiceId && i.State)
                .Select(i => new InvoiceDetailDto
                {
                    InvoiceId = i.InvoiceId,
                    IssueDate = i.IssueDate,
                    Serie = i.Serie,
                    Correlativo = i.Correlativo,
                    ContributorId = i.ContributorId,
                    ContributorRuc = i.Contributor!.ContributorRuc,
                    ContributorName = i.Contributor.ContributorName,
                    AbrilContributorId = i.AbrilContributorId,
                    AbrilContributorName = i.AbrilContributor!.ContributorName,
                    AbrilContributorRuc = i.AbrilContributor.ContributorRuc,
                    Description = i.Description,
                    InvoicePaymentFormId = i.InvoicePaymentFormId,
                    InvoicePaymentFormDescription = i.InvoicePaymentForm!.InvoicePaymentFormDescription,
                    Total = i.Total,
                    CurrencyId = i.CurrencyId,
                    CurrencyCode = i.Currency!.CurrencyCode,
                    CurrencySymbol = i.Currency.CurrencySymbol,
                    InvoiceFolderId = i.InvoiceFolderId,
                    InvoiceFolderName = i.InvoiceFolder!.Name,
                    DocumentUrl = i.DocumentUrl,
                    CreatedDateTime = i.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime,
                    UpdatedDateTime = i.UpdatedDateTime.HasValue
                        ? i.UpdatedDateTime.Value.ToOffset(TimeSpan.FromHours(-5)).DateTime
                        : (DateTime?)null
                })
                .FirstOrDefaultAsync();
        }

        public async Task Update(InvoiceUpdateDto dto, string? documentUrl, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var record = await ctx.Invoice.FirstOrDefaultAsync(i => i.InvoiceId == dto.InvoiceId && i.State)
                ?? throw new AbrilException("La factura no existe.");

            var serie = dto.Serie.Trim();
            var correlativo = dto.Correlativo.Trim();

            var duplicate = await ctx.Invoice.AnyAsync(i =>
                i.State &&
                i.InvoiceId != dto.InvoiceId &&
                i.ContributorId == dto.ContributorId &&
                i.Serie.ToLower() == serie.ToLower() &&
                i.Correlativo.ToLower() == correlativo.ToLower());
            if (duplicate)
                throw new AbrilException("Ya existe otra factura con esa serie y correlativo para el proveedor seleccionado.");

            record.IssueDate = dto.IssueDate;
            record.Serie = serie;
            record.Correlativo = correlativo;
            record.InvoiceNumber = $"{serie}-{correlativo}";
            record.ContributorId = dto.ContributorId;
            record.AbrilContributorId = dto.AbrilContributorId;
            record.InvoiceFolderId = dto.InvoiceFolderId;
            record.Description = dto.Description.Trim();
            record.InvoicePaymentFormId = dto.InvoicePaymentFormId;
            record.Total = dto.Total;
            record.CurrencyId = dto.CurrencyId;
            if (documentUrl != null) record.DocumentUrl = documentUrl;
            record.UpdatedDateTime = DateTimeOffset.UtcNow;
            record.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
        }

        public async Task<(string DriveId, string FolderId)?> GetFolderDestination(int invoiceFolderId)
        {
            using var ctx = _factory.CreateDbContext();
            var f = await ctx.InvoiceFolder
                .Where(x => x.InvoiceFolderId == invoiceFolderId && x.State && x.Active)
                .Select(x => new { x.DriveId, x.FolderId })
                .FirstOrDefaultAsync();
            return f == null ? null : (f.DriveId, f.FolderId);
        }

        public async Task<string?> GetContributorName(int contributorId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Contributor
                .Where(c => c.ContributorId == contributorId && c.State)
                .Select(c => c.ContributorName)
                .FirstOrDefaultAsync();
        }

        public async Task<string?> GetAbrilContributorName(int abrilContributorId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Contributor
                .Where(c => c.ContributorId == abrilContributorId && c.State && c.EsAbril)
                .Select(c => c.ContributorName)
                .FirstOrDefaultAsync();
        }

        public async Task<List<InvoicePaymentFormDto>> GetPaymentForms()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.InvoicePaymentForm
                .Where(p => p.State && p.Active)
                .OrderBy(p => p.InvoicePaymentFormId)
                .Select(p => new InvoicePaymentFormDto
                {
                    InvoicePaymentFormId = p.InvoicePaymentFormId,
                    InvoicePaymentFormDescription = p.InvoicePaymentFormDescription
                })
                .ToListAsync();
        }

        public async Task<PagedResult<InvoiceDto>> GetPaged(InvoiceFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.Invoice.Where(i => i.State);

            if (filter.ContributorId.HasValue)
                query = query.Where(i => i.ContributorId == filter.ContributorId.Value);

            if (!string.IsNullOrWhiteSpace(filter.ContributorRuc))
            {
                var ruc = filter.ContributorRuc.Trim();
                query = query.Where(i => i.Contributor!.ContributorRuc.Contains(ruc));
            }

            if (filter.AbrilContributorId.HasValue)
                query = query.Where(i => i.AbrilContributorId == filter.AbrilContributorId.Value);

            if (!string.IsNullOrWhiteSpace(filter.AbrilContributorRuc))
            {
                var ruc = filter.AbrilContributorRuc.Trim();
                query = query.Where(i => i.AbrilContributor!.ContributorRuc.Contains(ruc));
            }

            if (filter.InvoicePaymentFormId.HasValue)
                query = query.Where(i => i.InvoicePaymentFormId == filter.InvoicePaymentFormId.Value);

            if (filter.TotalMin.HasValue)
                query = query.Where(i => i.Total >= filter.TotalMin.Value);

            if (filter.TotalMax.HasValue)
                query = query.Where(i => i.Total <= filter.TotalMax.Value);

            if (filter.IssueDateFrom.HasValue)
                query = query.Where(i => i.IssueDate >= filter.IssueDateFrom.Value);

            if (filter.IssueDateTo.HasValue)
                query = query.Where(i => i.IssueDate <= filter.IssueDateTo.Value);

            if (!string.IsNullOrWhiteSpace(filter.Serie))
            {
                var serie = filter.Serie.Trim().ToLower();
                query = query.Where(i => i.Serie.ToLower().Contains(serie));
            }

            if (!string.IsNullOrWhiteSpace(filter.Correlativo))
            {
                var corr = filter.Correlativo.Trim().ToLower();
                query = query.Where(i => i.Correlativo.ToLower().Contains(corr));
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim().ToLower();
                query = query.Where(i =>
                    i.Serie.ToLower().Contains(s) ||
                    i.Correlativo.ToLower().Contains(s) ||
                    i.Contributor!.ContributorName.ToLower().Contains(s) ||
                    i.Contributor.ContributorRuc.Contains(s));
            }

            var totalRecords = await query.CountAsync();

            var data = await query
                .OrderByDescending(i => i.InvoiceId)
                .Skip((filter.Page - 1) * PageSize)
                .Take(PageSize)
                .Select(i => new InvoiceDto
                {
                    InvoiceId = i.InvoiceId,
                    IssueDate = i.IssueDate,
                    Serie = i.Serie,
                    Correlativo = i.Correlativo,
                    ContributorId = i.ContributorId,
                    ContributorRuc = i.Contributor!.ContributorRuc,
                    ContributorName = i.Contributor.ContributorName,
                    AbrilContributorId = i.AbrilContributorId,
                    AbrilContributorName = i.AbrilContributor!.ContributorName,
                    Description = i.Description,
                    InvoicePaymentFormId = i.InvoicePaymentFormId,
                    InvoicePaymentFormDescription = i.InvoicePaymentForm!.InvoicePaymentFormDescription,
                    Total = i.Total,
                    CurrencyId = i.CurrencyId,
                    CurrencyCode = i.Currency!.CurrencyCode,
                    CurrencySymbol = i.Currency.CurrencySymbol,
                    DocumentUrl = i.DocumentUrl,
                    CreatedDateTime = i.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime
                })
                .ToListAsync();

            return new PagedResult<InvoiceDto>
            {
                Page = filter.Page,
                PageSize = PageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)PageSize),
                Data = data
            };
        }

        public async Task Create(InvoiceCreateDto dto, string? documentUrl, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var serie = dto.Serie.Trim();
            var correlativo = dto.Correlativo.Trim();

            var exists = await ctx.Invoice.AnyAsync(i =>
                i.State &&
                i.ContributorId == dto.ContributorId &&
                i.Serie.ToLower() == serie.ToLower() &&
                i.Correlativo.ToLower() == correlativo.ToLower());
            if (exists)
                throw new AbrilException("Ya existe una factura con esa serie y correlativo para el proveedor seleccionado.");

            ctx.Invoice.Add(new Invoice
            {
                IssueDate = dto.IssueDate,
                Serie = serie,
                Correlativo = correlativo,
                InvoiceNumber = $"{serie}-{correlativo}",
                ContributorId = dto.ContributorId,
                Description = dto.Description.Trim(),
                InvoicePaymentFormId = dto.InvoicePaymentFormId,
                Total = dto.Total,
                CurrencyId = dto.CurrencyId,
                DocumentUrl = documentUrl,
                InvoiceFolderId = dto.InvoiceFolderId,
                AbrilContributorId = dto.AbrilContributorId,
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            });
            await ctx.SaveChangesAsync();
        }

        public async Task<InvoiceSupplierDto> CreateSupplier(InvoiceSupplierCreateDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var ruc = dto.ContributorRuc.Trim();

            var existing = await ctx.Contributor.FirstOrDefaultAsync(c => c.ContributorRuc == ruc && c.State);
            if (existing != null)
                throw new AbrilException("Ya existe un proveedor registrado con ese RUC.");

            var contributor = new Contributor
            {
                ContributorRuc = ruc,
                ContributorName = dto.ContributorName.Trim(),
                ContributorAddress = dto.ContributorAddress?.Trim() ?? string.Empty,
                ContributorEconomicActivityDescription = dto.ContributorEconomicActivityDescription?.Trim(),
                ContributorDistrict = dto.ContributorDistrict?.Trim(),
                ContributorProvince = dto.ContributorProvince?.Trim(),
                ContributorDepartment = dto.ContributorDepartment?.Trim(),
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            };
            ctx.Contributor.Add(contributor);
            await ctx.SaveChangesAsync();

            return new InvoiceSupplierDto
            {
                ContributorId = contributor.ContributorId,
                ContributorRuc = contributor.ContributorRuc,
                ContributorName = contributor.ContributorName
            };
        }
    }
}
