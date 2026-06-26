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
                    ContributorRuc = i.Contributor!.ContributorRuc ?? "",
                    ContributorName = i.ProveedorName ?? i.Contributor!.ContributorName,
                    AbrilContributorId = i.AbrilContributorId,
                    AbrilContributorName = i.AbrilName ?? i.AbrilContributor!.ContributorName,
                    AbrilContributorRuc = i.AbrilContributor!.ContributorRuc,
                    Description = i.Description,
                    InvoicePaymentFormId = i.InvoicePaymentFormId ?? 0,
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

        /// <summary>Aplica los mismos filtros usados en la tabla y el dashboard.</summary>
        private static IQueryable<Invoice> ApplyFilters(IQueryable<Invoice> query, InvoiceFilterDto filter)
        {
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
                    (i.ProveedorName != null && i.ProveedorName.ToLower().Contains(s)) ||
                    i.Contributor!.ContributorName.ToLower().Contains(s) ||
                    i.Contributor.ContributorRuc.Contains(s));
            }

            return query;
        }

        public async Task<InvoiceDashboardDto> GetDashboard(InvoiceFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();
            var query = ApplyFilters(ctx.Invoice.Where(i => i.State), filter);

            var totalCount = await query.CountAsync();

            var byCurrency = await query
                .GroupBy(i => new { i.CurrencyId, Code = i.Currency!.CurrencyCode, Symbol = i.Currency.CurrencySymbol })
                .Select(g => new InvoiceCurrencyTotalDto
                {
                    CurrencyCode = g.Key.Code ?? "—",
                    CurrencySymbol = g.Key.Symbol,
                    Total = g.Sum(x => x.Total),
                    Count = g.Count()
                })
                .ToListAsync();

            var byMonth = await query
                .GroupBy(i => new { i.IssueDate.Year, i.IssueDate.Month })
                .Select(g => new InvoiceChartItemDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Label = g.Key.Year + "-" + g.Key.Month,
                    Total = g.Sum(x => x.Total),
                    Count = g.Count()
                })
                .ToListAsync();

            var byPaymentForm = await query
                .GroupBy(i => i.InvoicePaymentForm!.InvoicePaymentFormDescription)
                .Select(g => new InvoiceChartItemDto { Label = g.Key, Total = g.Sum(x => x.Total), Count = g.Count() })
                .ToListAsync();

            var byAbril = await query
                .GroupBy(i => i.AbrilName ?? i.AbrilContributor!.ContributorName)
                .Select(g => new InvoiceChartItemDto { Label = g.Key ?? "—", Total = g.Sum(x => x.Total), Count = g.Count() })
                .ToListAsync();

            var topSuppliers = await query
                .GroupBy(i => i.ProveedorName ?? i.Contributor!.ContributorName)
                .Select(g => new InvoiceChartItemDto { Label = g.Key ?? "—", Total = g.Sum(x => x.Total), Count = g.Count() })
                .OrderByDescending(x => x.Total)
                .Take(10)
                .ToListAsync();

            return new InvoiceDashboardDto
            {
                TotalCount = totalCount,
                TotalsByCurrency = byCurrency.OrderByDescending(c => c.Total).ToList(),
                ByMonth = byMonth.OrderBy(m => m.Year).ThenBy(m => m.Month).ToList(),
                ByPaymentForm = byPaymentForm.OrderByDescending(p => p.Total).ToList(),
                ByAbril = byAbril.OrderByDescending(a => a.Total).ToList(),
                TopSuppliers = topSuppliers
            };
        }

        public async Task<PagedResult<InvoiceDto>> GetPaged(InvoiceFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ApplyFilters(ctx.Invoice.Where(i => i.State), filter);

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
                    ContributorRuc = i.Contributor!.ContributorRuc ?? "",
                    ContributorName = i.ProveedorName ?? i.Contributor!.ContributorName,
                    AbrilContributorId = i.AbrilContributorId,
                    AbrilContributorName = i.AbrilName ?? i.AbrilContributor!.ContributorName,
                    Description = i.Description,
                    InvoicePaymentFormId = i.InvoicePaymentFormId ?? 0,
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

        public async Task<InvoiceImportResultDto> ImportInvoices(
            List<InvoiceImportRowDto> rows, Dictionary<int, string?> docUrlByIndex, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;

            // Catálogos en memoria (pocos roundtrips).
            var currencyMap = await ctx.Currency
                .Where(c => c.State && c.Active)
                .ToDictionaryAsync(c => c.CurrencyCode.ToUpper(), c => c.CurrencyId);

            var abrilList = await ctx.Contributor
                .Where(c => c.State && c.EsAbril)
                .Select(c => new { c.ContributorId, c.ContributorName })
                .ToListAsync();
            var abrilNorm = abrilList
                .Select(a => new { a.ContributorId, Norm = RemoveDiacritics(a.ContributorName).ToUpper() })
                .ToList();

            var contributorMap = new Dictionary<string, int>();
            foreach (var c in await ctx.Contributor.Where(c => c.State).Select(c => new { c.ContributorId, c.ContributorName }).ToListAsync())
            {
                var key = RemoveDiacritics(c.ContributorName).Trim().ToUpper();
                if (!contributorMap.ContainsKey(key)) contributorMap[key] = c.ContributorId;
            }

            // Tipos de documento: crear los que falten.
            var docTypeMap = await ctx.InvoiceDocumentType
                .Where(t => t.State)
                .ToDictionaryAsync(t => t.Description.ToUpper(), t => t.InvoiceDocumentTypeId);
            var newTypes = rows
                .Select(r => (r.DocumentType ?? "").Trim())
                .Where(d => d.Length > 0 && !docTypeMap.ContainsKey(d.ToUpper()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var desc in newTypes)
            {
                var t = new InvoiceDocumentType { Description = desc, Active = true, State = true, CreatedDateTime = now, CreatedUserId = userId };
                ctx.InvoiceDocumentType.Add(t);
            }
            if (newTypes.Count > 0)
            {
                await ctx.SaveChangesAsync();
                docTypeMap = await ctx.InvoiceDocumentType.Where(t => t.State).ToDictionaryAsync(t => t.Description.ToUpper(), t => t.InvoiceDocumentTypeId);
            }

            int withFile = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var serie = (r.Serie ?? "").Trim();
                var correlativo = (r.Correlativo ?? "").Trim();
                var number = serie.Length > 0 ? $"{serie}-{correlativo}" : correlativo;

                int? currencyId = null;
                if (!string.IsNullOrWhiteSpace(r.CurrencyCode) && currencyMap.TryGetValue(r.CurrencyCode.Trim().ToUpper(), out var cid))
                    currencyId = cid;

                int? docTypeId = null;
                if (!string.IsNullOrWhiteSpace(r.DocumentType) && docTypeMap.TryGetValue(r.DocumentType.Trim().ToUpper(), out var dtid))
                    docTypeId = dtid;

                int? contributorId = null;
                if (!string.IsNullOrWhiteSpace(r.ProveedorName) &&
                    contributorMap.TryGetValue(RemoveDiacritics(r.ProveedorName).Trim().ToUpper(), out var coid))
                    contributorId = coid;

                int? abrilContributorId = null;
                if (!string.IsNullOrWhiteSpace(r.AbrilName))
                {
                    var token = RemoveDiacritics(r.AbrilName).Trim().ToUpper();
                    var match = abrilNorm.FirstOrDefault(a => a.Norm.Contains(token));
                    if (match != null) abrilContributorId = match.ContributorId;
                }

                DateOnly issueDate = default;
                if (!string.IsNullOrWhiteSpace(r.IssueDate) && DateOnly.TryParse(r.IssueDate, out var parsed))
                    issueDate = parsed;

                docUrlByIndex.TryGetValue(i, out var documentUrl);
                if (!string.IsNullOrWhiteSpace(documentUrl)) withFile++;

                ctx.Invoice.Add(new Invoice
                {
                    IssueDate = issueDate,
                    Serie = serie,
                    Correlativo = correlativo,
                    InvoiceNumber = number,
                    ProveedorName = r.ProveedorName?.Trim(),
                    AbrilName = r.AbrilName?.Trim(),
                    PaymentOrderNumber = r.PaymentOrderNumber?.Trim(),
                    InvoiceDocumentTypeId = docTypeId,
                    AuthorizedAmount = r.AuthorizedAmount,
                    Observation = r.Observation?.Trim(),
                    ContributorId = contributorId,
                    AbrilContributorId = abrilContributorId,
                    Description = (r.Description ?? "").Trim(),
                    InvoicePaymentFormId = 0,
                    Total = r.Total,
                    CurrencyId = currencyId,
                    DocumentUrl = documentUrl,
                    CreatedDateTime = now,
                    CreatedUserId = userId,
                    Active = true,
                    State = true
                });
            }

            await ctx.SaveChangesAsync();

            return new InvoiceImportResultDto
            {
                Inserted = rows.Count,
                WithFile = withFile,
                WithoutFile = rows.Count - withFile
            };
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in normalized)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
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
