using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;

namespace Abril_Backend.Infrastructure.Repositories {
    public class LayerRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public LayerRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }
        public async Task<List<LayerDTO>> GetAll()
        {
            var registros = _context.Layer
                .Where(item => item.State)
                .OrderBy(item => item.LayerDescription)
                .Select(item => new LayerDTO
                {
                    LayerId = item.LayerId,
                    LayerDescription = item.LayerDescription,

                    CreatedDateTime = item.CreatedDateTime,
                    CreatedUserId = item.CreatedUserId,
                    UpdatedDateTime = item.UpdatedDateTime,
                    UpdatedUserId = item.UpdatedUserId,
                    Active = item.Active
                });
            return await registros.ToListAsync();
        }
        public async Task<List<LayerSimpleDTO>> GetAllFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.Layer
                .OrderBy(item => item.LayerDescription)
                .Select(item => new LayerSimpleDTO
                {
                    LayerId = item.LayerId,
                    LayerDescription = item.LayerDescription,
                });
            return await registros.ToListAsync();
        }
        public async Task<object> GetPaged(int page)
        {
            const int pageSize = 10;

            var query = from layer in _context.Layer
                        where layer.State == true
                        orderby layer.LayerId descending
                        select new LayerDTO
                        {
                            LayerId = layer.LayerId,
                            LayerDescription = layer.LayerDescription,
                            CreatedDateTime = layer.CreatedDateTime,
                            CreatedUserId = layer.CreatedUserId,
                            UpdatedDateTime = layer.UpdatedDateTime,
                            UpdatedUserId = layer.UpdatedUserId,
                            Active = layer.Active
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

        public async Task<Layer> Create(LayerCreateDTO dto, int userId)
        {
            var layer = await _context.Layer.FirstOrDefaultAsync(a => a.LayerDescription == dto.LayerDescription.Trim());

            if (layer != null && layer.State)
                throw new AbrilException("El nivel ya existe");

            if (layer != null && !layer.State)
            {
                layer.State = true;
                layer.Active = dto.Active;
                layer.UpdatedDateTime = DateTime.UtcNow;
                layer.UpdatedUserId = userId;

                await _context.SaveChangesAsync();
                return layer;
            }

            layer = new Layer
            {
                LayerDescription = dto.LayerDescription.Trim(),
                Active = dto.Active,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            _context.Layer.Add(layer);
            await _context.SaveChangesAsync();

            return layer;
        }

        public async Task<Layer> Update(LayerEditDTO dto, int userId)
        {
            var layer = await _context.Layer.FirstOrDefaultAsync(p => p.LayerId == dto.LayerId);

            if (layer == null)
                throw new AbrilException("El proyecto no existe");

            var duplicate = await _context.Layer.FirstOrDefaultAsync(p =>
                p.LayerDescription == dto.LayerDescription.Trim() &&
                p.LayerId != dto.LayerId &&
                p.State
            );

            if (duplicate != null)
                throw new AbrilException("Ya existe otro proyecto con la misma descripción");

            layer.LayerDescription = dto.LayerDescription.Trim();
            layer.Active = dto.Active;
            layer.UpdatedDateTime = DateTime.UtcNow;
            layer.UpdatedUserId = userId;

            await _context.SaveChangesAsync();

            return layer;
        }

        public async Task<bool> DeleteSoftAsync(int layerId, int userId)
        {
            var layer = await _context.Layer.FirstOrDefaultAsync(u => u.LayerId == layerId && u.State == true);

            if (layer == null)
                return false;

            layer.State = false;
            layer.Active = false;
            layer.UpdatedDateTime = DateTime.UtcNow;
            layer.UpdatedUserId = userId;

            _context.Layer.Update(layer);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}