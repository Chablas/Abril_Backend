using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Infrastructure.Repositories
{
    /// <summary>
    /// Lectura/escritura de los revisores de salidas por trabajador (workers_revisores):
    /// n revisores por solicitante, ordenados por prioridad (1 = primero). El primer
    /// revisor vivo + activo con correo @abril.pe recibe las solicitudes; sin revisores
    /// válidos se hace fallback al área de GTH (area_scope.email). Reemplaza al campo
    /// 1:1 workers.worker_salida_jefe_id y al algoritmo de jerarquía (JefeResolver).
    /// </summary>
    public class RevisorSalidaRepository : IRevisorSalidaRepository
    {
        private const string EmailDomainCorp = "@abril.pe";
        private readonly IDbContextFactory<AppDbContext> _factory;

        public RevisorSalidaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<RevisorSalidaInicialDto> GetInitialDataAsync()
        {
            // Tabla + opciones + árbol de áreas en una sola conexión.
            using var ctx = _factory.CreateDbContext();

            // 1) Trabajadores con correo corporativo @abril.pe.
            var workers = await (
                from w in ctx.Worker
                where w.EmailCorporativo != null && w.EmailCorporativo.ToLower().Contains(EmailDomainCorp)
                join p in ctx.Person on w.PersonId equals p.PersonId into pj
                from p in pj.DefaultIfEmpty()
                join c in ctx.WorkersCategory on w.WorkerCategoryId equals c.WorkersCategoryId into cj
                from c in cj.DefaultIfEmpty()
                orderby p != null ? p.FullName : ""
                select new WorkerRevisorSalidaItemDto
                {
                    WorkerId = w.Id,
                    FullName = p != null ? p.FullName : null,
                    Email = w.EmailCorporativo,
                    CategoryId = w.WorkerCategoryId,
                    Category = c != null ? c.Name : null,
                    AreaScopeId = w.AreaScopeId,
                }
            ).ToListAsync();

            // 2) Todos los revisores vivos, con los datos del revisor resueltos (una sola query).
            var asignaciones = await (
                from r in ctx.WorkersRevisores
                where r.State
                join w in ctx.Worker on r.RevisorId equals w.Id
                join p in ctx.Person on w.PersonId equals p.PersonId into pj
                from p in pj.DefaultIfEmpty()
                join c in ctx.WorkersCategory on w.WorkerCategoryId equals c.WorkersCategoryId into cj
                from c in cj.DefaultIfEmpty()
                orderby r.SolicitanteId, r.OrdenPrioridad, r.WorkersRevisoresId
                select new
                {
                    r.SolicitanteId,
                    Dto = new WorkerRevisorAsignadoDto
                    {
                        Id = r.WorkersRevisoresId,
                        RevisorWorkerId = r.RevisorId,
                        RevisorFullName = p != null ? p.FullName : null,
                        RevisorEmail = w.EmailCorporativo,
                        RevisorCategory = c != null ? c.Name : null,
                        OrdenPrioridad = r.OrdenPrioridad,
                        Active = r.Active,
                    }
                }
            ).ToListAsync();

            var porSolicitante = asignaciones
                .GroupBy(a => a.SolicitanteId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Dto).ToList());

            foreach (var w in workers)
                if (porSolicitante.TryGetValue(w.WorkerId, out var revs)) w.Revisores = revs;

            // 3) Opciones del selector (solo workers con persona viva, como antes).
            var options = await (
                from w in ctx.Worker
                where w.EmailCorporativo != null && w.EmailCorporativo.ToLower().Contains(EmailDomainCorp)
                join p in ctx.Person on w.PersonId equals p.PersonId
                where p.State == true
                orderby p.FullName
                select new WorkerRevisorSalidaOptionDto
                {
                    WorkerId = w.Id,
                    FullName = p.FullName,
                    Email = w.EmailCorporativo
                }
            ).ToListAsync();

            return new RevisorSalidaInicialDto
            {
                Workers = workers,
                Options = options,
                AreaTree = await GaAreaTreeLoader.LoadAsync(ctx),
            };
        }

        public async Task UpdateWorkerRevisoresAsync(int workerId, List<WorkerRevisorAsignacionDto> revisores)
        {
            using var ctx = _factory.CreateDbContext();

            var workerExists = await ctx.Worker.AnyAsync(w => w.Id == workerId);
            if (!workerExists)
                throw new AbrilException("El trabajador no existe.", 404);

            var deseados = revisores ?? new List<WorkerRevisorAsignacionDto>();

            // ── Validaciones ────────────────────────────────────────────────
            if (deseados.Any(r => r.RevisorWorkerId == workerId))
                throw new AbrilException("Un trabajador no puede ser su propio revisor.", 400);

            if (deseados.GroupBy(r => r.RevisorWorkerId).Any(g => g.Count() > 1))
                throw new AbrilException("No se puede asignar dos veces al mismo revisor.", 400);

            if (deseados.Any(r => r.OrdenPrioridad < 1))
                throw new AbrilException("La prioridad debe ser 1 o mayor.", 400);

            if (deseados.GroupBy(r => r.OrdenPrioridad).Any(g => g.Count() > 1))
                throw new AbrilException("No puede haber dos revisores con la misma prioridad.", 400);

            if (deseados.Count > 0)
            {
                var ids = deseados.Select(r => r.RevisorWorkerId).ToList();
                var validos = await ctx.Worker
                    .Where(w => ids.Contains(w.Id)
                                && w.EmailCorporativo != null
                                && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp))
                    .Select(w => w.Id)
                    .ToListAsync();
                var faltantes = ids.Except(validos).ToList();
                if (faltantes.Count > 0)
                    throw new AbrilException("Uno o más revisores no existen o no tienen correo corporativo @abril.pe.", 400);
            }

            // ── Diff con las filas vivas (mismo patrón que visibilidad) ─────
            var now = DateTimeOffset.UtcNow;
            var vivos = await ctx.WorkersRevisores
                .Where(r => r.State && r.SolicitanteId == workerId)
                .ToListAsync();
            var vivosByRevisor = vivos.ToDictionary(r => r.RevisorId);

            foreach (var d in deseados)
            {
                if (vivosByRevisor.TryGetValue(d.RevisorWorkerId, out var row))
                {
                    if (row.OrdenPrioridad != d.OrdenPrioridad || row.Active != d.Active)
                    {
                        row.OrdenPrioridad = d.OrdenPrioridad;
                        row.Active = d.Active;
                        row.UpdatedAt = now;
                    }
                }
                else
                {
                    ctx.WorkersRevisores.Add(new WorkersRevisores
                    {
                        SolicitanteId = workerId,
                        RevisorId = d.RevisorWorkerId,
                        OrdenPrioridad = d.OrdenPrioridad,
                        Active = d.Active,
                        State = true,
                        CreatedAt = now,
                    });
                }
            }

            var deseadosIds = deseados.Select(d => d.RevisorWorkerId).ToHashSet();
            foreach (var row in vivos)
            {
                if (!deseadosIds.Contains(row.RevisorId))
                {
                    row.State = false;
                    row.UpdatedAt = now;
                }
            }

            await ctx.SaveChangesAsync();
        }
    }
}
