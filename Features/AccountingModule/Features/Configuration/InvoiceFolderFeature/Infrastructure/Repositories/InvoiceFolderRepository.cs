using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Infrastructure.Repositories
{
    public class InvoiceFolderRepository : IInvoiceFolderRepository
    {
        private const int PageSize = 10;
        private readonly IDbContextFactory<AppDbContext> _factory;

        public InvoiceFolderRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<PagedResult<InvoiceFolderDto>> GetPaged(InvoiceFolderFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.InvoiceFolder.Where(x => x.State);

            var totalRecords = await query.CountAsync();

            var data = await query
                .OrderByDescending(f => f.InvoiceFolderId)
                .Skip((filter.Page - 1) * PageSize)
                .Take(PageSize)
                .Select(f => new InvoiceFolderDto
                {
                    InvoiceFolderId = f.InvoiceFolderId,
                    Name = f.Name,
                    LinkUrl = f.LinkUrl,
                    DriveId = f.DriveId,
                    FolderId = f.FolderId,
                    FolderName = f.FolderName,
                    WebUrl = f.WebUrl,
                    Active = f.Active,
                    CreatedDateTime = f.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime,
                    CreatedUserId = f.CreatedUserId
                })
                .ToListAsync();

            return new PagedResult<InvoiceFolderDto>
            {
                Page = filter.Page,
                PageSize = PageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)PageSize),
                Data = data
            };
        }

        public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
        {
            using var ctx = _factory.CreateDbContext();
            var normalized = name.Trim().ToLower();
            return await ctx.InvoiceFolder.AnyAsync(x =>
                x.State &&
                x.Name.ToLower() == normalized &&
                (excludeId == null || x.InvoiceFolderId != excludeId.Value));
        }

        public async Task<bool> ExistsAsync(int invoiceFolderId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.InvoiceFolder.AnyAsync(x => x.InvoiceFolderId == invoiceFolderId && x.State);
        }

        public async Task Create(InvoiceFolderCreateDto dto, string driveId, string folderId, string? folderName, string? webUrl, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            ctx.InvoiceFolder.Add(new InvoiceFolder
            {
                Name = dto.Name.Trim(),
                LinkUrl = dto.LinkUrl.Trim(),
                DriveId = driveId,
                FolderId = folderId,
                FolderName = folderName,
                WebUrl = webUrl,
                Active = true,
                State = true,
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId = userId
            });
            await ctx.SaveChangesAsync();
        }

        public async Task Update(InvoiceFolderUpdateDto dto, string driveId, string folderId, string? folderName, string? webUrl, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var record = await ctx.InvoiceFolder
                .FirstOrDefaultAsync(x => x.InvoiceFolderId == dto.InvoiceFolderId && x.State)
                ?? throw new AbrilException("La carpeta no existe.");

            record.Name = dto.Name.Trim();
            record.LinkUrl = dto.LinkUrl.Trim();
            record.DriveId = driveId;
            record.FolderId = folderId;
            record.FolderName = folderName;
            record.WebUrl = webUrl;
            record.Active = dto.Active;
            record.UpdatedDateTime = DateTimeOffset.UtcNow;
            record.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
        }

        public async Task<bool> Delete(int invoiceFolderId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var record = await ctx.InvoiceFolder
                .FirstOrDefaultAsync(x => x.InvoiceFolderId == invoiceFolderId && x.State);

            if (record == null) return false;

            record.State = false;
            record.Active = false;
            record.UpdatedDateTime = DateTimeOffset.UtcNow;
            record.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
            return true;
        }
    }
}
