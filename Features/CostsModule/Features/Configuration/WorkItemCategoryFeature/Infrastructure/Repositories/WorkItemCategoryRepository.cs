using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemCategoryFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemCategoryFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemCategoryFeature.Infrastructure.Repositories
{
    public class WorkItemCategoryRepository : IWorkItemCategoryRepository
    {
        private readonly AppDbContext _context;

        public WorkItemCategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<WorkItemCategoryDto>> GetPaged(WorkItemCategoryFilterDto filter)
        {
            const int pageSize = 10;

            var query = _context.WorkItemCategory.Where(x => x.State);

            if (!string.IsNullOrWhiteSpace(filter.Description))
                query = query.Where(x => x.WorkItemCategoryDescription.Contains(filter.Description));

            var totalRecords = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.WorkItemCategoryId)
                .Skip((filter.Page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new WorkItemCategoryDto
                {
                    WorkItemCategoryId = x.WorkItemCategoryId,
                    WorkItemCategoryDescription = x.WorkItemCategoryDescription,
                    CreatedDateTime = x.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime,
                    CreatedUserId = x.CreatedUserId,
                    UpdatedDateTime = x.UpdatedDateTime.HasValue
                        ? x.UpdatedDateTime.Value.ToOffset(TimeSpan.FromHours(-5)).DateTime
                        : null,
                    UpdatedUserId = x.UpdatedUserId,
                    Active = x.Active,
                    InstructivosFolderId = x.InstructivosFolderId,
                    InstructivosFolderName = x.InstructivosFolderName,
                    InstructivosSyncStatus = x.InstructivosSyncStatus,
                    InstructivosSyncedAt = x.InstructivosSyncedAt.HasValue
                        ? x.InstructivosSyncedAt.Value.ToOffset(TimeSpan.FromHours(-5)).DateTime
                        : null
                })
                .ToListAsync();

            return new PagedResult<WorkItemCategoryDto>
            {
                Page = filter.Page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = data
            };
        }

        public async Task Create(WorkItemCategoryCreateDto dto, int userId)
        {
            var exists = await _context.WorkItemCategory
                .AnyAsync(x => x.WorkItemCategoryDescription.ToLower() == dto.WorkItemCategoryDescription.Trim().ToLower() && x.State);

            if (exists)
                throw new AbrilException("Ya existe una categoría con esa descripción.");

            var record = new WorkItemCategory
            {
                WorkItemCategoryDescription = dto.WorkItemCategoryDescription.Trim(),
                Active = true,
                State = true,
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId = userId
            };

            _context.WorkItemCategory.Add(record);
            await _context.SaveChangesAsync();
        }

        public async Task Update(WorkItemCategoryEditDto dto, int userId)
        {
            var record = await _context.WorkItemCategory
                .FirstOrDefaultAsync(x => x.WorkItemCategoryId == dto.WorkItemCategoryId && x.State);

            if (record == null)
                throw new AbrilException("La categoría no existe.");

            var duplicate = await _context.WorkItemCategory
                .AnyAsync(x => x.WorkItemCategoryDescription.ToLower() == dto.WorkItemCategoryDescription.Trim().ToLower()
                             && x.WorkItemCategoryId != dto.WorkItemCategoryId
                             && x.State);

            if (duplicate)
                throw new AbrilException("Ya existe una categoría con esa descripción.");

            record.WorkItemCategoryDescription = dto.WorkItemCategoryDescription.Trim();
            record.Active = dto.Active;
            record.UpdatedDateTime = DateTimeOffset.UtcNow;
            record.UpdatedUserId = userId;

            await _context.SaveChangesAsync();
        }

        public async Task<bool> Delete(int workItemCategoryId, int userId)
        {
            var record = await _context.WorkItemCategory
                .FirstOrDefaultAsync(x => x.WorkItemCategoryId == workItemCategoryId && x.State);

            if (record == null)
                return false;

            record.State = false;
            record.Active = false;
            record.UpdatedDateTime = DateTimeOffset.UtcNow;
            record.UpdatedUserId = userId;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<WorkItemCategory>> GetAllActive()
            => await _context.WorkItemCategory.Where(x => x.State && x.Active).ToListAsync();

        public async Task CreateWithSync(string description, string folderId, string folderName, int userId)
        {
            var record = new WorkItemCategory
            {
                WorkItemCategoryDescription = description,
                Active = true,
                State = true,
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId = userId,
                InstructivosFolderId = folderId,
                InstructivosFolderName = folderName,
                InstructivosSyncStatus = 1,
                InstructivosSyncedAt = DateTimeOffset.UtcNow,
            };
            _context.WorkItemCategory.Add(record);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateInstructivosSync(int workItemCategoryId, string? folderId, string? folderName, int syncStatus)
        {
            var record = await _context.WorkItemCategory
                .FirstOrDefaultAsync(x => x.WorkItemCategoryId == workItemCategoryId);

            if (record is null) return;

            record.InstructivosFolderId = folderId;
            record.InstructivosFolderName = folderName;
            record.InstructivosSyncStatus = syncStatus;
            record.InstructivosSyncedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();
        }
    }
}
