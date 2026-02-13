using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using System.Linq;

namespace Abril_Backend.Infrastructure.Repositories {
    public class MilestoneScheduleRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public MilestoneScheduleRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task<List<MilestoneScheduleDTO>> GetAllFactory(int milestoneScheduleId)
        {
            using var ctx = _factory.CreateDbContext();

            var registros = from ms in ctx.MilestoneSchedule
                            join m in ctx.Milestone
                                on ms.MilestoneId equals m.MilestoneId
                            where (ms.State == true) && (ms.MilestoneScheduleId == milestoneScheduleId)
                            select new MilestoneScheduleDTO
                            {
                                MilestoneScheduleId = ms.MilestoneScheduleId,
                                MilestoneId = ms.MilestoneId,
                                MilestoneDescription = m.MilestoneDescription,
                                MilestoneScheduleHistoryId = ms.MilestoneScheduleHistoryId,
                                PlannedStartDate = ms.PlannedStartDate,
                                PlannedEndDate = ms.PlannedEndDate,
                                CreatedDateTime = ms.CreatedDateTime,
                                CreatedUserId = ms.CreatedUserId,
                                UpdatedDateTime = ms.UpdatedDateTime,
                                UpdatedUserId = ms.UpdatedUserId,
                                Active = ms.Active
                            };

            return await registros.ToListAsync();
        }
    }
}