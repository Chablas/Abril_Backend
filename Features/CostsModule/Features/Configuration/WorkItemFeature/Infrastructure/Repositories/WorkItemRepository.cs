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
                    WorkSpecialtyId = x.WorkSpecialtyId,
                    WorkSpecialtyDescription = _context.WorkSpecialty
                        .Where(s => s.WorkSpecialtyId == x.WorkSpecialtyId)
                        .Select(s => s.WorkSpecialtyDescription)
                        .FirstOrDefault(),
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
                WorkSpecialtyId = dto.WorkSpecialtyId,
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
            record.WorkSpecialtyId = dto.WorkSpecialtyId;
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

        public async Task<List<WorkSpecialtyOptionDto>> GetActiveSpecialties()
        {
            return await _context.WorkSpecialty
                .Where(x => x.State && x.Active)
                .OrderBy(x => x.WorkSpecialtyDescription)
                .Select(x => new WorkSpecialtyOptionDto
                {
                    WorkSpecialtyId = x.WorkSpecialtyId,
                    WorkSpecialtyDescription = x.WorkSpecialtyDescription
                })
                .ToListAsync();
        }

        public async Task<List<AdjudicacionFolderRootDto>> GetActiveAdjudicacionFolderRoots()
        {
            return await (
                from f in _context.ProjectAdjudicacionFolder
                join p in _context.Project on f.ProjectId equals p.ProjectId
                where f.State && f.Active
                select new AdjudicacionFolderRootDto
                {
                    ProjectId = f.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                    DriveId = f.DriveId,
                    FolderId = f.FolderId
                })
                .ToListAsync();
        }

        public async Task<List<ExistingWorkItemDto>> GetActivePartidas()
        {
            // Solo activas: el índice único parcial (WHERE state) permite N soft-deleted con el mismo nombre.
            return await _context.WorkItem
                .Where(x => x.State)
                .Select(x => new ExistingWorkItemDto
                {
                    WorkItemId = x.WorkItemId,
                    WorkItemDescription = x.WorkItemDescription,
                    WorkSpecialtyId = x.WorkSpecialtyId
                })
                .ToListAsync();
        }

        public async Task<int> AssignSpecialties(IEnumerable<(int WorkItemId, int WorkSpecialtyId)> assignments, int userId)
        {
            var byId = assignments
                .GroupBy(a => a.WorkItemId)
                .ToDictionary(g => g.Key, g => g.First().WorkSpecialtyId);

            if (byId.Count == 0) return 0;

            var ids = byId.Keys.ToList();
            var rows = await _context.WorkItem
                .Where(x => ids.Contains(x.WorkItemId) && x.State)
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            var updated = 0;
            foreach (var row in rows)
            {
                if (byId.TryGetValue(row.WorkItemId, out var sid) && row.WorkSpecialtyId != sid)
                {
                    row.WorkSpecialtyId = sid;
                    row.UpdatedDateTime = now;
                    row.UpdatedUserId = userId;
                    updated++;
                }
            }

            if (updated > 0) await _context.SaveChangesAsync();
            return updated;
        }

        public async Task<List<string>> BulkCreate(IEnumerable<(string Description, int? WorkSpecialtyId)> items, int userId)
        {
            var now = DateTimeOffset.UtcNow;
            var created = new List<string>();

            // Inserción defensiva: cada partida en su propio SaveChanges; si choca con el índice
            // único (duplicado que se haya colado), se descarta esa fila y se continúa con el resto.
            foreach (var (description, workSpecialtyId) in items)
            {
                var record = new WorkItem
                {
                    WorkItemDescription = description.Trim(),
                    WorkSpecialtyId = workSpecialtyId,
                    Active = true,
                    State = true,
                    CreatedDateTime = now,
                    CreatedUserId = userId
                };

                _context.WorkItem.Add(record);
                try
                {
                    await _context.SaveChangesAsync();
                    created.Add(record.WorkItemDescription);
                }
                catch (DbUpdateException)
                {
                    // Quitar la entidad fallida del tracker para no arrastrar el error a la siguiente.
                    _context.Entry(record).State = EntityState.Detached;
                }
            }

            return created;
        }
    }
}
