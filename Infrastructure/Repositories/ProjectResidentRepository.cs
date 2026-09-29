using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Residentes.Services;

namespace Abril_Backend.Infrastructure.Repositories {
    /// <summary>
    /// Proyectos del módulo de Residentes (Control de IVTs, Cuaderno de obra, Control de respuesta
    /// de informes y Seguimiento). Ya no lee la tabla antigua <c>project_resident</c>: sale del
    /// residente de Configuración → Proyectos (ResidenteQueries). El nombre se queda hasta el Paso 6
    /// de PLAN-RESIDENTES.md.
    /// </summary>
    public class ProjectResidentRepository : IProjectResidentRepository {
        private readonly IDbContextFactory<AppDbContext> _factory;
        public ProjectResidentRepository(IDbContextFactory<AppDbContext> factory) {
            _factory = factory;
        }

        /// <summary>Filtros y combos: las obras con residente, visibles y sin excluir de RESIDENTES.</summary>
        public async Task<List<ProjectSimpleDTO>> GetProjectsDescription()
        {
            using var ctx = _factory.CreateDbContext();

            return await ctx.ObrasEnResidentes()
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new ProjectSimpleDTO
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription ?? string.Empty
                })
                .ToListAsync();
        }

        /// <summary>Las obras donde el usuario es el residente, visibles y sin excluir de RESIDENTES.</summary>
        public async Task<List<ProjectSimpleDTO>> GetProjectByResidentUserId(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            return await ctx.ObrasDelResidenteEnResidentes(userId)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new ProjectSimpleDTO
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription ?? string.Empty,
                })
                .ToListAsync();
        }
    }
}
