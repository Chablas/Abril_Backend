using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Convalidacion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class ConvalidacionRepository : IConvalidacionRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ConvalidacionRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<PagedResponseDto<ConvalidacionListDto>> List(ConvalidacionFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var q = from cv in ctx.WorkerEmoConvalidacion
                    join e in ctx.WorkerEmo on cv.EmoId equals e.Id
                    join w in ctx.Worker on e.WorkerId equals w.Id
                    join per in ctx.Person on w.PersonId equals per.PersonId into perj
                    from per in perj.DefaultIfEmpty()
                    join et in ctx.SsEmoTipo on e.TipoEmoId equals et.Id into etj
                    from et in etj.DefaultIfEmpty()
                    join med in ctx.SsMedicoOcupacional on cv.MedicoId equals med.Id into medj
                    from med in medj.DefaultIfEmpty()
                    join eo in ctx.Contributor on e.EmpresaOrigenId equals eo.ContributorId into eoj
                    from eo in eoj.DefaultIfEmpty()
                    join ed in ctx.Contributor on cv.EmpresaDestinoId equals ed.ContributorId into edj
                    from ed in edj.DefaultIfEmpty()
                    select new { cv, e, per, et, med, eo, ed };

            if (filter.WorkerId.HasValue)
                q = q.Where(x => x.e.WorkerId == filter.WorkerId.Value);

            if (!string.IsNullOrWhiteSpace(filter.Resultado))
                q = q.Where(x => x.cv.Resultado == filter.Resultado);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.ToLower().Trim();
                q = q.Where(x =>
                    (x.per != null && x.per.FullName.ToLower().Contains(term)) ||
                    (x.per != null && x.per.DocumentIdentityCode.ToLower().Contains(term)));
            }

            var total = await q.CountAsync();
            var page = Math.Max(filter.Page, 1);
            var pageSize = Math.Max(filter.PageSize, 1);

            var items = await q
                .OrderByDescending(x => x.cv.FechaConvalidacion)
                .ThenByDescending(x => x.cv.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ConvalidacionListDto
                {
                    Id = x.cv.Id,
                    EmoOrigenId = x.cv.EmoId,
                    WorkerId = x.e.WorkerId,
                    WorkerNombre = x.per != null ? x.per.FullName : null,
                    WorkerDni = x.per != null ? x.per.DocumentIdentityCode : null,
                    EmpresaOrigen = x.eo != null ? x.eo.ContributorName : null,
                    EmpresaDestino = x.ed != null ? x.ed.ContributorName : null,
                    TipoEmo = x.et != null ? x.et.Nombre : null,
                    Medico = x.med != null ? x.med.ApellidoNombre : null,
                    FechaEmoOrigen = x.e.FechaEmo,
                    FechaConvalidacion = x.cv.FechaConvalidacion,
                    Resultado = x.cv.Resultado,
                    FechaVencimiento = x.cv.FechaVencimiento,
                    Notas = x.cv.Observaciones,
                    UrlDocumento = x.cv.UrlDocumento,
                    EmoFechaVencimiento = x.e.FechaVencimientoCalculada ?? x.e.FechaVencimiento,
                    UrlResultado = x.e.UrlResultado,
                    UrlAptitud = x.e.UrlAptitud,
                    UrlEmoCompleto = x.e.UrlEmoCompleto,
                    InterconsultaEstado = ctx.SsInterconsulta
                        .Where(i => i.EmoId == x.e.Id)
                        .OrderByDescending(i => i.FechaDerivacion)
                        .Select(i => (string?)i.Estado)
                        .FirstOrDefault(),
                    InterconsultaEspecialidad = ctx.SsInterconsulta
                        .Where(i => i.EmoId == x.e.Id)
                        .OrderByDescending(i => i.FechaDerivacion)
                        .Select(i => (string?)i.Especialidad)
                        .FirstOrDefault(),
                    InterconsultaUrlInforme = ctx.SsInterconsulta
                        .Where(i => i.EmoId == x.e.Id)
                        .OrderByDescending(i => i.FechaDerivacion)
                        .Select(i => (string?)i.UrlInforme)
                        .FirstOrDefault()
                })
                .ToListAsync();

            foreach (var it in items)
                if (it.FechaVencimiento.HasValue)
                    it.DiasParaVencer = it.FechaVencimiento.Value.DayNumber - hoy.DayNumber;

            return new PagedResponseDto<ConvalidacionListDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Data = items
            };
        }

        public async Task<int> Create(ConvalidacionCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var emo = await ctx.WorkerEmo
                .Include(e => e.Worker)
                .FirstOrDefaultAsync(e => e.Id == dto.EmoOrigenId)
                ?? throw new AbrilException("EMO no encontrado.", 404);

            var ent = new WorkerEmoConvalidacion
            {
                EmoId = dto.EmoOrigenId,
                EmpresaDestinoId = dto.EmpresaDestinoId,
                FechaConvalidacion = dto.FechaConvalidacion,
                MedicoId = dto.MedicoId,
                Resultado = dto.Resultado,
                FechaVencimiento = dto.FechaVencimiento,
                UrlDocumento = dto.UrlDocumento,
                Observaciones = dto.Notas,
                RegistradoPorId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            ctx.WorkerEmoConvalidacion.Add(ent);

            await SincronizarHabilitacionAsync(ctx, emo, dto.Resultado, dto.FechaVencimiento);

            await ctx.SaveChangesAsync();
            return ent.Id;
        }

        public async Task Update(int id, ConvalidacionUpdateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var ent = await ctx.WorkerEmoConvalidacion.FirstOrDefaultAsync(c => c.Id == id)
                ?? throw new AbrilException("Convalidación no encontrada.", 404);

            var emo = await ctx.WorkerEmo
                .Include(e => e.Worker)
                .FirstOrDefaultAsync(e => e.Id == ent.EmoId)
                ?? throw new AbrilException("EMO de origen no encontrado.", 404);

            ent.EmpresaDestinoId = dto.EmpresaDestinoId;
            ent.FechaConvalidacion = dto.FechaConvalidacion;
            ent.MedicoId = dto.MedicoId;
            ent.Resultado = dto.Resultado;
            ent.FechaVencimiento = dto.FechaVencimiento;
            ent.UrlDocumento = dto.UrlDocumento;
            ent.Observaciones = dto.Notas;
            ent.UpdatedAt = DateTimeOffset.UtcNow;

            await SincronizarHabilitacionAsync(ctx, emo, dto.Resultado, dto.FechaVencimiento);

            await ctx.SaveChangesAsync();
        }

        private static async Task SincronizarHabilitacionAsync(
            AppDbContext ctx, WorkerEmo emo, string resultado, DateOnly? fechaVencimiento)
        {
            var workerId = emo.WorkerId;

            var hab = await ctx.SsHabTrabajador
                .FirstOrDefaultAsync(h => h.WorkerId == workerId && h.ItemId == HabItemIds.CertAptitud);

            if (hab == null)
            {
                hab = new SsHabTrabajador
                {
                    WorkerId = workerId,
                    ItemId = HabItemIds.CertAptitud,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                ctx.SsHabTrabajador.Add(hab);
            }

            switch (resultado)
            {
                case "Aprobada":
                case "Aprobada con Observaciones":
                    emo.Estado = "Convalidado";
                    emo.UpdatedAt = DateTimeOffset.UtcNow;
                    hab.Estado = "Aprobado";
                    if (fechaVencimiento.HasValue)
                        hab.Vigencia = DateTime.SpecifyKind(
                            fechaVencimiento.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                    break;
                case "Rechazada":
                    hab.Estado = "Falta";
                    break;
                case "Pendiente":
                    hab.Estado = "Pendiente";
                    break;
            }

            hab.UpdatedAt = DateTime.UtcNow;
        }
    }
}
