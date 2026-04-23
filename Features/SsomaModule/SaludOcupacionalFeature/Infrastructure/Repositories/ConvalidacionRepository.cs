using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Convalidacion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
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

        public async Task<List<ConvalidacionListDto>> List(int? workerId)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var q =
                from cv in ctx.WorkerEmoConvalidacion
                join e in ctx.WorkerEmo on cv.EmoId equals e.Id
                join w in ctx.Worker on e.WorkerId equals w.Id
                join eo in ctx.Empresa on e.EmpresaOrigenId equals eo.Id into eoj
                from eo in eoj.DefaultIfEmpty()
                join ed in ctx.Empresa on cv.EmpresaDestinoId equals ed.Id into edj
                from ed in edj.DefaultIfEmpty()
                select new { cv, e, w, eo, ed };

            if (workerId.HasValue)
                q = q.Where(x => x.e.WorkerId == workerId.Value);

            var list = await q
                .OrderByDescending(x => x.cv.FechaConvalidacion)
                .Select(x => new ConvalidacionListDto
                {
                    Id = x.cv.Id,
                    EmoId = x.cv.EmoId,
                    WorkerId = x.e.WorkerId,
                    WorkerNombre = x.w.ApellidoNombre,
                    WorkerDni = x.w.Dni,
                    EmpresaOrigen = x.eo != null ? x.eo.RazonSocial : null,
                    EmpresaDestino = x.ed != null ? x.ed.RazonSocial : null,
                    FechaConvalidacion = x.cv.FechaConvalidacion,
                    Resultado = x.cv.Resultado,
                    FechaVencimiento = x.cv.FechaVencimiento,
                    Observaciones = x.cv.Observaciones,
                    UrlDocumento = x.cv.UrlDocumento
                })
                .ToListAsync();

            foreach (var it in list)
                if (it.FechaVencimiento.HasValue)
                    it.DiasParaVencer = it.FechaVencimiento.Value.DayNumber - hoy.DayNumber;

            return list;
        }

        public async Task<int> Create(ConvalidacionCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var emo = await ctx.WorkerEmo.FirstOrDefaultAsync(e => e.Id == dto.EmoId)
                ?? throw new AbrilException("EMO no encontrado.", 404);

            var ent = new WorkerEmoConvalidacion
            {
                EmoId = dto.EmoId,
                EmpresaDestinoId = dto.EmpresaDestinoId,
                FechaConvalidacion = dto.FechaConvalidacion,
                MedicoId = dto.MedicoId,
                Resultado = dto.Resultado,
                FechaVencimiento = dto.FechaVencimiento,
                UrlDocumento = dto.UrlDocumento,
                Observaciones = dto.Observaciones,
                RegistradoPorId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            ctx.WorkerEmoConvalidacion.Add(ent);

            if (dto.Resultado == "Aprobada" || dto.Resultado == "Aprobada con Observaciones")
            {
                emo.Estado = "Convalidado";
                emo.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await ctx.SaveChangesAsync();
            return ent.Id;
        }

        public async Task Update(int id, ConvalidacionUpdateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var ent = await ctx.WorkerEmoConvalidacion.FirstOrDefaultAsync(c => c.Id == id)
                ?? throw new AbrilException("Convalidación no encontrada.", 404);
            ent.EmpresaDestinoId = dto.EmpresaDestinoId;
            ent.FechaConvalidacion = dto.FechaConvalidacion;
            ent.MedicoId = dto.MedicoId;
            ent.Resultado = dto.Resultado;
            ent.FechaVencimiento = dto.FechaVencimiento;
            ent.UrlDocumento = dto.UrlDocumento;
            ent.Observaciones = dto.Observaciones;
            ent.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }
    }
}
