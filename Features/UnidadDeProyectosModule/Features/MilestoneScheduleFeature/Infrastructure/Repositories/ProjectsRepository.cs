using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Shared.Services.Residentes.Services;
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
        /// Tarjetas del Cronograma de Hitos: el universo de obras con residente (visible, tipo que
        /// es obra, ciclo ACTIVO y residente con usuario y rol RESIDENTE; ver ResidenteQueries). Una
        /// obra nueva entra sola al ponerle residente en Configuración → Proyectos: ya no depende de
        /// la tabla antigua <c>project_resident</c> ni de Unidad de Proyectos. El residente que se
        /// muestra es ese (<c>project.residente_workers_id</c>).
        /// Con <paramref name="soloDelResidente"/> quedan solo las obras donde el usuario es ese
        /// residente (lo decide ProjectsService).
        /// </summary>
        public async Task<PagedResult<MilestoneProjectDTO>> GetPagedWithResidents(int userId, bool soloDelResidente, int page, int pageSize = 10, string? search = null)
        {
            // Subconsultas de la misma consulta (sin N+1): la regla vive en ResidenteQueries.
            var obras = _context.ObrasConResidente();
            var delUsuario = _context.ProyectosDelResidente(userId);

            var projectQuery = _context.Project
                .Where(p => obras.Contains(p.ProjectId)
                    && (!soloDelResidente || delUsuario.Contains(p.ProjectId))
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
                    EsResidenteDelProyecto = delUsuario.Contains(p.ProjectId)
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
