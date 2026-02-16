using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using System.Linq;

namespace Abril_Backend.Infrastructure.Repositories {
    public class AreaRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public AreaRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task<List<AreaDTO>> GetAll()
        {
            var registros = _context.Area
                .OrderBy(item => item.AreaDescription)
                .Select(item => new AreaDTO
                {
                    AreaId = item.AreaId,
                    AreaDescription = item.AreaDescription,

                    CreatedDateTime = item.CreatedDateTime,
                    CreatedUserId = item.CreatedUserId,
                    UpdatedDateTime = item.UpdatedDateTime,
                    UpdatedUserId = item.UpdatedUserId,
                    Active = item.Active
                });
            return await registros.ToListAsync();
        }

        public async Task<List<AreaSimpleDTO>> GetAllFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.Area
                .Where(item => item.State)
                .OrderBy(item => item.AreaDescription)
                .Select(item => new AreaSimpleDTO
                {
                    AreaId = item.AreaId,
                    AreaDescription = item.AreaDescription,
                });
            return await registros.ToListAsync();
        }

        public async Task<object> GetPaged(int page)
        {
            const int pageSize = 10;

            var query = from area in _context.Area
                        where area.State == true
                        orderby area.AreaId descending
                        select new AreaDTO
                        {
                            AreaId = area.AreaId,
                            AreaDescription = area.AreaDescription,
                            CreatedDateTime = area.CreatedDateTime,
                            CreatedUserId = area.CreatedUserId,
                            UpdatedDateTime = area.UpdatedDateTime,
                            UpdatedUserId = area.UpdatedUserId,
                            Active = area.Active
                        };

            var totalRecords = await query.CountAsync();

            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return new
            {
                page,
                pageSize,
                totalRecords,
                totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                data
            };
        }

        public async Task<Area> Create(AreaCreateDTO dto, int userId)
        {
            var area = await _context.Area.FirstOrDefaultAsync(a => a.AreaDescription == dto.AreaDescription.Trim());

            if (area != null && area.State)
                throw new AbrilException("El área ya existe");

            if (area != null && !area.State)
            {
                area.State = true;
                area.Active = dto.Active;
                area.UpdatedDateTime = DateTime.UtcNow;
                area.UpdatedUserId = userId;

                await _context.SaveChangesAsync();
                return area;
            }

            area = new Area
            {
                AreaDescription = dto.AreaDescription.Trim(),
                Active = dto.Active,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            _context.Area.Add(area);
            await _context.SaveChangesAsync();

            return area;
        }

        public async Task<Area> Update(AreaEditDTO dto, int userId)
        {
            var area = await _context.Area.FirstOrDefaultAsync(p => p.AreaId == dto.AreaId);

            if (area == null)
                throw new AbrilException("El area no existe");

            var duplicate = await _context.Area.FirstOrDefaultAsync(p =>
                p.AreaDescription == dto.AreaDescription.Trim() &&
                p.AreaId != dto.AreaId &&
                p.State
            );

            if (duplicate != null)
                throw new AbrilException("Ya existe otra area con la misma descripción");

            area.AreaDescription = dto.AreaDescription.Trim();
            area.Active = dto.Active;
            area.UpdatedDateTime = DateTime.UtcNow;
            area.UpdatedUserId = userId;

            await _context.SaveChangesAsync();

            return area;
        }

        public async Task<bool> DeleteSoftAsync(int areaId, int userId)
        {
            var area = await _context.Area.FirstOrDefaultAsync(u => u.AreaId == areaId && u.State == true);

            if (area == null)
                return false;

            area.State = false;
            area.Active = false;
            area.UpdatedDateTime = DateTime.UtcNow;
            area.UpdatedUserId = userId;

            _context.Area.Update(area);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}