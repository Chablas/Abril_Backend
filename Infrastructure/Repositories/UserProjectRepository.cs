using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using System.Linq;

namespace Abril_Backend.Infrastructure.Repositories {
    public class UserProjectRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public UserProjectRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task<object> GetPagedFactory(int page, int pageSizeQuery)
        {
            int pageSize = pageSizeQuery;
            page = page < 1 ? 1 : page;

            using var ctx = _factory.CreateDbContext();

            var query =
                from up in ctx.UserProject
                join u in ctx.User on up.UserId equals u.UserId
                join p in ctx.Person on u.PersonId equals p.PersonId
                join pj in ctx.Project on up.ProjectId equals pj.ProjectId
                where up.State == true
                orderby up.UserProjectId descending
                select new UserProjectDTO
                {
                    UserProjectId = up.UserProjectId,
                    UserId = up.UserId,
                    UserFullName = p.FullName,
                    ProjectId = up.ProjectId,
                    ProjectDescription = pj.ProjectDescription,
                    CreatedDateTime = up.CreatedDateTime,
                    CreatedUserId = up.CreatedUserId,
                    UpdatedDateTime = up.UpdatedDateTime,
                    UpdatedUserId = up.UpdatedUserId,
                    Active = up.Active
                };

            var totalRecords = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new
            {
                page,
                pageSize,
                totalRecords,
                totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                data
            };
        }

        public async Task<List<UserWithoutLessonsDTO>> GetUsersWithoutLessonsThisMonth()
        {
            using var ctx = _factory.CreateDbContext();

            var currentPeriod = DateTime.UtcNow
                
                .ToString("MM-yyyy");

            var query =
                from up in ctx.UserProject
                join u in ctx.User on up.UserId equals u.UserId
                join p in ctx.Person on u.PersonId equals p.PersonId
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
                group new { up, pj } by new
                {
                    up.UserId,
                    p.FullName,
                    p.Email
                }
                into g
                select new UserWithoutLessonsDTO
                {
                    UserId = g.Key.UserId,
                    UserFullName = g.Key.FullName,
                    Email = g.Key.Email,
                    Projects = g.Select(x => new ProjectFilterDTO
                    {
                        ProjectId = x.pj.ProjectId,
                        ProjectDescription = x.pj.ProjectDescription
                    }).ToList()
                };

            return await query.ToListAsync();
        }

        public async Task<UserProject> Create(UserProjectCreateDTO dto, int userId)
        {
            var userProject = await _context.UserProject.FirstOrDefaultAsync(up => up.UserId == dto.UserId &&
                up.ProjectId == dto.ProjectId
            );

            if (userProject != null && userProject.State && userProject.Active)
                throw new AbrilException("El usuario ya está asignado a este proyecto");

            if (userProject != null && !userProject.State)
            {
                userProject.State = true;
                userProject.Active = dto.Active;
                userProject.UpdatedDateTime = DateTime.UtcNow;
                userProject.UpdatedUserId = userId;

                await _context.SaveChangesAsync();
                return userProject;
            }

            if (userProject!= null && userProject.State && !userProject.Active)
            {
                throw new AbrilException("El usuario ya está asignado a este proyecto, pero se encuentra inactivo. Reactívelo para continuar.");
            }

            userProject = new UserProject
            {
                UserId = dto.UserId,    
                ProjectId = dto.ProjectId,
                
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                Active = dto.Active,
                State = true,
            };

            _context.UserProject.Add(userProject);
            await _context.SaveChangesAsync();

            return userProject;
        }

        public async Task<bool> DeleteSoftAsync(int userProjectId, int updatedUserId)
        {
            var userProject = await _context.UserProject.FirstOrDefaultAsync(u => u.UserProjectId == userProjectId && u.State == true);

            if (userProject == null)
            {
                throw new AbrilException("El usuario no tiene asignado el proyecto especificado");
            }

            userProject.State = false;
            userProject.Active = false;
            userProject.UpdatedDateTime = DateTime.UtcNow;
            userProject.UpdatedUserId = updatedUserId;

            _context.UserProject.Update(userProject);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}