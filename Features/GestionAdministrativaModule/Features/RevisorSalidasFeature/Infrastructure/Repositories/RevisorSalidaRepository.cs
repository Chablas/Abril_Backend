using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Infrastructure.Repositories
{
    /// <summary>
    /// Lectura/escritura del revisor de salidas directo (workers.worker_salida_jefe_id),
    /// análogo al "Revisor de Trabajadores" de Lecciones Aprendidas pero para la
    /// aprobación de solicitudes de salida. El campo actúa como override manual del
    /// aprobador que, si está en null, cae al algoritmo de jerarquía (ApproverResolver).
    /// </summary>
    public class RevisorSalidaRepository : IRevisorSalidaRepository
    {
        private const string EmailDomainCorp = "@abril.pe";
        private readonly IDbContextFactory<AppDbContext> _factory;

        public RevisorSalidaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<WorkerRevisorSalidaItemDto>> GetWorkerRevisoresAsync()
        {
            using var ctx = _factory.CreateDbContext();

            // Trabajadores con correo corporativo @abril.pe (vive en email_corporativo)
            // + su revisor de salidas directo si lo tiene. Left join doble: persona del
            // trabajador y worker/persona del jefe pueden faltar.
            return await (
                from w in ctx.Worker
                where w.EmailCorporativo != null && w.EmailCorporativo.ToLower().Contains("@abril.pe")
                join p in ctx.Person on w.PersonId equals p.PersonId into pj
                from p in pj.DefaultIfEmpty()
                join c in ctx.WorkersCategory on w.WorkerCategoryId equals c.WorkersCategoryId into cj
                from c in cj.DefaultIfEmpty()
                join j in ctx.Worker on w.WorkerSalidaJefeId equals j.Id into jj
                from j in jj.DefaultIfEmpty()
                join jp in ctx.Person on j.PersonId equals jp.PersonId into jpj
                from jp in jpj.DefaultIfEmpty()
                join jc in ctx.WorkersCategory on j.WorkerCategoryId equals jc.WorkersCategoryId into jcj
                from jc in jcj.DefaultIfEmpty()
                orderby p != null ? p.FullName : ""
                select new WorkerRevisorSalidaItemDto
                {
                    WorkerId = w.Id,
                    FullName = p != null ? p.FullName : null,
                    Email = w.EmailCorporativo,
                    CategoryId = w.WorkerCategoryId,
                    Category = c != null ? c.Name : null,
                    JefeWorkerId = w.WorkerSalidaJefeId,
                    JefeFullName = jp != null ? jp.FullName : null,
                    JefeEmail = j != null ? j.EmailCorporativo : null,
                    JefeCategoryId = j != null ? j.WorkerCategoryId : null,
                    JefeCategory = jc != null ? jc.Name : null
                }
            ).ToListAsync();
        }

        public async Task<List<WorkerRevisorSalidaOptionDto>> GetWorkerRevisorOptionsAsync()
        {
            using var ctx = _factory.CreateDbContext();

            // Solo workers con correo corporativo @abril.pe pueden ser aprobadores válidos
            // (el correo del aprobador se deriva de email_corporativo).
            return await (
                from w in ctx.Worker
                where w.EmailCorporativo != null && w.EmailCorporativo.ToLower().Contains("@abril.pe")
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
        }

        public async Task UpdateWorkerRevisorAsync(int workerId, int? jefeWorkerId)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId);
            if (worker == null)
                throw new AbrilException("El trabajador no existe.", 404);

            if (jefeWorkerId.HasValue)
            {
                if (jefeWorkerId.Value == workerId)
                    throw new AbrilException("Un trabajador no puede ser su propio revisor.", 400);

                var jefe = await ctx.Worker
                    .Where(w => w.Id == jefeWorkerId.Value)
                    .Select(w => new { w.EmailCorporativo })
                    .FirstOrDefaultAsync();
                if (jefe == null)
                    throw new AbrilException("El revisor seleccionado no existe.", 404);
                if (string.IsNullOrWhiteSpace(jefe.EmailCorporativo) ||
                    !jefe.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp))
                    throw new AbrilException("El revisor seleccionado no tiene un correo corporativo @abril.pe.", 400);
            }

            worker.WorkerSalidaJefeId = jefeWorkerId;
            worker.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }
    }
}
