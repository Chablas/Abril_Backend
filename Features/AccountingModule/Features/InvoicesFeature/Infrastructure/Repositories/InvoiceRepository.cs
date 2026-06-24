using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Models;
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

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim().ToLower();
                query = query.Where(i =>
                    i.InvoiceNumber.ToLower().Contains(s) ||
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
                    InvoiceNumber = i.InvoiceNumber,
                    ContributorId = i.ContributorId,
                    ContributorRuc = i.Contributor!.ContributorRuc,
                    ContributorName = i.Contributor.ContributorName,
                    Description = i.Description,
                    InvoicePaymentFormId = i.InvoicePaymentFormId,
                    InvoicePaymentFormDescription = i.InvoicePaymentForm!.InvoicePaymentFormDescription,
                    Total = i.Total,
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

            var number = dto.InvoiceNumber.Trim();

            var exists = await ctx.Invoice.AnyAsync(i =>
                i.State &&
                i.ContributorId == dto.ContributorId &&
                i.InvoiceNumber.ToLower() == number.ToLower());
            if (exists)
                throw new AbrilException("Ya existe una factura con ese número para el proveedor seleccionado.");

            ctx.Invoice.Add(new Invoice
            {
                IssueDate = dto.IssueDate,
                InvoiceNumber = number,
                ContributorId = dto.ContributorId,
                Description = dto.Description.Trim(),
                InvoicePaymentFormId = dto.InvoicePaymentFormId,
                Total = dto.Total,
                DocumentUrl = documentUrl,
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
