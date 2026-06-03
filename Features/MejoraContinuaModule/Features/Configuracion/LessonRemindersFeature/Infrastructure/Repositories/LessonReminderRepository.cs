using Abril_Backend.Application.DTOs;
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

        public async Task<object> GetPaged(int page, int pageSize, string? subarea = null)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 10 : pageSize;

            using var ctx = _factory.CreateDbContext();

            var baseQuery =
                from up in ctx.UserProject
                join u in ctx.User on up.UserId equals u.UserId
                join p in ctx.Person on u.UserId equals p.UserId
                join pj in ctx.Project on up.ProjectId equals pj.ProjectId
                where up.State == true
                select new { up, p, pj };

            // Filtro opcional por subárea: el usuario pertenece (vía person) a un
            // worker cuya subarea coincide con la seleccionada.
            if (!string.IsNullOrWhiteSpace(subarea))
            {
                var sa = subarea.Trim();
                baseQuery = baseQuery.Where(x =>
                    ctx.Worker.Any(w => w.PersonId == x.p.PersonId && w.Subarea == sa));
            }

            var query = baseQuery
                .OrderByDescending(x => x.up.UserProjectId)
                .Select(x => new LessonReminderDTO
                {
                    UserProjectId = x.up.UserProjectId,
                    UserId = x.up.UserId,
                    UserFullName = x.p.FullName,
                    ProjectId = x.up.ProjectId,
                    ProjectDescription = x.pj.ProjectDescription ?? string.Empty,
                    CreatedDateTime = x.up.CreatedDateTime,
                    CreatedUserId = x.up.CreatedUserId,
                    UpdatedDateTime = x.up.UpdatedDateTime,
                    UpdatedUserId = x.up.UpdatedUserId,
                    Active = x.up.Active
                });

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

            // IMPORTANTE: ejecutar SECUENCIALMENTE sobre una sola instancia de DbContext.
            // DbContext no es thread-safe; lanzar ambas queries con Task.WhenAll sobre el
            // mismo ctx provoca "A second operation was started on this context instance"
            // (Npgsql/PostgreSQL no soporta operaciones concurrentes en la misma conexión).
            var users = await (
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

            var projects = await ctx.Project
                .Where(p => p.State && p.Active)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new LessonReminderProjectDTO
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription ?? string.Empty
                })
                .ToListAsync();

            return new LessonReminderCreateDataDTO
            {
                Users = users,
                Projects = projects
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

        public async Task<ToggleLessonReminderResultDTO> ToggleActiveAsync(int userProjectId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var userProject = await ctx.UserProject
                .FirstOrDefaultAsync(u => u.UserProjectId == userProjectId && u.State == true);

            if (userProject == null)
                throw new AbrilException("El recordatorio no existe.", 404);

            // Invierte solo Active (State permanece true). El cron filtra por Active == true,
            // así que un recordatorio inactivo deja de enviarse sin perder el registro.
            userProject.Active = !userProject.Active;
            userProject.UpdatedDateTime = DateTime.UtcNow;
            userProject.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();

            return new ToggleLessonReminderResultDTO
            {
                UserProjectId = userProject.UserProjectId,
                Active = userProject.Active
            };
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

        public async Task<List<PendingStaffMemberDTO>> GetPendingMembersForProjectAsync(
            int projectId,
            string period,
            IReadOnlyList<string> emails)
        {
            if (emails == null || emails.Count == 0)
                return new List<PendingStaffMemberDTO>();

            // Normalizar: trim + lower, dedup
            var normalized = emails
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Trim().ToLower())
                .Distinct()
                .ToList();

            if (normalized.Count == 0)
                return new List<PendingStaffMemberDTO>();

            using var ctx = _factory.CreateDbContext();

            // Match case-insensitive contra user.email (también lowercased en el lado servidor)
            var matchedUsers = await (
                from u in ctx.User
                join p in ctx.Person on u.UserId equals p.UserId into pj
                from p in pj.DefaultIfEmpty()
                where u.Email != null && normalized.Contains(u.Email.ToLower())
                select new
                {
                    u.UserId,
                    Email = u.Email!,
                    FullName = p != null ? p.FullName : null
                }
            ).ToListAsync();

            var userByEmail = matchedUsers
                .GroupBy(x => x.Email.Trim().ToLower())
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // ¿Qué user_ids ya cumplieron (tienen lección del proyecto/período)?
            var matchedUserIds = matchedUsers.Select(u => u.UserId).Distinct().ToList();

            var compliedUserIds = matchedUserIds.Count == 0
                ? new HashSet<int>()
                : (await ctx.Lesson
                    .Where(l => matchedUserIds.Contains(l.CreatedUserId)
                                && l.ProjectId == projectId
                                && l.Period == period
                                && l.State == true
                                && l.Active == true)
                    .Select(l => l.CreatedUserId)
                    .Distinct()
                    .ToListAsync())
                    .ToHashSet();

            var pending = new List<PendingStaffMemberDTO>();
            foreach (var email in normalized)
            {
                if (userByEmail.TryGetValue(email, out var u))
                {
                    if (!compliedUserIds.Contains(u.UserId))
                    {
                        pending.Add(new PendingStaffMemberDTO
                        {
                            Email = u.Email,        // versión "original" guardada en BD
                            UserId = u.UserId,
                            FullName = u.FullName
                        });
                    }
                }
                else
                {
                    // No existe en user → no hay cómo verificar cumplimiento → pendiente.
                    pending.Add(new PendingStaffMemberDTO
                    {
                        Email = email,
                        UserId = null,
                        FullName = null
                    });
                }
            }

            return pending;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Usuarios pendientes (user_project) — consumido por ReminderService
        // ─────────────────────────────────────────────────────────────────────

        public async Task<List<UserWithoutLessonsDTO>> GetUsersWithoutLessonsThisMonth(string period)
        {
            using var ctx = _factory.CreateDbContext();

            // El período viene del llamador (formato "MM-yyyy") para que la fecha
            // simulada de ReminderService se propague correctamente al filtro.
            var currentPeriod = period;

            var query =
                from up in ctx.UserProject
                join u in ctx.User on up.UserId equals u.UserId
                join p in ctx.Person on u.UserId equals p.UserId
                join pj in ctx.Project on up.ProjectId equals pj.ProjectId
                where up.State == true
                      && up.Active == true
                      && !ctx.Lesson.Any(l =>
                             l.CreatedUserId == up.UserId &&
                             l.ProjectId == up.ProjectId &&
                             l.Period == currentPeriod &&
                             l.State == true &&
                             l.Active == true
                         )
                group new { up, pj, u } by new
                {
                    up.UserId,
                    p.FullName,
                    u.Email
                }
                into g
                select new UserWithoutLessonsDTO
                {
                    UserId = g.Key.UserId,
                    UserFullName = g.Key.FullName,
                    Email = g.Key.Email,
                    Projects = g.Select(x => new ProjectSimpleDTO
                    {
                        ProjectId = x.pj.ProjectId,
                        ProjectDescription = x.pj.ProjectDescription ?? string.Empty
                    }).ToList()
                };

            return await query.ToListAsync();
        }

        public async Task<List<UserWithoutLessonsDTO>> GetUsersWithoutLessonsByPeriod(DateTime periodDate)
        {
            using var ctx = _factory.CreateDbContext();

            var targetPeriod = new DateTime(periodDate.Year, periodDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var query =
                from up in ctx.UserProject
                join u in ctx.User on up.UserId equals u.UserId
                join p in ctx.Person on u.UserId equals p.UserId
                join pj in ctx.Project on up.ProjectId equals pj.ProjectId
                where up.State == true
                      && up.Active == true
                      && !ctx.Lesson.Any(l =>
                             l.CreatedUserId == up.UserId &&
                             l.ProjectId == up.ProjectId &&
                             l.PeriodDate == targetPeriod &&
                             l.State == true &&
                             l.Active == true
                         )
                group new { up, pj, u } by new
                {
                    up.UserId,
                    p.FullName,
                    u.Email
                }
                into g
                select new UserWithoutLessonsDTO
                {
                    UserId = g.Key.UserId,
                    UserFullName = g.Key.FullName,
                    Email = g.Key.Email,
                    Projects = g.Select(x => new ProjectSimpleDTO
                    {
                        ProjectId = x.pj.ProjectId,
                        ProjectDescription = x.pj.ProjectDescription ?? string.Empty
                    }).ToList()
                };

            return await query.ToListAsync();
        }
    }
}
