using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using System.Linq;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Infrastructure.Repositories {
    public class MilestoneScheduleRepository : IMilestoneScheduleRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public MilestoneScheduleRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task<List<ScheduleChangeInfoDTO>> GetSchedulesWithChangesThisMonthAsync()
        {
            var now = DateTime.UtcNow;

            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var startOfNextMonth = startOfMonth.AddMonths(1);

            var data = await (
                from msh in _context.MilestoneScheduleHistory
                join p in _context.Project
                    on msh.ProjectId equals p.ProjectId
                join u in _context.User
                    on msh.CreatedUserId equals u.UserId
                join person in _context.Person
                    on u.PersonId equals person.PersonId
                where
                    msh.CreatedDateTime >= startOfMonth &&
                    msh.CreatedDateTime < startOfNextMonth &&
                    msh.Active && msh.State
                select new
                {
                    p.ProjectDescription,
                    ChangedBy = person.FullName,
                    ChangeDate = msh.CreatedDateTime
                }
            ).ToListAsync();

            var result = data
                .GroupBy(x => new { x.ProjectDescription, x.ChangedBy })
                .Select(g => new ScheduleChangeInfoDTO
                {
                    ProjectDescription = g.Key.ProjectDescription,
                    ChangedBy = g.Key.ChangedBy,
                    ChangeDate = g
                        .Select(x => x.ChangeDate)
                        .Distinct()
                        .OrderBy(d => d)
                        .ToList()
                })
                .ToList();

            return result;
        }

        public async Task<List<MilestoneScheduleDTO>> GetAllByMilestoneScheduleHistoryIdFactory(int milestoneScheduleHistoryId)
        {
            using var ctx = _factory.CreateDbContext();

            var registros = from ms in ctx.MilestoneSchedule
                            join m in ctx.Milestone
                                on ms.MilestoneId equals m.MilestoneId
                            where ms.State && ms.MilestoneScheduleHistoryId == milestoneScheduleHistoryId
                            orderby ms.Order
                            select new MilestoneScheduleDTO
                            {
                                MilestoneScheduleId = ms.MilestoneScheduleId,
                                MilestoneId = ms.MilestoneId,
                                MilestoneDescription = m.MilestoneDescription,
                                MilestoneScheduleHistoryId = ms.MilestoneScheduleHistoryId,
                                Order = ms.Order,
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
        /*
        public async Task<MilestoneSchedule> Create(MilestoneScheduleCreateDTO dto, int userId)
        {
            var milestoneSchedule = await _context.MilestoneSchedule.FirstOrDefaultAsync(a => a.MilestoneScheduleDescription == dto.MilestoneScheduleDescription.Trim());

            if (milestoneSchedule != null && milestoneSchedule.State)
                throw new AbrilException("El área ya existe");

            if (milestoneSchedule != null && !milestoneSchedule.State)
            {
                milestoneSchedule.State = true;
                milestoneSchedule.Active = dto.Active;
                milestoneSchedule.UpdatedDateTime = DateTime.UtcNow;
                milestoneSchedule.UpdatedUserId = userId;

                await _context.SaveChangesAsync();
                return milestoneSchedule;
            }

            milestoneSchedule = new MilestoneSchedule
            {
                MilestoneScheduleDescription = dto.MilestoneScheduleDescription.Trim(),
                Active = dto.Active,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            _context.MilestoneSchedule.Add(milestoneSchedule);
            await _context.SaveChangesAsync();

            return milestoneSchedule;
        }
        */
    }
}