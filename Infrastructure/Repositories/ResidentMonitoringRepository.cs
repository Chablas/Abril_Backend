using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Services.Residentes.Services;

namespace Abril_Backend.Infrastructure.Repositories
{
    public class ResidentMonitoringRepository : IResidentMonitoringRepository
    {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public ResidentMonitoringRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory)
        {
            _context = contexto;
            _factory = factory;
        }

        /// <summary>
        /// Una fila por obra del módulo de Residentes (ResidenteQueries: obras con residente, visibles
        /// y sin excluir de RESIDENTES) con su residente de Configuración → Proyectos; ya no la tabla
        /// antigua project_resident. Los conteos son de la obra: como no hay historial de residentes
        /// anterior al 2026-09-29, los meses pasados quedan a nombre del residente actual, igual que
        /// antes.
        /// </summary>
        public async Task<IEnumerable<TrackingRawDto>> GetTrackingDataAsync(
            int? projectId,
            int? residentUserId,
            int? month,
            int? year)
        {
            return await (
                from p in _context.ObrasEnResidentes()
                join w in _context.Worker on p.ResidenteWorkersId equals (int?)w.Id
                join pe in _context.Person on w.PersonId equals (int?)pe.PersonId
                join u in _context.User on pe.UserId equals (int?)u.UserId
                where u.State && u.Active
                   && (!projectId.HasValue || p.ProjectId == projectId.Value)
                   && (!residentUserId.HasValue || u.UserId == residentUserId.Value)
                select new TrackingRawDto
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription ?? string.Empty,
                    ResidentUserId = u.UserId,
                    ResidentFullName = pe.FullName ?? (pe.FirstLastName + " " + pe.FirstNames),

                    ScheduleReportedCount = _context.MilestoneScheduleHistory
                        .Where(h =>
                            h.ProjectId == p.ProjectId
                            && h.State && h.Active
                            && (!month.HasValue || h.CreatedDateTime.AddHours(-5).Month == month.Value)
                            && (!year.HasValue || h.CreatedDateTime.AddHours(-5).Year == year.Value))
                        .Select(h => new
                        {
                            h.CreatedDateTime.AddHours(-5).Year,
                            h.CreatedDateTime.AddHours(-5).Month
                        })
                        .Distinct()
                        .Count(),

                    IvtsUploaded = _context.IvtControlPdf.Count(x =>
                        x.ProjectId == p.ProjectId
                        && x.State && x.Active
                        && (!month.HasValue || x.PeriodDate.Month == month.Value)
                        && (!year.HasValue || x.PeriodDate.Year == year.Value)),

                    ConstructionLogsUploaded = _context.ConstructionSiteLogbookControl.Count(x =>
                        x.ProjectId == p.ProjectId
                        && x.State && x.Active
                        && (!month.HasValue || x.PeriodDate.Month == month.Value)
                        && (!year.HasValue || x.PeriodDate.Year == year.Value)),

                    TotalIncidences = _context.ResidentReportIncidence.Count(x =>
                        x.ProjectId == p.ProjectId
                        && x.State && x.Active
                        && (!month.HasValue || x.CreatedDateTime.AddHours(-5).Month == month.Value)
                        && (!year.HasValue || x.CreatedDateTime.AddHours(-5).Year == year.Value)),

                    AnsweredIncidences = _context.ResidentReportIncidence.Count(x =>
                        x.ProjectId == p.ProjectId
                        && x.State && x.Active
                        && (!month.HasValue || x.CreatedDateTime.AddHours(-5).Month == month.Value)
                        && (!year.HasValue || x.CreatedDateTime.AddHours(-5).Year == year.Value)
                        && x.ResidentReportResponses.Any(r => r.State && r.Active)),
                }
            ).ToListAsync();
        }
    }
}