using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;

namespace Abril_Backend.Infrastructure.Repositories {
    public class PhaseRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public PhaseRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }
        public async Task<List<PhaseDTO>> GetAll()
        {
            var registros = _context.Phase
                .OrderBy(item => item.PhaseDescription)
                .Select(item => new PhaseDTO
                {
                    PhaseId = item.PhaseId,
                    PhaseDescription = item.PhaseDescription,

                    CreatedDateTime = item.CreatedDateTime,
                    CreatedUserId = item.CreatedUserId,
                    UpdatedDateTime = item.UpdatedDateTime,
                    UpdatedUserId = item.UpdatedUserId,
                    Active = item.Active
                });
            return await registros.ToListAsync();
        }
        public async Task<List<PhaseSimpleDTO>> GetAllFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.Phase
                .Where(item => item.State)
                .OrderBy(item => item.Order)
                .Select(item => new PhaseSimpleDTO
                {
                    PhaseId = item.PhaseId,
                    PhaseDescription = item.PhaseDescription,
                });
            return await registros.ToListAsync();
        }
        public async Task<object> GetPaged(int page)
        {
            const int pageSize = 10;

            var query = from phase in _context.Phase
                        where phase.State == true
                        orderby phase.PhaseId descending
                        select new PhaseDTO
                        {
                            PhaseId = phase.PhaseId,
                            PhaseDescription = phase.PhaseDescription,
                            CreatedDateTime = phase.CreatedDateTime,
                            CreatedUserId = phase.CreatedUserId,
                            UpdatedDateTime = phase.UpdatedDateTime,
                            UpdatedUserId = phase.UpdatedUserId,
                            Active = phase.Active
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

        public async Task<Phase> Create(PhaseCreateDTO dto, int userId)
        {
            var phase = await _context.Phase.FirstOrDefaultAsync(a => a.PhaseDescription == dto.PhaseDescription.Trim());

            if (phase != null && phase.State)
                throw new AbrilException("La fase ya existe");

            if (phase != null && !phase.State)
            {
                phase.State = true;
                phase.Active = dto.Active;
                phase.UpdatedDateTime = DateTime.UtcNow;
                phase.UpdatedUserId = userId;

                await _context.SaveChangesAsync();
                return phase;
            }

            phase = new Phase
            {
                PhaseDescription = dto.PhaseDescription.Trim(),
                Active = dto.Active,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            _context.Phase.Add(phase);
            await _context.SaveChangesAsync();

            return phase;
        }

        public async Task<Phase> Update(PhaseEditDTO dto, int userId)
        {
            var phase = await _context.Phase.FirstOrDefaultAsync(p => p.PhaseId == dto.PhaseId);

            if (phase == null)
                throw new AbrilException("La fase no existe");

            var duplicate = await _context.Phase.FirstOrDefaultAsync(p =>
                p.PhaseDescription == dto.PhaseDescription.Trim() &&
                p.PhaseId != dto.PhaseId &&
                p.State
            );

            if (duplicate != null)
                throw new AbrilException("Ya existe otra fase con la misma descripción");

            phase.PhaseDescription = dto.PhaseDescription.Trim();
            phase.Active = dto.Active;
            phase.UpdatedDateTime = DateTime.UtcNow;
            phase.UpdatedUserId = userId;

            await _context.SaveChangesAsync();

            return phase;
        }

        public async Task<bool> DeleteSoftAsync(int phaseId, int userId)
        {
            var phase = await _context.Phase.FirstOrDefaultAsync(u => u.PhaseId == phaseId && u.State == true);

            if (phase == null)
                return false;

            phase.State = false;
            phase.Active = false;
            phase.UpdatedDateTime = DateTime.UtcNow;
            phase.UpdatedUserId = userId;

            await _context.SaveChangesAsync();

            return true;
        }
    }
}