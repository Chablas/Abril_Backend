using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using System.Linq;
using DocumentFormat.OpenXml.Office.CustomUI;

namespace Abril_Backend.Infrastructure.Repositories {
    public class MilestoneScheduleHistoryRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public MilestoneScheduleHistoryRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task<List<MilestoneScheduleHistoryDTO>> GetAllByScheduleIdFactory(int scheduleId)
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.MilestoneScheduleHistory
                .Where(item => item.State)
                .Where(item => item.ScheduleId == scheduleId)
                .OrderByDescending(item => item.CreatedDateTime)
                .Select(item => new MilestoneScheduleHistoryDTO
                {
                    MilestoneScheduleHistoryId = item.MilestoneScheduleHistoryId,
                    ScheduleId = item.ScheduleId,
                    CreatedDateTime = item.CreatedDateTime,
                    CreatedUserId = item.CreatedUserId,
                    UpdatedDateTime = item.UpdatedDateTime,
                    UpdatedUserId = item.UpdatedUserId,
                    Active = item.Active
                });
            return await registros.ToListAsync();
        }

        public async Task<List<MilestoneSchedule>?> Create(MilestoneScheduleHistoryCreateDTO dto, int userId)
        {
            var lastHistory = await _context.MilestoneScheduleHistory
                .Where(h => h.ScheduleId == dto.ScheduleId && h.Active && h.State)
                .OrderByDescending(h => h.CreatedDateTime)
                .FirstOrDefaultAsync();

            if (lastHistory != null)
            {
                var lastMilestones = await _context.MilestoneSchedule
                    .Where(ms => ms.MilestoneScheduleHistoryId == lastHistory.MilestoneScheduleHistoryId
                                 && ms.Active && ms.State)
                    .OrderBy(ms => ms.Order)
                    .ToListAsync();

                var newMilestones = dto.MilestoneSchedules
                    .OrderBy(ms => ms.Order)
                    .ToList();

                if (lastMilestones.Count == newMilestones.Count)
                {
                    bool areEqual = true;

                    for (int i = 0; i < lastMilestones.Count; i++)
                    {
                        var last = lastMilestones[i];
                        var current = newMilestones[i];

                        if (last.MilestoneId != current.MilestoneId ||
                            last.Order != current.Order ||
                            last.PlannedStartDate != current.PlannedStartDate ||
                            last.PlannedEndDate != current.PlannedEndDate)
                        {
                            areEqual = false;
                            break;
                        }
                    }

                    if (areEqual)
                        throw new AbrilException("El cronograma es igual a la última versión subida.");
                }
            }

            var history = new MilestoneScheduleHistory
            {
                ScheduleId = dto.ScheduleId,
                Active = true,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            _context.MilestoneScheduleHistory.Add(history);
            await _context.SaveChangesAsync();

            var milestoneSchedules = dto.MilestoneSchedules.Select(item => new MilestoneSchedule
            {
                MilestoneId = item.MilestoneId,
                MilestoneScheduleHistoryId = history.MilestoneScheduleHistoryId,
                Order = item.Order,
                PlannedStartDate = item.PlannedStartDate,
                PlannedEndDate = item.PlannedEndDate,
                Active = true,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            }).ToList();

            _context.MilestoneSchedule.AddRange(milestoneSchedules);
            await _context.SaveChangesAsync();

            return milestoneSchedules;
        }

        /*

        public async Task<object> GetPaged(int page)
        {
            const int pageSize = 10;

            var query = from milestoneScheduleHistory in _context.MilestoneScheduleHistory
                        where milestoneScheduleHistory.State == true
                        orderby milestoneScheduleHistory.MilestoneScheduleHistoryId descending
                        select new MilestoneScheduleHistoryDTO
                        {
                            MilestoneScheduleHistoryId = milestoneScheduleHistory.MilestoneScheduleHistoryId,
                            MilestoneScheduleHistoryDescription = milestoneScheduleHistory.MilestoneScheduleHistoryDescription,
                            CreatedDateTime = milestoneScheduleHistory.CreatedDateTime,
                            CreatedUserId = milestoneScheduleHistory.CreatedUserId,
                            UpdatedDateTime = milestoneScheduleHistory.UpdatedDateTime,
                            UpdatedUserId = milestoneScheduleHistory.UpdatedUserId,
                            Active = milestoneScheduleHistory.Active
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

        public async Task<MilestoneScheduleHistory> Create(MilestoneScheduleHistoryCreateDTO dto, int userId)
        {
            var milestoneScheduleHistory = await _context.MilestoneScheduleHistory.FirstOrDefaultAsync(a => a.MilestoneScheduleHistoryDescription == dto.MilestoneScheduleHistoryDescription.Trim());

            if (milestoneScheduleHistory != null && milestoneScheduleHistory.State)
                throw new AbrilException("El área ya existe");

            if (milestoneScheduleHistory != null && !milestoneScheduleHistory.State)
            {
                milestoneScheduleHistory.State = true;
                milestoneScheduleHistory.Active = dto.Active;
                milestoneScheduleHistory.UpdatedDateTime = DateTime.UtcNow;
                milestoneScheduleHistory.UpdatedUserId = userId;

                await _context.SaveChangesAsync();
                return milestoneScheduleHistory;
            }

            milestoneScheduleHistory = new MilestoneScheduleHistory
            {
                MilestoneScheduleHistoryDescription = dto.MilestoneScheduleHistoryDescription.Trim(),
                Active = dto.Active,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            _context.MilestoneScheduleHistory.Add(milestoneScheduleHistory);
            await _context.SaveChangesAsync();

            return milestoneScheduleHistory;
        }

        public async Task<MilestoneScheduleHistory> Update(MilestoneScheduleHistoryEditDTO dto, int userId)
        {
            var milestoneScheduleHistory = await _context.MilestoneScheduleHistory.FirstOrDefaultAsync(p => p.MilestoneScheduleHistoryId == dto.MilestoneScheduleHistoryId);

            if (milestoneScheduleHistory == null)
                throw new AbrilException("El milestoneScheduleHistory no existe");

            var duplicate = await _context.MilestoneScheduleHistory.FirstOrDefaultAsync(p =>
                p.MilestoneScheduleHistoryDescription == dto.MilestoneScheduleHistoryDescription.Trim() &&
                p.MilestoneScheduleHistoryId != dto.MilestoneScheduleHistoryId &&
                p.State
            );

            if (duplicate != null)
                throw new AbrilException("Ya existe otra milestoneScheduleHistory con la misma descripción");

            milestoneScheduleHistory.MilestoneScheduleHistoryDescription = dto.MilestoneScheduleHistoryDescription.Trim();
            milestoneScheduleHistory.Active = dto.Active;
            milestoneScheduleHistory.UpdatedDateTime = DateTime.UtcNow;
            milestoneScheduleHistory.UpdatedUserId = userId;

            await _context.SaveChangesAsync();

            return milestoneScheduleHistory;
        }
        */
    }
}