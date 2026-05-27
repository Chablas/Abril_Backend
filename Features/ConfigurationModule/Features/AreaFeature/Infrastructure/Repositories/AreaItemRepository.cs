using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Dtos;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models;

namespace Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Repositories
{
    public class AreaItemRepository : IAreaItemRepository
    {
        private readonly AppDbContext _context;

        public AreaItemRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<AreaItemDto>> GetPaged(AreaItemFilterDto filter)
        {
            var query =
                from i in _context.AreaItem
                join t in _context.AreaType on i.AreaTypeId equals t.AreaTypeId
                join parent in _context.AreaItem on i.AreaItemParentId equals parent.AreaItemId into pj
                from parent in pj.DefaultIfEmpty()
                select new { i, t, parent };

            if (filter.AreaTypeId.HasValue)
                query = query.Where(x => x.i.AreaTypeId == filter.AreaTypeId.Value);

            if (filter.AreaItemParentId.HasValue)
                query = query.Where(x => x.i.AreaItemParentId == filter.AreaItemParentId.Value);

            if (filter.Active.HasValue)
                query = query.Where(x => x.i.Active == filter.Active.Value);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim().ToLower();
                query = query.Where(x => x.i.AreaItemName.ToLower().Contains(s));
            }

            query = query.OrderBy(x => x.t.AreaTypeName).ThenBy(x => x.i.AreaItemName);

            var totalRecords = await query.CountAsync();

            var data = await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x => new AreaItemDto
                {
                    AreaItemId = x.i.AreaItemId,
                    AreaItemName = x.i.AreaItemName,
                    AreaTypeId = x.i.AreaTypeId,
                    AreaTypeName = x.t.AreaTypeName,
                    AreaItemParentId = x.i.AreaItemParentId,
                    AreaItemParentName = x.parent != null ? x.parent.AreaItemName : null,
                    Active = x.i.Active,
                })
                .ToListAsync();

