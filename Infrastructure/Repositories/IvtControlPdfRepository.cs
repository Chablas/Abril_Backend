using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Infrastructure.Repositories {
    public class IvtControlPdfRepository : IIvtControlPdfRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public IvtControlPdfRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task<bool> Create(int scheduleId, string fileUrl, int userId)
        {
            var entity = new IvtControlPdf
            {
                ScheduleId = scheduleId,
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
    }
}