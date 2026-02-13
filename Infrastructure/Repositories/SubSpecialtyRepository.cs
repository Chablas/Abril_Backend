using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;

namespace Abril_Backend.Infrastructure.Repositories {
    public class SubSpecialtyRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public SubSpecialtyRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task<List<SubSpecialtyDTO>> GetAll()
        {
            var registros = _context.SubSpecialty
                .OrderBy(item => item.SubSpecialtyDescription)
                .Select(item => new SubSpecialtyDTO
                {
                    SubSpecialtyId = item.SubSpecialtyId,
                    SubSpecialtyDescription = item.SubSpecialtyDescription,

                    CreatedDateTime = item.CreatedDateTime,
                    CreatedUserId = item.CreatedUserId,
                    UpdatedDateTime = item.UpdatedDateTime,
                    UpdatedUserId = item.UpdatedUserId,
                    Active = item.Active
                });
            return await registros.ToListAsync();
        }
        public async Task<List<SubSpecialtyDTO>> GetAllFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.SubSpecialty
                .Where(item => item.State)
                .OrderBy(item => item.SubSpecialtyDescription)
                .Select(item => new SubSpecialtyDTO
                {
                    SubSpecialtyId = item.SubSpecialtyId,
                    SubSpecialtyDescription = item.SubSpecialtyDescription,

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

            var query = from subspecialty in _context.SubSpecialty
                        where subspecialty.State == true
                        orderby subspecialty.SubSpecialtyId descending
                        select new SubSpecialtyDTO
                        {
                            SubSpecialtyId = subspecialty.SubSpecialtyId,
                            SubSpecialtyDescription = subspecialty.SubSpecialtyDescription,
                            CreatedDateTime = subspecialty.CreatedDateTime,
                            CreatedUserId = subspecialty.CreatedUserId,
                            UpdatedDateTime = subspecialty.UpdatedDateTime,
                            UpdatedUserId = subspecialty.UpdatedUserId,
                            Active = subspecialty.Active
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

        public async Task<SubSpecialty> Create(SubSpecialtyCreateDTO dto, int userId)
        {
            var subSpecialty = await _context.SubSpecialty.FirstOrDefaultAsync(a => a.SubSpecialtyDescription == dto.SubSpecialtyDescription.Trim());

            if (subSpecialty != null && subSpecialty.State)
                throw new AbrilException("La subespecialidad ya existe");

            if (subSpecialty != null && !subSpecialty.State)
            {
                subSpecialty.State = true;
                subSpecialty.Active = dto.Active;
                subSpecialty.UpdatedDateTime = DateTime.UtcNow;
                subSpecialty.UpdatedUserId = userId;

                await _context.SaveChangesAsync();
                return subSpecialty;
            }

            subSpecialty = new SubSpecialty
            {
                SubSpecialtyDescription = dto.SubSpecialtyDescription.Trim(),
                Active = dto.Active,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            _context.SubSpecialty.Add(subSpecialty);
            await _context.SaveChangesAsync();

            return subSpecialty;
        }

        public async Task<SubSpecialty> Update(SubSpecialtyEditDTO dto, int userId)
        {
            var subSpecialty = await _context.SubSpecialty.FirstOrDefaultAsync(p => p.SubSpecialtyId == dto.SubSpecialtyId);

            if (subSpecialty == null)
                throw new AbrilException("La subespecialidad no existe");

            var duplicate = await _context.SubSpecialty.FirstOrDefaultAsync(p =>
                p.SubSpecialtyDescription == dto.SubSpecialtyDescription.Trim() &&
                p.SubSpecialtyId != dto.SubSpecialtyId &&
                p.State
            );

            if (duplicate != null)
                throw new AbrilException("Ya existe otra subespecialidad con la misma descripción");

            subSpecialty.SubSpecialtyDescription = dto.SubSpecialtyDescription.Trim();
            subSpecialty.Active = dto.Active;
            subSpecialty.UpdatedDateTime = DateTime.UtcNow;
            subSpecialty.UpdatedUserId = userId;

            await _context.SaveChangesAsync();

            return subSpecialty;
        }

        public async Task<bool> DeleteSoftAsync(int subSpecialtyId, int userId)
        {
            var subSpecialty = await _context.SubSpecialty.FirstOrDefaultAsync(u => u.SubSpecialtyId == subSpecialtyId && u.State == true);

            if (subSpecialty == null)
                return false;

            subSpecialty.State = false;
            subSpecialty.Active = false;
            subSpecialty.UpdatedDateTime = DateTime.UtcNow;
            subSpecialty.UpdatedUserId = userId;

            _context.SubSpecialty.Update(subSpecialty);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}