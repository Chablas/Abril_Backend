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
                query = query.Where(x => EF.Functions.ILike(x.WorkItemCategoryDescription, $"%{filter.Description}%"));

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
                        : null,
                    Clauses = _context.WorkItemCategoryClause
                        .Where(c => c.WorkItemCategoryId == x.WorkItemCategoryId && c.State)
                        .OrderBy(c => c.SortOrder)
                        .Select(c => new WorkItemCategoryClauseDto
                        {
                            WorkItemCategoryClauseId = c.WorkItemCategoryClauseId,
                            ClauseText = c.ClauseText,
                            SortOrder  = c.SortOrder,
                        })
                        .ToList(),
                    Anexo3Clauses = _context.WorkItemCategoryAnexo3Clause
                        .Where(c => c.WorkItemCategoryId == x.WorkItemCategoryId && c.State)
                        .OrderBy(c => c.SortOrder)
                        .Select(c => new WorkItemCategoryAnexo3ClauseDto
                        {
                            WorkItemCategoryAnexo3ClauseId = c.WorkItemCategoryAnexo3ClauseId,
                            ClauseText = c.ClauseText,
                            SortOrder  = c.SortOrder,
                        })
                        .ToList(),
                    Anexo4Clauses = _context.WorkItemCategoryAnexo4Clause
                        .Where(c => c.WorkItemCategoryId == x.WorkItemCategoryId && c.State)
                        .OrderBy(c => c.SortOrder)
                        .Select(c => new WorkItemCategoryAnexo4ClauseDto
                        {
                            WorkItemCategoryAnexo4ClauseId = c.WorkItemCategoryAnexo4ClauseId,
                            ClauseText = c.ClauseText,
                            SortOrder  = c.SortOrder,
                        })
                        .ToList()
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

            // ── Cláusulas: upsert completo ──────────────────────────────────
            var now = DateTimeOffset.UtcNow;
            var incomingIds = dto.Clauses
                .Where(c => c.WorkItemCategoryClauseId.HasValue)
                .Select(c => c.WorkItemCategoryClauseId!.Value)
                .ToHashSet();

            // Soft-delete de las que ya no están en la lista
            var toDelete = await _context.WorkItemCategoryClause
                .Where(c => c.WorkItemCategoryId == dto.WorkItemCategoryId
                         && c.State
                         && !incomingIds.Contains(c.WorkItemCategoryClauseId))
                .ToListAsync();
            foreach (var c in toDelete)
            {
                c.State          = false;
                c.UpdatedDatetime = now;
                c.UpdatedUserId   = userId;
            }

            // Actualizar existentes e insertar nuevas
            foreach (var clauseDto in dto.Clauses)
            {
                if (clauseDto.WorkItemCategoryClauseId.HasValue)
                {
                    var existing = await _context.WorkItemCategoryClause
                        .FirstOrDefaultAsync(c => c.WorkItemCategoryClauseId == clauseDto.WorkItemCategoryClauseId.Value);
                    if (existing is not null)
                    {
                        existing.ClauseText       = clauseDto.ClauseText.Trim();
                        existing.SortOrder        = clauseDto.SortOrder;
                        existing.UpdatedDatetime  = now;
                        existing.UpdatedUserId    = userId;
                    }
                }
                else
                {
                    _context.WorkItemCategoryClause.Add(new WorkItemCategoryClause
                    {
                        WorkItemCategoryId = dto.WorkItemCategoryId,
                        ClauseText         = clauseDto.ClauseText.Trim(),
                        SortOrder          = clauseDto.SortOrder,
                        State              = true,
                        CreatedDatetime    = now,
                        CreatedUserId      = userId,
                    });
                }
            }

            // ── Cláusulas Anexo 3 (Suministro): mismo patrón de upsert + soft-delete ──
            var incomingAnexo3Ids = dto.Anexo3Clauses
                .Where(c => c.WorkItemCategoryAnexo3ClauseId.HasValue)
                .Select(c => c.WorkItemCategoryAnexo3ClauseId!.Value)
                .ToHashSet();

            var anexo3ToDelete = await _context.WorkItemCategoryAnexo3Clause
                .Where(c => c.WorkItemCategoryId == dto.WorkItemCategoryId
                         && c.State
                         && !incomingAnexo3Ids.Contains(c.WorkItemCategoryAnexo3ClauseId))
                .ToListAsync();
            foreach (var c in anexo3ToDelete)
            {
                c.State           = false;
                c.UpdatedDatetime = now;
                c.UpdatedUserId   = userId;
            }

            foreach (var clauseDto in dto.Anexo3Clauses)
            {
                if (clauseDto.WorkItemCategoryAnexo3ClauseId.HasValue)
                {
                    var existing = await _context.WorkItemCategoryAnexo3Clause
                        .FirstOrDefaultAsync(c => c.WorkItemCategoryAnexo3ClauseId == clauseDto.WorkItemCategoryAnexo3ClauseId.Value);
                    if (existing is not null)
                    {
                        existing.ClauseText      = clauseDto.ClauseText.Trim();
                        existing.SortOrder       = clauseDto.SortOrder;
                        existing.UpdatedDatetime = now;
                        existing.UpdatedUserId   = userId;
                    }
                }
                else
                {
                    _context.WorkItemCategoryAnexo3Clause.Add(new WorkItemCategoryAnexo3Clause
                    {
                        WorkItemCategoryId = dto.WorkItemCategoryId,
                        ClauseText         = clauseDto.ClauseText.Trim(),
                        SortOrder          = clauseDto.SortOrder,
                        State              = true,
                        CreatedDatetime    = now,
                        CreatedUserId      = userId,
                    });
                }
            }

            // ── Cláusulas Anexo 4 (Suministro): mismo patrón de upsert + soft-delete ──
            var incomingAnexo4Ids = dto.Anexo4Clauses
                .Where(c => c.WorkItemCategoryAnexo4ClauseId.HasValue)
                .Select(c => c.WorkItemCategoryAnexo4ClauseId!.Value)
                .ToHashSet();

            var anexo4ToDelete = await _context.WorkItemCategoryAnexo4Clause
                .Where(c => c.WorkItemCategoryId == dto.WorkItemCategoryId
                         && c.State
                         && !incomingAnexo4Ids.Contains(c.WorkItemCategoryAnexo4ClauseId))
                .ToListAsync();
            foreach (var c in anexo4ToDelete)
            {
                c.State           = false;
                c.UpdatedDatetime = now;
                c.UpdatedUserId   = userId;
            }

            foreach (var clauseDto in dto.Anexo4Clauses)
            {
                if (clauseDto.WorkItemCategoryAnexo4ClauseId.HasValue)
                {
                    var existing = await _context.WorkItemCategoryAnexo4Clause
                        .FirstOrDefaultAsync(c => c.WorkItemCategoryAnexo4ClauseId == clauseDto.WorkItemCategoryAnexo4ClauseId.Value);
                    if (existing is not null)
                    {
                        existing.ClauseText      = clauseDto.ClauseText.Trim();
                        existing.SortOrder       = clauseDto.SortOrder;
                        existing.UpdatedDatetime = now;
                        existing.UpdatedUserId   = userId;
                    }
                }
                else
                {
                    _context.WorkItemCategoryAnexo4Clause.Add(new WorkItemCategoryAnexo4Clause
                    {
                        WorkItemCategoryId = dto.WorkItemCategoryId,
                        ClauseText         = clauseDto.ClauseText.Trim(),
                        SortOrder          = clauseDto.SortOrder,
                        State              = true,
                        CreatedDatetime    = now,
                        CreatedUserId      = userId,
                    });
                }
            }

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

        public async Task UpdateManualInstructivo(int workItemCategoryId, string fileUrl, string fileName, int userId)
        {
            var record = await _context.WorkItemCategory
                .FirstOrDefaultAsync(x => x.WorkItemCategoryId == workItemCategoryId && x.State)
                ?? throw new AbrilException("La partida de control no existe.");

            record.InstructivosFolderId   = fileUrl;
            record.InstructivosFolderName = fileName;
            record.InstructivosSyncStatus = 2;
            record.InstructivosSyncedAt   = DateTimeOffset.UtcNow;
            record.UpdatedDateTime        = DateTimeOffset.UtcNow;
            record.UpdatedUserId          = userId;

            await _context.SaveChangesAsync();
        }
    }
}