            return new PagedResult<AreaItemDto>
            {
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)filter.PageSize),
                Data = data
            };
        }

        public async Task<List<AreaItemSimpleDto>> GetSimple(int? areaTypeId)
        {
            var query = _context.AreaItem.Where(i => i.Active);
            if (areaTypeId.HasValue)
                query = query.Where(i => i.AreaTypeId == areaTypeId.Value);

            return await query
                .OrderBy(i => i.AreaItemName)
                .Select(i => new AreaItemSimpleDto
                {
                    AreaItemId = i.AreaItemId,
                    AreaItemName = i.AreaItemName,
                    AreaTypeId = i.AreaTypeId,
                    AreaItemParentId = i.AreaItemParentId,
                })
                .ToListAsync();
        }

        public async Task<List<AreaItemTreeDto>> GetTree(int? areaTypeId)
        {
            var query =
                from i in _context.AreaItem
                join t in _context.AreaType on i.AreaTypeId equals t.AreaTypeId
                where i.Active
                select new { i, t };

            if (areaTypeId.HasValue)
                query = query.Where(x => x.i.AreaTypeId == areaTypeId.Value);

            var flat = await query
                .OrderBy(x => x.i.AreaItemName)
                .Select(x => new AreaItemTreeDto
                {
                    AreaItemId = x.i.AreaItemId,
                    AreaItemName = x.i.AreaItemName,
                    AreaTypeId = x.i.AreaTypeId,
                    AreaTypeName = x.t.AreaTypeName,
                    AreaItemParentId = x.i.AreaItemParentId,
                    Active = x.i.Active,
                })
                .ToListAsync();

            var map = flat.ToDictionary(x => x.AreaItemId);
            var roots = new List<AreaItemTreeDto>();
            foreach (var node in flat)
            {
                if (node.AreaItemParentId.HasValue && map.TryGetValue(node.AreaItemParentId.Value, out var parent))
                    parent.Children.Add(node);
                else
                    roots.Add(node);
            }
            return roots;
        }

        public async Task Create(AreaItemCreateDto dto)
        {
            var name = dto.AreaItemName.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new AbrilException("La descripción es obligatoria.");

            var typeExists = await _context.AreaType.AnyAsync(t => t.AreaTypeId == dto.AreaTypeId);
            if (!typeExists)
                throw new AbrilException("El tipo de área no existe.");

            if (dto.AreaItemParentId.HasValue)
            {
                var parentExists = await _context.AreaItem.AnyAsync(p => p.AreaItemId == dto.AreaItemParentId.Value);
                if (!parentExists)
                    throw new AbrilException("El área padre no existe.");
            }

            var duplicate = await _context.AreaItem.FirstOrDefaultAsync(i =>
                i.AreaItemName.ToLower() == name.ToLower() &&
                i.AreaTypeId == dto.AreaTypeId &&
                i.AreaItemParentId == dto.AreaItemParentId);
            if (duplicate != null)
                throw new AbrilException("Ya existe un área con esa descripción en el mismo nivel.");

            _context.AreaItem.Add(new AreaItem
            {
                AreaItemName = name,
                AreaTypeId = dto.AreaTypeId,
                AreaItemParentId = dto.AreaItemParentId,
                Active = dto.Active
            });
            await _context.SaveChangesAsync();
        }

        public async Task Update(AreaItemEditDto dto)
        {
            var entity = await _context.AreaItem.FirstOrDefaultAsync(i => i.AreaItemId == dto.AreaItemId);
            if (entity == null)
                throw new AbrilException("El área no existe.");

            var name = dto.AreaItemName.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new AbrilException("La descripción es obligatoria.");

            if (dto.AreaItemParentId.HasValue && dto.AreaItemParentId.Value == dto.AreaItemId)
                throw new AbrilException("El área no puede ser su propio padre.");

            // Evitar ciclo: el nuevo padre no puede ser descendiente del nodo actual
            if (dto.AreaItemParentId.HasValue)
            {
                var descendantIds = await GetDescendantIdsAsync(dto.AreaItemId);
                if (descendantIds.Contains(dto.AreaItemParentId.Value))
                    throw new AbrilException("El área padre seleccionada genera un ciclo.");
            }

            var typeExists = await _context.AreaType.AnyAsync(t => t.AreaTypeId == dto.AreaTypeId);
            if (!typeExists)
                throw new AbrilException("El tipo de área no existe.");

            var duplicate = await _context.AreaItem.FirstOrDefaultAsync(i =>
                i.AreaItemName.ToLower() == name.ToLower() &&
                i.AreaTypeId == dto.AreaTypeId &&
                i.AreaItemParentId == dto.AreaItemParentId &&
                i.AreaItemId != dto.AreaItemId);
            if (duplicate != null)
                throw new AbrilException("Ya existe otra área con esa descripción en el mismo nivel.");

            entity.AreaItemName = name;
            entity.AreaTypeId = dto.AreaTypeId;
            entity.AreaItemParentId = dto.AreaItemParentId;
            entity.Active = dto.Active;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteSoftAsync(int areaItemId)
        {
            var entity = await _context.AreaItem.FirstOrDefaultAsync(i => i.AreaItemId == areaItemId);
            if (entity == null) return false;

            var hasActiveChildren = await _context.AreaItem.AnyAsync(c => c.AreaItemParentId == areaItemId && c.Active);
            if (hasActiveChildren)
                throw new AbrilException("No se puede eliminar: existen subáreas activas.");

            entity.Active = false;
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<HashSet<int>> GetDescendantIdsAsync(int areaItemId)
        {
            var all = await _context.AreaItem
                .Select(i => new { i.AreaItemId, i.AreaItemParentId })
                .ToListAsync();

            var children = all.GroupBy(x => x.AreaItemParentId ?? 0)
                              .ToDictionary(g => g.Key, g => g.Select(c => c.AreaItemId).ToList());

            var result = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(areaItemId);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!children.TryGetValue(current, out var kids)) continue;
                foreach (var k in kids)
                    if (result.Add(k)) stack.Push(k);
            }
            return result;
        }
    }
}
