using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Infrastructure.Repositories
{
    public class ProjectsDashboardRepository : IProjectsDashboardRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ProjectsDashboardRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<(List<string> Estados, List<ResponsableArqComSimpleDto> ResponsablesArqCom)> GetFiltersDataFactory()
        {
            using var ctx1 = _factory.CreateDbContext();
            using var ctx2 = _factory.CreateDbContext();

            var estadosTask = ctx1.Project
                .Where(p => p.State && p.TieneArquitecturaComercial && p.Estado != null)
                .Select(p => p.Estado!)
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();

            var workerIdsTask = ctx2.Project
                .Where(p => p.State && p.TieneArquitecturaComercial && p.ResponsableArqComId.HasValue)
                .Select(p => p.ResponsableArqComId!.Value)
                .Distinct()
                .ToListAsync();

            await Task.WhenAll(estadosTask, workerIdsTask);

            var workerIds = workerIdsTask.Result;
            using var ctx3 = _factory.CreateDbContext();
            var responsables = await ctx3.Worker
                .Where(w => workerIds.Contains(w.Id))
                .Select(w => new ResponsableArqComSimpleDto
                {
                    WorkerId = w.Id,
                    FullName = w.Person != null ? w.Person.FullName : null
                })
                .OrderBy(r => r.FullName)
                .ToListAsync();

            return (estadosTask.Result, responsables);
        }

        public async Task<List<ProyectoDetalleDto>> GetDashboardDataFactory(int? proyectoId, string? estado, int? responsableArqComId)
        {
            using var ctx = _factory.CreateDbContext();
            var today = DateOnly.FromDateTime(DateTime.Today);

            var query = ctx.Project
                .Where(p => p.State && p.TieneArquitecturaComercial);

            if (proyectoId.HasValue)
                query = query.Where(p => p.ProjectId == proyectoId.Value);
            if (estado != null)
                query = query.Where(p => p.Estado == estado);
            if (responsableArqComId.HasValue)
                query = query.Where(p => p.ResponsableArqComId == responsableArqComId.Value);

            var projects = await query
                .Select(p => new
                {
                    p.ProjectId,
                    p.ProjectDescription,
                    p.Estado,
                    p.ResponsableArqCom
                })
                .ToListAsync();

            var projectIds = projects.Select(p => p.ProjectId).ToList();

            var actividades = await ctx.AcActividad
                .Where(a => projectIds.Contains(a.ProjectId) && a.Activo)
                .Select(a => new
                {
                    a.ProjectId,
                    a.FinEfectivo,
                    a.FinProgramado,
                    a.InicioEfectivo
                })
                .ToListAsync();

            var actsByProject = actividades
                .GroupBy(a => a.ProjectId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return projects.Select(p =>
            {
                var acts = actsByProject.TryGetValue(p.ProjectId, out var list) ? list : new();
                var total = acts.Count;
                var culminadas = acts.Count(a => a.FinEfectivo != null);
                var vencidas = acts.Count(a => a.FinProgramado < today && a.FinEfectivo == null);
                var enProceso = acts.Count(a => a.FinEfectivo == null && a.InicioEfectivo != null);
                var avance = total > 0 ? Math.Round((double)culminadas / total * 100, 1) : 0d;

                return new ProyectoDetalleDto
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                    Estado = p.Estado,
                    ResponsableArqCom = p.ResponsableArqCom,
                    TotalActividades = total,
                    Culminadas = culminadas,
                    EnProceso = enProceso,
                    Vencidas = vencidas,
                    PorcentajeAvance = avance,
                    EstaConRetraso = vencidas > 0
                };
            }).ToList();
        }
    }
}
