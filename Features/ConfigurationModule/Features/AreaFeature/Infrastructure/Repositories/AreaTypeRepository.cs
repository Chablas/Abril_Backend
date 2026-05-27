using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Dtos;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models;

namespace Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Repositories
{
    public class AreaTypeRepository : IAreaTypeRepository
    {
        private readonly AppDbContext _context;

        public AreaTypeRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<AreaTypeDto>> GetPaged(int page, int pageSize)
        {
            var query = _context.AreaType.OrderBy(t => t.AreaTypeName);
            var totalRecords = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new AreaTypeDto
                {
                    AreaTypeId = t.AreaTypeId,
                    AreaTypeName = t.AreaTypeName,
                    Active = t.Active,
                })
                .ToListAsync();

            return new PagedResult<AreaTypeDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = data
            };
        }

        public async Task<List<AreaTypeSimpleDto>> GetSimple()
        {
            return await _context.AreaType
                .Where(t => t.Active)
                .OrderBy(t => t.AreaTypeName)
                .Select(t => new AreaTypeSimpleDto
                {
                    AreaTypeId = t.AreaTypeId,
                    AreaTypeName = t.AreaTypeName,
                })
                .ToListAsync();
        }

        public async Task Create(AreaTypeCreateDto dto)
        {
            var name = dto.AreaTypeName.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new AbrilException("La descripción es obligatoria.");

            var duplicate = await _context.AreaType
                .FirstOrDefaultAsync(t => t.AreaTypeName.ToLower() == name.ToLower());
            if (duplicate != null)
                throw new AbrilException("Ya existe un tipo de área con esa descripción.");

            _context.AreaType.Add(new AreaType
            {
                AreaTypeName = name,
                Active = dto.Active
            });
            await _context.SaveChangesAsync();
        }

        public async Task Update(AreaTypeEditDto dto)
        {
            var entity = await _context.AreaType.FirstOrDefaultAsync(t => t.AreaTypeId == dto.AreaTypeId);
            if (entity == null)
                throw new AbrilException("El tipo de área no existe.");

            var name = dto.AreaTypeName.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new AbrilException("La descripción es obligatoria.");

            var duplicate = await _context.AreaType
                .FirstOrDefaultAsync(t => t.AreaTypeName.ToLower() == name.ToLower() && t.AreaTypeId != dto.AreaTypeId);
            if (duplicate != null)
                throw new AbrilException("Ya existe otro tipo de área con esa descripción.");

            entity.AreaTypeName = name;
            entity.Active = dto.Active;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteSoftAsync(int areaTypeId)
        {
            var entity = await _context.AreaType.FirstOrDefaultAsync(t => t.AreaTypeId == areaTypeId);
            if (entity == null) return false;

            var inUse = await _context.AreaItem.AnyAsync(i => i.AreaTypeId == areaTypeId && i.Active);
            if (inUse)
                throw new AbrilException("No se puede eliminar: existen áreas activas que usan este tipo.");

            entity.Active = false;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
