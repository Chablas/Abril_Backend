using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Repositories
{
    public class LessonReminderRepository : ILessonReminderRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public LessonReminderRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<object> GetPaged(int page, int pageSize)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 10 : pageSize;

            using var ctx = _factory.CreateDbContext();

            var query =
                from up in ctx.UserProject
                join u in ctx.User on up.UserId equals u.UserId
                join p in ctx.Person on u.UserId equals p.UserId
                join pj in ctx.Project on up.ProjectId equals pj.ProjectId
                where up.State == true
                orderby up.UserProjectId descending
                select new LessonReminderDTO
                {
                    UserProjectId = up.UserProjectId,
                    UserId = up.UserId,
                    UserFullName = p.FullName,
                    ProjectId = up.ProjectId,
                    ProjectDescription = pj.ProjectDescription ?? string.Empty,
                    CreatedDateTime = up.CreatedDateTime,
                    CreatedUserId = up.CreatedUserId,
                    UpdatedDateTime = up.UpdatedDateTime,
                    UpdatedUserId = up.UpdatedUserId,
                    Active = up.Active
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

        public async Task<LessonReminderCreateDataDTO> GetCreateData()
        {
            using var ctx = _factory.CreateDbContext();

            var usersTask = (
                from u in ctx.User
                join p in ctx.Person on u.UserId equals p.UserId
                where u.Active == true
                      && u.State == true
                      && p.Active == true
                      && p.State == true
                      && u.EmailConfirmed == true
                orderby p.FullName
                select new LessonReminderUserDTO
                {
                    UserId = u.UserId,
                    FullName = p.FullName
                }
            ).ToListAsync();

            var projectsTask = ctx.Project
                .Where(p => p.State && p.Active)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new LessonReminderProjectDTO
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription ?? string.Empty
                })
                .ToListAsync();

            await Task.WhenAll(usersTask, projectsTask);

            return new LessonReminderCreateDataDTO
            {
                Users = await usersTask,
                Projects = await projectsTask
            };
        }

        public async Task Create(LessonReminderCreateDTO dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var existing = await ctx.UserProject
                .FirstOrDefaultAsync(up => up.UserId == dto.UserId && up.ProjectId == dto.ProjectId);

            if (existing != null && existing.State && existing.Active)
                throw new AbrilException("El usuario ya está asignado a este proyecto");

            if (existing != null && existing.State && !existing.Active)
                throw new AbrilException("El usuario ya está asignado a este proyecto, pero se encuentra inactivo. Reactívelo para continuar.");

            if (existing != null && !existing.State)
            {
                existing.State = true;
                existing.Active = dto.Active;
                existing.UpdatedDateTime = DateTime.UtcNow;
                existing.UpdatedUserId = userId;
                await ctx.SaveChangesAsync();
                return;
            }

            var userProject = new UserProject
            {
                UserId = dto.UserId,
                ProjectId = dto.ProjectId,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                Active = dto.Active,
                State = true
            };

            ctx.UserProject.Add(userProject);
            await ctx.SaveChangesAsync();
        }

        public async Task<bool> DeleteSoftAsync(int userProjectId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var userProject = await ctx.UserProject
                .FirstOrDefaultAsync(u => u.UserProjectId == userProjectId && u.State == true);

            if (userProject == null)
                throw new AbrilException("El usuario no tiene asignado el proyecto especificado");

            userProject.State = false;
            userProject.Active = false;
            userProject.UpdatedDateTime = DateTime.UtcNow;
            userProject.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Filtro project_staff_reminder (toggle por proyecto con staff_email)
        // ─────────────────────────────────────────────────────────────────────

        public async Task<List<ProjectStaffReminderConfigItemDTO>> GetAllProjectStaffAsync()
        {
            using var ctx = _factory.CreateDbContext();

            // Proyectos vivos que tienen staff_email registrado
            var projects = await ctx.Project
                .Where(p => p.State && p.Active && p.StaffEmail != null && p.StaffEmail != "")
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new
                {
                    p.ProjectId,
                    p.ProjectDescription,
                    p.StaffEmail
                })
                .ToListAsync();

            if (projects.Count == 0)
                return new List<ProjectStaffReminderConfigItemDTO>();

            var rows = await ctx.ProjectStaffReminder.ToListAsync();
            var byProjectId = rows.ToDictionary(r => r.ProjectId);

            return projects.Select(p =>
            {
                byProjectId.TryGetValue(p.ProjectId, out var row);
                return new ProjectStaffReminderConfigItemDTO
                {
                    ProjectStaffReminderId = row?.ProjectStaffReminderId,
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                    StaffEmail = p.StaffEmail,
                    Active = row != null && row.Active
                };
            }).ToList();
        }

        public async Task<ToggleProjectStaffReminderResultDTO> ToggleProjectStaffAsync(int projectId)
        {
            using var ctx = _factory.CreateDbContext();

            // Projection para evitar SELECT * (la tabla project puede tener columnas
            // declaradas en el modelo pero aún no presentes en algunas BDs, ej. responsable_udp).
            var projectInfo = await ctx.Project
                .Where(p => p.ProjectId == projectId && p.State && p.Active)
                .Select(p => new { p.ProjectId, p.StaffEmail })
                .FirstOrDefaultAsync();

            if (projectInfo == null)
                throw new AbrilException("El proyecto no existe o está inactivo.", 404);
            if (string.IsNullOrWhiteSpace(projectInfo.StaffEmail))
                throw new AbrilException("El proyecto no tiene un correo de staff configurado.", 400);

            var row = await ctx.ProjectStaffReminder.FirstOrDefaultAsync(r => r.ProjectId == projectId);
            if (row == null)
            {
                row = new ProjectStaffReminder
                {
                    ProjectId = projectId,
                    Active = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                ctx.ProjectStaffReminder.Add(row);
            }
            else
            {
                row.Active = !row.Active;
            }

            await ctx.SaveChangesAsync();
            return new ToggleProjectStaffReminderResultDTO
            {
                ProjectStaffReminderId = row.ProjectStaffReminderId,
                Active = row.Active
            };
        }

        public async Task<List<ActiveProjectStaffEmailDTO>> GetActiveStaffEmailsAsync()
        {
            using var ctx = _factory.CreateDbContext();

            return await (
                from r in ctx.ProjectStaffReminder
                join p in ctx.Project on r.ProjectId equals p.ProjectId
                where r.Active
                      && p.State
                      && p.Active
                      && p.StaffEmail != null
                      && p.StaffEmail != ""
                orderby p.ProjectDescription
                select new ActiveProjectStaffEmailDTO
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription ?? string.Empty,
                    StaffEmail = p.StaffEmail!
                }
            ).ToListAsync();
        }
    }
}
