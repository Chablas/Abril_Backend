using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Infrastructure.Repositories
{
    public class WorkItemRepository : IWorkItemRepository
    {
        private readonly AppDbContext _context;

        public WorkItemRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<WorkItemDto>> GetPaged(WorkItemFilterDto filter)
        {
            const int pageSize = 10;

            var query = _context.WorkItem.Where(x => x.State);

            if (!string.IsNullOrWhiteSpace(filter.Description))
            {
                var descLower = filter.Description.ToLower();
                query = query.Where(x => x.WorkItemDescription.ToLower().Contains(descLower));
            }

            var totalRecords = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.WorkItemId)
                .Skip((filter.Page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new WorkItemDto
                {
                    WorkItemId = x.WorkItemId,
                    WorkItemDescription = x.WorkItemDescription,
                    CreatedDateTime = x.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime,
                    CreatedUserId = x.CreatedUserId,
                    UpdatedDateTime = x.UpdatedDateTime.HasValue
                        ? x.UpdatedDateTime.Value.ToOffset(TimeSpan.FromHours(-5)).DateTime
                        : null,
                    UpdatedUserId = x.UpdatedUserId,
                    Active = x.Active
                })
                .ToListAsync();

            return new PagedResult<WorkItemDto>
            {
                Page = filter.Page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = data
            };
        }

        public async Task Create(WorkItemCreateDto dto, int userId)
        {
            var exists = await _context.WorkItem
                .AnyAsync(x => x.WorkItemDescription.ToLower() == dto.WorkItemDescription.Trim().ToLower()
                             && x.State);

            if (exists)
                throw new AbrilException("Ya existe una partida con esa descripción.");

            var record = new WorkItem
            {
                WorkItemDescription = dto.WorkItemDescription.Trim(),
                Active = true,
                State = true,
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId = userId
            };

            _context.WorkItem.Add(record);
            await _context.SaveChangesAsync();
        }

        public async Task Update(WorkItemEditDto dto, int userId)
        {
            var record = await _context.WorkItem
                .FirstOrDefaultAsync(x => x.WorkItemId == dto.WorkItemId && x.State);

            if (record == null)
                throw new AbrilException("La partida no existe.");

            var duplicate = await _context.WorkItem
                .AnyAsync(x => x.WorkItemDescription.ToLower() == dto.WorkItemDescription.Trim().ToLower()
                             && x.WorkItemId != dto.WorkItemId
                             && x.State);

            if (duplicate)
                throw new AbrilException("Ya existe una partida con esa descripción.");

            record.WorkItemDescription = dto.WorkItemDescription.Trim();
            record.Active = dto.Active;
            record.UpdatedDateTime = DateTimeOffset.UtcNow;
            record.UpdatedUserId = userId;

            await _context.SaveChangesAsync();
        }

        public async Task<bool> Delete(int workItemId, int userId)
        {
            var record = await _context.WorkItem
                .FirstOrDefaultAsync(x => x.WorkItemId == workItemId && x.State);

            if (record == null)
                return false;

            record.State = false;
            record.Active = false;
            record.UpdatedDateTime = DateTimeOffset.UtcNow;
            record.UpdatedUserId = userId;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
