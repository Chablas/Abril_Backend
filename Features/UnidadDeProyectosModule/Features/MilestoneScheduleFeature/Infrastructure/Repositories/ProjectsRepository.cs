using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Repositories
{
    public class ProjectsRepository : IProjectsRepository
    {
        private readonly AppDbContext _context;

        public ProjectsRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Tarjetas del Cronograma de Hitos. Entran los proyectos que ya estaban (con fila en la
        /// tabla antigua <c>project_resident</c>) y además los de Unidad de Proyectos que tienen
        /// residente en Emails SSOMA, que es el que puede subir versiones. El residente que se
        /// muestra es el de Emails SSOMA (<c>project.residente_workers_id</c>).
        /// Con <paramref name="soloDelResidente"/> quedan solo los proyectos donde el usuario es
        /// ese residente (lo decide ProjectsService).
        /// </summary>
        public async Task<PagedResult<MilestoneProjectDTO>> GetPagedWithResidents(int userId, bool soloDelResidente, int page, int pageSize = 10, string? search = null)
        {
            var projectQuery = _context.Project
                .Where(p => p.Active && p.State
                    && (_context.ProjectResident.Any(pr => pr.ProjectId == p.ProjectId && pr.Active && pr.State)
                        || (p.ResidenteWorkersId != null && p.TieneUnidadDeProyectos))
                    && (!soloDelResidente || _context.Worker.Any(w =>
                        w.Id == p.ResidenteWorkersId && w.Person != null && w.Person.UserId == userId))
                    && (search == null || p.ProjectDescription.ToLower().Contains(search.ToLower())))
                .OrderByDescending(p => p.ProjectId);

            var totalRecords = await projectQuery.CountAsync();

            // Residente y "¿soy yo?" en la misma consulta de la página (subconsultas, sin N+1).
            var data = await projectQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new MilestoneProjectDTO
                {
                    ProjectId          = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                    LevelDescription   = p.LevelDescription,
                    FotoUrl            = p.FotoUrl,
                    ResidenteNombre    = _context.Worker
                        .Where(w => w.Id == p.ResidenteWorkersId)
                        .Select(w => w.Person != null ? w.Person.FullName : null)
                        .FirstOrDefault(),
                    EsResidenteDelProyecto = _context.Worker.Any(w =>
                        w.Id == p.ResidenteWorkersId && w.Person != null && w.Person.UserId == userId)
                })
                .ToListAsync();

            return new PagedResult<MilestoneProjectDTO>
            {
                Page         = page,
                PageSize     = pageSize,
                TotalRecords = totalRecords,
                TotalPages   = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data         = data
            };
        }

        public async Task UpdateFotoUrlAsync(int projectId, string? fotoUrl, int userId)
        {
            var project = await _context.Project
                .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.State);
            if (project == null)
                throw new AbrilException("Proyecto no encontrado.", 404);

            project.FotoUrl = fotoUrl;
            project.UpdatedDateTime = DateTime.UtcNow;
            project.UpdatedUserId = userId;
            await _context.SaveChangesAsync();
        }

        /// <summary>Solo la característica (p. ej. "5 pisos + 2 sótanos"). Antes se hacía con el
        /// PUT completo de Configuración → Proyectos, que obligaba a leer el proyecto entero.</summary>
        public async Task<string?> UpdateLevelDescriptionAsync(int projectId, string? levelDescription, int userId)
        {
            var project = await _context.Project
                .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.State);
            if (project == null)
                throw new AbrilException("Proyecto no encontrado.", 404);

            var valor = string.IsNullOrWhiteSpace(levelDescription) ? null : levelDescription.Trim();
            if (valor != null && valor.Length > 1000)
                throw new AbrilException("La característica no puede superar los 1000 caracteres.");

            project.LevelDescription = valor;
            project.UpdatedDateTime = DateTime.UtcNow;
            project.UpdatedUserId = userId;
            await _context.SaveChangesAsync();
            return valor;
        }
    }
}
