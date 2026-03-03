using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Infrastructure.Repositories {
    public class IvtControlPdfRepository : IIvtControlPdfRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public IvtControlPdfRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task<bool> Create(int scheduleId, string fileUrl, int userId, string fileDescription)
        {
            var entity = new IvtControlPdf
            {
                ScheduleId = scheduleId,
                FileUrl = fileUrl,
                FileDescription = fileDescription,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                UpdatedDateTime = null,
                Active = true,
                State = true
            };
            _context.IvtControlPdf.Add(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<PagedResult<IvtControlPdfGetDTO>> GetPaged(int page)
        {
            const int pageSize = 10;

            var query = from item in _context.IvtControlPdf
                where item.State == true
                select new IvtControlPdfGetDTO
                {
                    FileUrl = item.FileUrl,
                    FileDescription = item.FileDescription
                };

            var totalRecords = await query.CountAsync();

            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return new PagedResult<IvtControlPdfGetDTO>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = data
            };
        }
    }
}