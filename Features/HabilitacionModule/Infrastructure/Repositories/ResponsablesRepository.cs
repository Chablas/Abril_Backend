using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Responsables;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    /// <summary>
    /// Administradores/coordinadores responsables de notificaciones (EMOs, interconsultas, etc.)
    /// por razón social y por proyecto — pantalla "Gestión de Responsables" para que Habilitación
    /// pueda mantenerlos al día sin depender de Configuración > Proyectos.
    /// </summary>
    public class ResponsablesRepository : IResponsablesRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ResponsablesRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<ResponsablesDto> GetAll()
        {
            using var ctx = _factory.CreateDbContext();

            var razonesSociales = await ctx.Contributor
                .Where(c => c.EsAbril && c.State)
                .OrderBy(c => c.ContributorName)
                .Select(c => new ResponsableRazonSocialDto
                {
                    ContributorId = c.ContributorId,
                    ContributorName = c.ContributorName,
                    EmailAdministrador = c.EmailAdministrador
                })
                .ToListAsync();

            var proyectos = await ctx.Project
                .Where(p => p.State)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new ResponsableProyectoDto
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                    EmailResponsable = p.EmailResponsable,
                    EmailRrhh = p.EmailRrhh,
                    EmailCoordSsoma = p.EmailCoordSsoma,
                    EmailCoordAdmin = p.EmailCoordAdmin
                })
                .ToListAsync();

            // Solo personal Casa: los responsables/coordinadores son siempre personal propio de
            // Abril, no contratistas — mismo criterio que InterconsultaRepository.List.
            var trabajadores = await ctx.Worker
                .Where(w => w.ContrataCasa == "Casa"
                         && w.Estado != "RETIRADO"
                         && w.EmailCorporativo != null && w.EmailCorporativo != "")
                .Select(w => new ResponsableWorkerOptionDto
                {
                    WorkerId = w.Id,
                    NombreCompleto = w.Person != null ? w.Person.FullName! : "",
                    Email = w.EmailCorporativo!
                })
                .AsNoTracking()
                .ToListAsync();

            trabajadores = trabajadores
                .OrderBy(w => w.NombreCompleto, StringComparer.CurrentCulture)
                .ToList();

            return new ResponsablesDto
            {
                RazonesSociales = razonesSociales,
                Proyectos = proyectos,
                Trabajadores = trabajadores
            };
        }

        public async Task UpdateRazonSocial(int contributorId, ResponsableRazonSocialUpdateDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var contributor = await ctx.Contributor.FirstOrDefaultAsync(c => c.ContributorId == contributorId)
                ?? throw new AbrilException("La razón social no existe.", 404);

            contributor.EmailAdministrador = string.IsNullOrWhiteSpace(dto.EmailAdministrador)
                ? null
                : dto.EmailAdministrador.Trim();
            contributor.UpdatedDateTime = DateTimeOffset.UtcNow;

            await ctx.SaveChangesAsync();
        }

        public async Task UpdateProyecto(int projectId, ResponsableProyectoUpdateDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var project = await ctx.Project.FirstOrDefaultAsync(p => p.ProjectId == projectId)
                ?? throw new AbrilException("El proyecto no existe.", 404);

            project.EmailResponsable = string.IsNullOrWhiteSpace(dto.EmailResponsable)
                ? null
                : dto.EmailResponsable.Trim();
            project.EmailRrhh = string.IsNullOrWhiteSpace(dto.EmailRrhh)
                ? null
                : dto.EmailRrhh.Trim();
            project.EmailCoordSsoma = string.IsNullOrWhiteSpace(dto.EmailCoordSsoma)
                ? null
                : dto.EmailCoordSsoma.Trim();
            project.EmailCoordAdmin = string.IsNullOrWhiteSpace(dto.EmailCoordAdmin)
                ? null
                : dto.EmailCoordAdmin.Trim();
            project.UpdatedDateTime = DateTime.UtcNow;

            await ctx.SaveChangesAsync();
        }
    }
}
