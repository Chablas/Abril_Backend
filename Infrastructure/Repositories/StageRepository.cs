using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;

namespace Abril_Backend.Infrastructure.Repositories {
    public class StageRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public StageRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task<List<StageDTO>> GetAll()
        {
            var registros = _context.Stage
                .Where(item => item.State)
                .OrderBy(item => item.StageDescription)
                .Select(item => new StageDTO
                {
                    StageId = item.StageId,
                    StageDescription = item.StageDescription,

                    CreatedDateTime = item.CreatedDateTime,
                    CreatedUserId = item.CreatedUserId,
                    UpdatedDateTime = item.UpdatedDateTime,
                    UpdatedUserId = item.UpdatedUserId,
                    Active = item.Active
                });
            return await registros.ToListAsync();
        }
        public async Task<List<StageDTO>> GetAllFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.Stage
                .OrderBy(item => item.StageDescription)
                .Select(item => new StageDTO
                {
                    StageId = item.StageId,
                    StageDescription = item.StageDescription,

                    CreatedDateTime = item.CreatedDateTime,
                    CreatedUserId = item.CreatedUserId,
                    UpdatedDateTime = item.UpdatedDateTime,
                    UpdatedUserId = item.UpdatedUserId,
                    Active = item.Active
                });
            return await registros.ToListAsync();
        }

        public async Task<object> GetPaged(int page)
        {
            const int pageSize = 10;

            var query = from stage in _context.Stage
                        where stage.State == true
                        orderby stage.StageId descending
                        select new StageDTO
                        {
                            StageId = stage.StageId,
                            StageDescription = stage.StageDescription,
                            CreatedDateTime = stage.CreatedDateTime,
                            CreatedUserId = stage.CreatedUserId,
                            UpdatedDateTime = stage.UpdatedDateTime,
                            UpdatedUserId = stage.UpdatedUserId,
                            Active = stage.Active
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

        public async Task<Stage> Create(StageCreateDTO dto, int userId)
        {
            var stage = await _context.Stage.FirstOrDefaultAsync(a => a.StageDescription == dto.StageDescription.Trim());

            if (stage != null && stage.State)
                throw new AbrilException("La etapa ya existe");

            if (stage != null && !stage.State)
            {
                stage.State = true;
                stage.Active = dto.Active;
                stage.UpdatedDateTime = DateTime.UtcNow;
                stage.UpdatedUserId = userId;

                await _context.SaveChangesAsync();
                return stage;
            }

            stage = new Stage
            {
                StageDescription = dto.StageDescription.Trim(),
                Active = dto.Active,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            _context.Stage.Add(stage);
            await _context.SaveChangesAsync();

            return stage;
        }

        public async Task<Stage> Update(StageEditDTO dto, int userId)
        {
            var stage = await _context.Stage.FirstOrDefaultAsync(p => p.StageId == dto.StageId);

            if (stage == null)
                throw new AbrilException("La etapa no existe");

            var duplicate = await _context.Stage.FirstOrDefaultAsync(p =>
                p.StageDescription == dto.StageDescription.Trim() &&
                p.StageId != dto.StageId &&
                p.State
            );

            if (duplicate != null)
                throw new AbrilException("Ya existe otra etapa con la misma descripción");

            stage.StageDescription = dto.StageDescription.Trim();
            stage.Active = dto.Active;
            stage.UpdatedDateTime = DateTime.UtcNow;
            stage.UpdatedUserId = userId;

            await _context.SaveChangesAsync();

            return stage;
        }

        public async Task<bool> DeleteSoftAsync(int stageId, int userId)
        {
            var stage = await _context.Stage.FirstOrDefaultAsync(u => u.StageId == stageId && u.State == true);

            if (stage == null)
                return false;

            stage.State = false;
            stage.Active = false;
            stage.UpdatedDateTime = DateTime.UtcNow;
            stage.UpdatedUserId = userId;

            _context.Stage.Update(stage);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}