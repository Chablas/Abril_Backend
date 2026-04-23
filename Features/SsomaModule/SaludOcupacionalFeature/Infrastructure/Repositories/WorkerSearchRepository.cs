using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class WorkerSearchRepository : IWorkerSearchRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public WorkerSearchRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<WorkerSearchResultDto>> Search(string? q, int limit)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var workers = ctx.Worker.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                workers = workers.Where(w =>
                    (w.ApellidoNombre != null && w.ApellidoNombre.ToLower().Contains(term))
                    || (w.Dni != null && w.Dni.ToLower().Contains(term)));
            }

            var baseList = await workers
                .OrderBy(w => w.ApellidoNombre)
                .Take(limit)
                .Select(w => new
                {
                    w.Id,
                    w.ApellidoNombre,
                    w.Dni,
                    w.Ocupacion,
                    w.Estado
                })
                .ToListAsync();

            var ids = baseList.Select(b => b.Id).ToList();

            var vinculacionActual = await (
                from v in ctx.WorkerVinculacion
                join em in ctx.Empresa on v.EmpresaId equals em.Id into ej
                from em in ej.DefaultIfEmpty()
                where ids.Contains(v.WorkerId)
                      && (v.FechaFin == null || v.FechaFin >= hoy)
                orderby v.FechaInicio descending
                select new
                {
                    v.WorkerId,
                    v.EmpresaId,
                    EmpresaNombre = em != null ? em.RazonSocial : null,
                    v.FechaInicio
                }).ToListAsync();

            var porWorker = vinculacionActual
                .GroupBy(x => x.WorkerId)
                .ToDictionary(g => g.Key, g => g.First());

            return baseList.Select(b =>
            {
                porWorker.TryGetValue(b.Id, out var vin);
                return new WorkerSearchResultDto
                {
                    Id = b.Id,
                    ApellidoNombre = b.ApellidoNombre,
                    Dni = b.Dni,
                    Ocupacion = b.Ocupacion,
                    EmpresaActualId = vin?.EmpresaId,
                    EmpresaActual = vin?.EmpresaNombre,
                    Activo = !string.IsNullOrWhiteSpace(b.Estado)
                             && b.Estado.Trim().Equals("ACTIVO", StringComparison.OrdinalIgnoreCase)
                };
            }).ToList();
        }
    }
}
