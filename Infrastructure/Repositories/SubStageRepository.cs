using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;

namespace Abril_Backend.Infrastructure.Repositories {
    public class SubStageRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public SubStageRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task<List<SubStageDTO>> GetAll()
        {
            var registros = _context.SubStage
                .Where(item => item.State)
                .OrderBy(item => item.SubStageDescription)
                .Select(item => new SubStageDTO
                {
                    SubStageId = item.SubStageId,
                    SubStageDescription = item.SubStageDescription,

                    CreatedDateTime = item.CreatedDateTime,
                    CreatedUserId = item.CreatedUserId,
                    UpdatedDateTime = item.UpdatedDateTime,
                    UpdatedUserId = item.UpdatedUserId,
                    Active = item.Active
                });
            return await registros.ToListAsync();
        }
        public async Task<List<SubStageDTO>> GetAllFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.SubStage
                .OrderBy(item => item.SubStageDescription)
                .Select(item => new SubStageDTO
                {
                    SubStageId = item.SubStageId,
                    SubStageDescription = item.SubStageDescription,

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

            var query = from substage in _context.SubStage
                        where substage.State == true
                        orderby substage.SubStageId descending
                        select new SubStageDTO
                        {
                            SubStageId = substage.SubStageId,
                            SubStageDescription = substage.SubStageDescription,
                            CreatedDateTime = substage.CreatedDateTime,
                            CreatedUserId = substage.CreatedUserId,
                            UpdatedDateTime = substage.UpdatedDateTime,
                            UpdatedUserId = substage.UpdatedUserId,
                            Active = substage.Active
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

        public async Task<SubStage> Create(SubStageCreateDTO dto, int userId)
        {
            var subStage = await _context.SubStage.FirstOrDefaultAsync(a => a.SubStageDescription == dto.SubStageDescription.Trim());

            if (subStage != null && subStage.State)
                throw new AbrilException("La subetapa ya existe");

            if (subStage != null && !subStage.State)
            {
                subStage.State = true;
                subStage.Active = dto.Active;
                subStage.UpdatedDateTime = DateTime.UtcNow;
                subStage.UpdatedUserId = userId;

                await _context.SaveChangesAsync();
                return subStage;
            }

            subStage = new SubStage
            {
                SubStageDescription = dto.SubStageDescription.Trim(),
                Active = dto.Active,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            _context.SubStage.Add(subStage);
            await _context.SaveChangesAsync();

            return subStage;
        }

        public async Task<SubStage> Update(SubStageEditDTO dto, int userId)
        {
            var subStage = await _context.SubStage.FirstOrDefaultAsync(p => p.SubStageId == dto.SubStageId);

            if (subStage == null)
                throw new AbrilException("La subetapa no existe");

            var duplicate = await _context.SubStage.FirstOrDefaultAsync(p =>
                p.SubStageDescription == dto.SubStageDescription.Trim() &&
                p.SubStageId != dto.SubStageId &&
                p.State
            );

            if (duplicate != null)
                throw new AbrilException("Ya existe otra subetapa con la misma descripción");

            subStage.SubStageDescription = dto.SubStageDescription.Trim();
            subStage.Active = dto.Active;
            subStage.UpdatedDateTime = DateTime.UtcNow;
            subStage.UpdatedUserId = userId;

            await _context.SaveChangesAsync();

            return subStage;
        }

        public async Task<bool> DeleteSoftAsync(int subStageId, int userId)
        {
            var subStage = await _context.SubStage.FirstOrDefaultAsync(u => u.SubStageId == subStageId && u.State == true);

            if (subStage == null)
                return false;

            subStage.State = false;
            subStage.Active = false;
            subStage.UpdatedDateTime = DateTime.UtcNow;
            subStage.UpdatedUserId = userId;

            _context.SubStage.Update(subStage);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}